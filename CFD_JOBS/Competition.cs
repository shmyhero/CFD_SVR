using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using AyondoTrade;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;

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
                var chinaToday = chinaNow.Date;

                var endDate = chinaToday;
                var startDate = endDate.AddDays(-1);

                if (chinaToday > _lastCalculatedDate //a new chinese day, calculate yesterday's data
                    )
                {
                    try
                    {
                        //skip weekend
                        if (startDate.DayOfWeek == DayOfWeek.Saturday)
                        {
                            startDate = startDate.AddDays(-1);
                            endDate = endDate.AddDays(-1);
                        }
                        else if (startDate.DayOfWeek == DayOfWeek.Sunday)
                        {
                            startDate = startDate.AddDays(-2);
                            endDate = endDate.AddDays(-2);
                        }

                        CFDGlobal.LogLine("checking db for existing competition results on " + startDate);
                        int existedResultCount = 0;
                        using (var db = CFDEntities.Create())
                        {
                            existedResultCount = db.CompetitionResults.Count(o => o.Date == startDate);
                        }

                        if (existedResultCount > 0)
                        {
                            CFDGlobal.LogLine(existedResultCount +
                                              " existing competition results found. skip for 1 day.");
                        }
                        else
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

                            #region //get trade history via HTTP API

                            //CFDGlobal.LogLine("Getting last day's 24 hours trading histories...");

                            //var utsStart = yesterday.ToUnixTimeMs();
                            //var utsEnd = chinaNow.Date.ToUnixTimeMs() - 1;

                            //var url = "http://thvm-prod4.cloudapp.net:14535/demo/reports/tradehero/cn/tradehistory?start="
                            //          + utsStart + "&end=" + utsEnd;

                            //CFDGlobal.LogLine("downloading from " + url + "...");

                            //var webClient = new WebClient();
                            //var dtDownloadBegin = DateTime.UtcNow;
                            //var downloadString = webClient.DownloadString(
                            //    "http://thvm-prod4.cloudapp.net:14535/demo/reports/tradehero/cn/tradehistory?start="
                            //    + utsStart + "&end=" + utsEnd);

                            //CFDGlobal.LogLine("Done. " + (DateTime.UtcNow - dtDownloadBegin).TotalSeconds + "s");

                            //var lines = downloadString.Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries)//skip empty line
                            //    .Skip(1) //skip headers
                            //    .Select(o => o.Split(','))
                            //    .ToList();

                            //var tradeHistories = new List<AyondoTradeHistory>();

                            ////PositionID,TradeID,AccountID,FirstName,LastName,
                            ////TradeTime,ProductID,ProductName,Direction,Trade Size,
                            ////Trade Price,Realized P&L,GUID,StopLoss,TakeProfit,
                            ////CreationTime,UpdateType,DeviceType
                            //foreach (var arr in lines)
                            //{
                            //    var posId = Convert.ToInt64(arr[0]);
                            //    var tradeId = Convert.ToInt64(arr[1]);
                            //    var account = Convert.ToInt64(arr[2]);
                            //    var time = DateTime.ParseExact(arr[5], CFDGlobal.AYONDO_DATETIME_MASK,
                            //        CultureInfo.CurrentCulture,
                            //        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                            //    var secIdD = Convert.ToInt32(arr[6]);
                            //    var secName = arr[7];
                            //    var direction = arr[8];
                            //    var qty = Convert.ToDecimal(arr[9]);
                            //    var price = Convert.ToDecimal(arr[10]);
                            //    var pl = Convert.ToDecimal(arr[11]);
                            //    var guid = arr[12];
                            //    decimal? stopLoss = arr[13] == ""
                            //        ? (decimal?) null
                            //        : decimal.Parse(arr[13], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint);
                            //    //1.0E-6
                            //    decimal? takeProfit = arr[14] == "" ? (decimal?) null : Convert.ToDecimal(arr[14]);
                            //    var createTime = DateTime.ParseExact(arr[15], CFDGlobal.AYONDO_DATETIME_MASK,
                            //        CultureInfo.CurrentCulture,
                            //        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                            //    var updateType = arr[16];
                            //    var deviceType = arr[17];

                            //    var trade = new AyondoTradeHistory()
                            //    {
                            //        PositionId = posId,
                            //        TradeId = tradeId,
                            //        AccountId = account,
                            //        TradeTime = time,
                            //        SecurityId = secIdD,
                            //        SecurityName = secName,
                            //        Direction = direction,
                            //        Quantity = qty,
                            //        TradePrice = price,
                            //        PL = pl,
                            //        GUID = guid,
                            //        StopLoss = stopLoss,
                            //        TakeProfit = takeProfit,
                            //        CreateTime = createTime,
                            //        UpdateType = updateType,
                            //        DeviceType = deviceType,
                            //    };
                            //    tradeHistories.Add(trade);
                            //}

                            //CFDGlobal.LogLine("Got "+tradeHistories.Count+" trade histories."); 

                            #endregion

                            using (var db = CFDEntities.Create())
                            {
                                var participants =
                                    db.CompetitionUsers.Where(o => o.CompetitionId == 1).Include(o => o.User).ToList();

                                CFDGlobal.LogLine("found " + participants.Count + " participants");

                                var competitionResults = new List<CompetitionResult>();

                                //for each user
                                foreach (var competitionUser in participants)
                                {
                                    #region //process data from trade histories

                                    //var trades =
                                    //    tradeHistories.Where(o => o.AccountId == competitionUser.User.AyondoAccountId).ToList();

                                    ////UpdateType: CREATE,UPDATE,DELETE

                                    //var createList = trades.Where(o => o.UpdateType == "CREATE").ToList();

                                    //if (createList.Count == 0)//no new position
                                    //    continue;

                                    //var deleteList = trades.Where(o => o.UpdateType == "DELETE").ToList();

                                    //foreach (var create in createList)
                                    //{
                                    //    var competitionUserPosition=new CompetitionUserPosition()
                                    //    {
                                    //        CompetitionId = 1,
                                    //        Date = yesterday,
                                    //        PositionId = create.PositionId.Value,
                                    //        SecurityId = create.SecurityId,
                                    //        SecurityName = Translator.GetCName(create.SecurityName),
                                    //        UserId = competitionUser.UserId,
                                    //    };

                                    //    var prodDef = prodDefs.FirstOrDefault(o => o.Id == create.SecurityId);

                                    //    if(prodDef==null)
                                    //        continue;

                                    //    var tradeValue = create.TradePrice * prodDef.LotSize / prodDef.PLUnits * create.Quantity;
                                    //    var invest = FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2, "USD",prodDefs, quotes) / create.Leverage.Value;

                                    //    var delete = deleteList.FirstOrDefault(o => o.PositionId == create.PositionId);
                                    //    if (delete == null) //not closed. use UPL
                                    //    {

                                    //    }
                                    //    else//closed. use PL
                                    //    {

                                    //    }
                                    //} 

                                    #endregion

                                    if (string.IsNullOrEmpty(competitionUser.User.AyondoUsername))
                                    {
                                        CFDGlobal.LogLine("user " + competitionUser.UserId +
                                                          " has no ayondo account, skip");
                                        continue;
                                    }

                                    var userPositions = new List<CompetitionUserPosition>();

                                    var clientHttp = new AyondoTradeClient();
                                    var openPositions = clientHttp.GetPositionReport(
                                        competitionUser.User.AyondoUsername,
                                        competitionUser.User.AyondoPassword);

                                    //yesterday created open positions
                                    var yesterdayOpenedPositions =
                                        openPositions.Where(
                                            o => o.CreateTime >= startDate && o.CreateTime < endDate)
                                            .ToList();

                                    foreach (var position in yesterdayOpenedPositions)
                                    {
                                        var secId = Convert.ToInt32(position.SecurityID);

                                        var prodDef = prodDefs.FirstOrDefault(o => o.Id == secId);
                                        if (prodDef == null) continue;

                                        var competitionUserPosition = new CompetitionUserPosition()
                                        {
                                            CompetitionId = 1,
                                            PositionId = Convert.ToInt64(position.PosMaintRptID),
                                            Date = startDate,
                                            SecurityId = secId,
                                            SecurityName = Translator.GetCName(prodDef.Name),
                                            UserId = competitionUser.UserId,
                                        };

                                        //invest
                                        var tradeValue = position.SettlPrice*prodDef.LotSize/prodDef.PLUnits*
                                                         (position.LongQty ?? position.ShortQty);
                                        var invest =
                                            FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2, "USD", prodDefs,
                                                quotes)/position.Leverage.Value;
                                        competitionUserPosition.Invest = invest;

                                        //upl
                                        var quote = quotes.FirstOrDefault(o => o.Id == secId);
                                        decimal uplUSD = 0;
                                        if (quote != null)
                                        {
                                            var upl = position.LongQty.HasValue
                                                ? tradeValue.Value*(quote.Bid/position.SettlPrice - 1)
                                                : tradeValue.Value*(1 - quote.Offer/position.SettlPrice);
                                            uplUSD = FX.ConvertPlByOutright(upl, prodDef.Ccy2, "USD", prodDefs, quotes);
                                        }
                                        competitionUserPosition.PL = uplUSD;

                                        userPositions.Add(competitionUserPosition);
                                    }

                                    var historyReports =
                                        clientHttp.GetPositionHistoryReport(competitionUser.User.AyondoUsername,
                                            competitionUser.User.AyondoPassword, startDate,
                                            endDate.AddMilliseconds(-1));

                                    var groupByPositions = historyReports.GroupBy(o => o.PosMaintRptID);

                                    foreach (var group in groupByPositions)
                                    {
                                        var posId = Convert.ToInt64(group.Key);
                                        var reports = group.ToList();

                                        if (reports.Count < 2) continue;

                                        var openReport = reports.OrderBy(o => o.CreateTime).First();
                                        var closeReport = reports.OrderBy(o => o.CreateTime).Last();

                                        var secId = Convert.ToInt32(openReport.SecurityID);

                                        var prodDef = prodDefs.FirstOrDefault(o => o.Id == secId);
                                        if (prodDef == null) continue;

                                        var competitionUserPosition = new CompetitionUserPosition()
                                        {
                                            CompetitionId = 1,
                                            PositionId = posId,
                                            Date = startDate,
                                            SecurityId = secId,
                                            SecurityName = Translator.GetCName(prodDef.Name),
                                            UserId = competitionUser.UserId,
                                        };

                                        //invest
                                        var tradeValue = openReport.SettlPrice*prodDef.LotSize/prodDef.PLUnits*
                                                         (openReport.LongQty ?? openReport.ShortQty);
                                        var tradeValueUSD = tradeValue;
                                        if (prodDef.Ccy2 != "USD")
                                            tradeValueUSD = FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2,
                                                "USD", prodDefs, quotes);
                                        var invest = tradeValueUSD.Value/openReport.Leverage.Value;
                                        competitionUserPosition.Invest = invest;

                                        //pl
                                        var pl = closeReport.PL.Value;
                                        competitionUserPosition.PL = pl;

                                        userPositions.Add(competitionUserPosition);
                                    }

                                    if (userPositions.Count > 0)
                                    {
                                        CFDGlobal.LogLine("found " + userPositions.Count + " positions for user " +
                                                          competitionUser.User.Id);

                                        db.CompetitionUserPositions.AddRange(userPositions);

                                        competitionResults.Add(new CompetitionResult()
                                        {
                                            CompetitionId = 1,
                                            Date = startDate,
                                            Invest = userPositions.Sum(o => o.Invest),
                                            Nickname = competitionUser.User.Nickname,
                                            Phone = competitionUser.Phone,
                                            PL = userPositions.Sum(o => o.PL),
                                            PositionCount = userPositions.Count,
                                            UserId = competitionUser.UserId,
                                        });
                                    }
                                }

                                //calculate ranking by ROI
                                if (competitionResults.Count > 0)
                                {
                                    CFDGlobal.LogLine("summing " + competitionResults.Count + " users' results");

                                    var orderedResults =
                                        competitionResults.OrderByDescending(o => o.PL/o.Invest).ToList();
                                    for (int i = 0; i < orderedResults.Count; i++)
                                    {
                                        orderedResults[i].Rank = i + 1;
                                    }

                                    db.CompetitionResults.AddRange(orderedResults);
                                    db.SaveChanges();
                                }
                            }

                            CFDGlobal.LogLine("------------End------------");
                        }

                        _lastCalculatedDate = chinaToday;
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