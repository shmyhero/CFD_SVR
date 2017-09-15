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
                                    o.WebHookResult == "charge.succeeded" && (o.AyTransId == null ||
                                    o.AyTransReqId == null)).ToList();

                        CFDGlobal.LogLine(list.Count+" unsent/incomplete data...");

                        using (var client = new AyondoTradeClient(true))//live
                        {
                            foreach (var pay in list)
                            {
                                if (pay.AyTransReqId == null)
                                {
                                   if (pay.User.AyLiveBalanceId == null || pay.User.AyLiveActorId == null)
                                        CFDGlobal.LogLine(pay.Id + ": user " + pay.UserId + " has no balanceId/actorId");
                                    else
                                    {
                                        try
                                        {
                                            var guid = client.NewCashTransfer("TradeHeroHoldingAC", "dY$Tqn4KQ#",
                                                pay.AmountUSD.Value, pay.User.AyLiveBalanceId.ToString(),
                                                pay.User.AyLiveActorId.ToString());
                                            pay.AyTransReqId = guid;
                                            pay.AyTransReqSentAt = DateTime.UtcNow;

                                            CFDGlobal.LogLine(pay.Id + ": request sent " + pay.AyTransReqId);
                                        }
                                        catch (Exception e)
                                        {
                                            CFDGlobal.LogLine(pay.Id + ": user " + pay.UserId + " sending transfer request error:");
                                            CFDGlobal.LogException(e);
                                        }
                                    }
                                }
                                else if (pay.AyTransId == null)
                                {
                                    try
                                    {
                                        var transferReport = client.GetCashTransferResult(pay.AyTransReqId);
                                        pay.AyTransId = Convert.ToInt64(transferReport.TransferId);
                                        pay.AyTransStatus = transferReport.StatusCode.ToString();
                                        pay.AyTransUpdateAt = transferReport.Timestamp;
                                        pay.AyTransText = transferReport.Text;

                                        CFDGlobal.LogLine(pay.Id + ": transfer result updated " + pay.AyTransStatus);
                                    }
                                    catch (Exception e)
                                    {
                                        CFDGlobal.LogLine(pay.Id + ": guid " + pay.AyTransReqId + " getting transfer result error:");
                                        CFDGlobal.LogException(e);
                                    }
                                }
                            }

                            db.SaveChanges();
                        }
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                CFDGlobal.LogLine("");
                Thread.Sleep(_sleepInterval);
            }
        }
    }
}