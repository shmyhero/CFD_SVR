using System;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using AyondoTrade;
using CFD_COMMON;
using CFD_COMMON.Models.Context;

namespace CFD_JOBS
{
    public class PingPPCashTransfer
    {
        private static readonly TimeSpan _sleepInterval = TimeSpan.FromSeconds(10);

        public static void Run()
        {
            CFDGlobal.LogLine("Starting...");

            //using (var client = new AyondoTradeClient(false)) //live
            //{
            //    var s=new AyondoTradeService();
            //    var newCashTransfer = s.NewCashTransfer("thcn3QKRz", "meFjCt", 1.99m, "139344959096", "139344791535");
            //}

            while (true)
            {
                try
                {
                    using (var db = CFDEntities.Create())
                    {
                        CFDGlobal.LogLine("loading data...");
                        var list =
                            db.PingOrders.Include(o => o.User).Where(
                                o =>
                                    o.WebHookResult == "charge.succeeded" &&
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
                                    if (pay.AyTransReqSentAt == null)
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
                                                    pay.AmountUSD.Value, pay.User.AyLiveBalanceId.ToString(),
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
                                                      " ping order process error:");
                                    CFDGlobal.LogException(e);

                                    ElmahLogForJOB.Log(e);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogLine("PingPPCashTransfer job main loop error:");
                    CFDGlobal.LogException(e);

                    ElmahLogForJOB.Log(e);
                }

                CFDGlobal.LogLine("");
                Thread.Sleep(_sleepInterval);
            }
        }
    }
}