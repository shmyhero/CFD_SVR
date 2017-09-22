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
                                    o.WebHookResult == "charge.succeeded" && (o.AyTransReqSentAt == null ||
                                                                              o.AyTransId == null)).ToList();

                        CFDGlobal.LogLine(list.Count + " unsent/incomplete data...");

                        using (var client = new AyondoTradeClient(true)) //live
                        {
                            foreach (var pay in list)
                            {
                                if (pay.AyTransReqSentAt == null)
                                {
                                    if (pay.User.AyLiveBalanceId == null || pay.User.AyLiveActorId == null)
                                        CFDGlobal.LogLine(pay.Id + ": user " + pay.UserId + " has no balanceId/actorId");
                                    else
                                    {
                                        pay.AyTransReqSentAt = DateTime.UtcNow;

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
                                        CFDGlobal.LogLine(pay.Id + ": guid " + pay.AyTransReqId +
                                                          " getting transfer result error:");
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