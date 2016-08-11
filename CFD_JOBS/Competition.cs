using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using ServiceStack.Text;

namespace CFD_JOBS
{
    class Competition
    {
        private static DateTime _lastCalculatedDate = DateTime.MinValue;

        public static void Run()
        {
            while (true)
            {
                var chinaNow = DateTimes.GetChinaDateTimeNow();

                if (chinaNow.Date > _lastCalculatedDate) //a new day
                {
                    try
                    {
                        CFDGlobal.LogLine("------------Start------------");

                        CFDGlobal.LogLine("Getting ProdDefs and Quotes...");

                        IList<ProdDef> prodDefs;
                        IList<Quote> quotes;

                        using (var redisClient = CFDGlobal.BasicRedisClientManager.GetClient())
                        {
                            prodDefs = redisClient.As<ProdDef>().GetAll();
                            quotes = redisClient.As<Quote>().GetAll();
                        }

                        CFDGlobal.LogLine("Got " + quotes.Count + " quotes. Last one at " + quotes.Max(o => o.Time));

                        CFDGlobal.LogLine("Getting last day's 24 hours trading histories...");

                        var yesterday = chinaNow.AddDays(-1).Date;

                        var utsStart = yesterday.ToUnixTimeMs();
                        var utsEnd = chinaNow.Date.ToUnixTimeMs() - 1;

                        var url = "http://thvm-prod4.cloudapp.net:14535/demo/reports/tradehero/cn/tradehistory?start="
                                  + utsStart + "&end=" + utsEnd;

                        CFDGlobal.LogLine("downloading from " + url + "...");

                        var webClient = new WebClient();
                        var dtDownloadBegin = DateTime.UtcNow;
                        var downloadString = webClient.DownloadString(
                            "http://thvm-prod4.cloudapp.net:14535/demo/reports/tradehero/cn/tradehistory?start="
                            + utsStart + "&end=" + utsEnd);

                        CFDGlobal.LogLine("Done. " + (DateTime.UtcNow - dtDownloadBegin).TotalSeconds + "s");

                        var lines = downloadString.Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries)//skip empty line
                            .Skip(1) //skip headers
                            .Select(o => o.Split(','))
                            .ToList();

                        var tradeHistories = new List<AyondoTradeHistory>();

                        //PositionID,TradeID,AccountID,FirstName,LastName,
                        //TradeTime,ProductID,ProductName,Direction,Trade Size,
                        //Trade Price,Realized P&L,GUID,StopLoss,TakeProfit,
                        //CreationTime,UpdateType,DeviceType
                        foreach (var arr in lines)
                        {
                            var posId = Convert.ToInt64(arr[0]);
                            var tradeId = Convert.ToInt64(arr[1]);
                            var account = Convert.ToInt64(arr[2]);
                            var time = DateTime.ParseExact(arr[5], CFDGlobal.AYONDO_DATETIME_MASK,
                                CultureInfo.CurrentCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                            var secIdD = Convert.ToInt32(arr[6]);
                            var secName = arr[7];
                            var direction = arr[8];
                            var qty = Convert.ToDecimal(arr[9]);
                            var price = Convert.ToDecimal(arr[10]);
                            var pl = Convert.ToDecimal(arr[11]);
                            var guid = arr[12];
                            decimal? stopLoss = arr[13] == ""
                                ? (decimal?) null
                                : decimal.Parse(arr[13], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint);
                            //1.0E-6
                            decimal? takeProfit = arr[14] == "" ? (decimal?) null : Convert.ToDecimal(arr[14]);
                            var createTime = DateTime.ParseExact(arr[15], CFDGlobal.AYONDO_DATETIME_MASK,
                                CultureInfo.CurrentCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                            var updateType = arr[16];
                            var deviceType = arr[17];

                            var trade = new AyondoTradeHistory()
                            {
                                PositionId = posId,
                                TradeId = tradeId,
                                AccountId = account,
                                TradeTime = time,
                                SecurityId = secIdD,
                                SecurityName = secName,
                                Direction = direction,
                                Quantity = qty,
                                TradePrice = price,
                                PL = pl,
                                GUID = guid,
                                StopLoss = stopLoss,
                                TakeProfit = takeProfit,
                                CreateTime = createTime,
                                UpdateType = updateType,
                                DeviceType = deviceType,
                            };
                            tradeHistories.Add(trade);
                        }

                        CFDGlobal.LogLine("Got "+tradeHistories.Count+" trade histories.");

                        using (var db = CFDEntities.Create())
                        {
                            var participants = db.CompetitionUsers.Where(o => o.CompetitionId == 1).Include(o => o.User).ToList();

                            foreach (var competitionUser in participants)
                            {
                                var trades =
                                    tradeHistories.Where(o => o.AccountId == competitionUser.User.AyondoAccountId);
                            }
                        }

                        CFDGlobal.LogLine("------------End------------");

                        _lastCalculatedDate = chinaNow.Date;
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogException(e);
                    }
                }
                else //sleep
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}