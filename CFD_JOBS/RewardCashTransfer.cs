using System;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using AyondoTrade;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Service;

namespace CFD_JOBS
{
    class RewardCashTransfer
    {
        private static readonly TimeSpan _sleepInterval = TimeSpan.FromSeconds(10);
        private const decimal rewardFxRate = 6.8M;
        public static void Run()
        {
            CFDGlobal.LogLine("Starting...");

            while (true)
            {
                try
                {
                    using (var db = CFDEntities.Create())
                    {
                        CFDGlobal.LogLine("loading data...");
                        var list =
                            db.OrderRewardUsages.Include(o => o.User).Where(
                                o =>
                                    o.PingPaidAt.HasValue &&
                                    (
                                        o.AyTransReqSentAt == null //never sent
                                        ||
                                        o.AyTransReqId != null && o.AyTransId == null
                                        //sent and has reqId, waiting for getting transId
                                        )
                                )
                                .ToList();

                        CFDGlobal.LogLine(list.Count + " unsent/incomplete data...");

                        using (var client = new AyondoTradeClient(true)) //live
                        {
                            foreach (var pay in list)
                            {
                                try
                                {
                                    if (pay.AyTransReqSentAt == null && HasEnoughReward(pay.UserId?? 0, pay.RewardAmountUSD?? 0 * rewardFxRate, db))
                                    {
                                        if (pay.User.AyLiveBalanceId == null || pay.User.AyLiveActorId == null)
                                            CFDGlobal.LogLine(pay.Id + ": user " + pay.UserId +
                                                              " has no balanceId/actorId");
                                        else
                                        {
                                            pay.AyTransReqSentAt = DateTime.UtcNow;

                                            //make sure that there is no replicated requesting
                                            db.SaveChanges();

                                            try
                                            {
                                                var guid = client.NewCashTransfer("TradeHeroHoldingAC", "dY$Tqn4KQ#",
                                                    pay.RewardAmountUSD.Value, pay.User.AyLiveBalanceId.ToString(),
                                                    pay.User.AyLiveActorId.ToString());

                                                pay.AyTransReqId = guid;
                                               
                                                CFDGlobal.LogLine(pay.Id + ": request sent " + pay.AyTransReqId);
                                            }
                                            catch (Exception e)
                                            {
                                                pay.AyTransReqSentResult = e.Message + "\r\n" + e.StackTrace;

                                                CFDGlobal.LogLine(pay.Id + ": user " + pay.UserId +
                                                                  " sending FIX transfer request error:");
                                                CFDGlobal.LogException(e);

                                                ElmahLogForJOB.Log(e);
                                            }

                                            db.SaveChanges();
                                        }
                                    }
                                    else if (pay.AyTransId == null && pay.AyTransReqId != null)
                                    {
                                        try
                                        {
                                            var transferReport = client.GetCashTransferResult(pay.AyTransReqId);
                                            pay.AyTransId = Convert.ToInt64(transferReport.TransferId);
                                            pay.AyTransStatus = transferReport.StatusCode.ToString();
                                            pay.AyTransUpdateAt = transferReport.Timestamp;
                                            pay.AyTransText = transferReport.Text;

                                            CFDGlobal.LogLine(pay.Id + ": transfer result updated " + pay.AyTransStatus);

                                            db.RewardTransfers.Add(new CFD_COMMON.Models.Entities.RewardTransfer()
                                            {
                                                Amount = (pay.RewardAmountUSD ?? 0) * rewardFxRate,
                                                UserID = pay.UserId ?? 0,
                                                CreatedAt = DateTime.UtcNow
                                            });

                                            db.SaveChanges();
                                        }
                                        catch (Exception e)
                                        {
                                            CFDGlobal.LogLine(pay.Id + ": guid " + pay.AyTransReqId +
                                                              " getting transfer result error:");
                                            CFDGlobal.LogException(e);

                                            ElmahLogForJOB.Log(e);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    CFDGlobal.LogLine(pay.Id + ": user " + pay.UserId +
                                                      " reward process error:");
                                    CFDGlobal.LogException(e);

                                    ElmahLogForJOB.Log(e);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogLine("RewardCashTransfer job main loop error:");
                    CFDGlobal.LogException(e);

                    ElmahLogForJOB.Log(e);
                }

                CFDGlobal.LogLine("");
                Thread.Sleep(_sleepInterval);
            }
        }

        private static bool HasEnoughReward(int userId, decimal rewardToTransfer, CFDEntities db)
        {
            RewardService service = new RewardService(db);
            var rewardDetail = service.GetTotalReward(userId);

            //总支出的交易金
            var transferredReward = db.RewardTransfers.Where(o => o.UserID == userId).Select(o => o.Amount).DefaultIfEmpty(0).Sum();
            //未结算的竞猜结果作为支出
            transferredReward += db.QuizBets.Where(o => o.UserID == userId && !o.SettledAt.HasValue).Select(o => o.PL).DefaultIfEmpty(0).Sum(o => Math.Abs(o.Value));

            //总交易金
            var totalReward = rewardDetail.referralReward + rewardDetail.liveRegister + rewardDetail.demoRegister + rewardDetail.totalCard + rewardDetail.totalDailySign + rewardDetail.totalDemoTransaction + rewardDetail.firstDeposit + rewardDetail.demoProfit + rewardDetail.quizSettled;

            if((totalReward - transferredReward) >= rewardToTransfer)
            {
                return true;
            }

            else
            {
                return false;
            }

        }
    }
}
