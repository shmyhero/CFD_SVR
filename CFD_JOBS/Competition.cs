using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using AyondoTrade;
using AyondoTrade.Model;
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
                var chinaNow = DateTimes.GetChinaNow();
                var chinaToday = chinaNow.Date;

                if (chinaNow.Hour==0 //only run this job between 0:00~1:00 am
                    && chinaToday > _lastCalculatedDate) //a new chinese day, calculate yesterday's data
                {
                    try
                    {
                        //china start/end time
                        var endDateCN = chinaToday;
                        var startDateCN = endDateCN.AddDays(-1);

                        //readjust china start/end time  (skip weekend)
                        if (startDateCN.DayOfWeek == DayOfWeek.Saturday)
                        {
                            startDateCN = startDateCN.AddDays(-1);
                            endDateCN = endDateCN.AddDays(-1);
                        }
                        else if (startDateCN.DayOfWeek == DayOfWeek.Sunday)
                        {
                            startDateCN = startDateCN.AddDays(-2);
                            endDateCN = endDateCN.AddDays(-2);
                        }

                        //utc start/end time
                        var endDateUtc = endDateCN.AddHours(-8);
                        var startDateUtc = startDateCN.AddHours(-8);

                        CFDGlobal.LogLine("checking db for existing competition results on " + startDateCN);
                        int existedResultCount = 0;
                        using (var db = CFDEntities.Create())
                        {
                            existedResultCount = db.CompetitionResults.Count(o => o.Date == startDateCN);
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

                            if (!prodDefs.Any(o => o.QuoteType == enmQuoteType.Open || o.QuoteType == enmQuoteType.PhoneOnly)//all closed
                                && prodDefs.Count>0 && (DateTime.UtcNow- prodDefs.Max(o=>o.LastClose))>TimeSpan.FromHours(1)//for more than 1 hour
                                )
                            {
                                CFDGlobal.LogLine("all products are closed for more than 1 hour --> must be a holiday");
                            }
                            else
                            {
                                CFDGlobal.LogLine("Got " + quotes.Count + " quotes. Last one at " +
                                                  quotes.Max(o => o.Time));

                                using (var db = CFDEntities.Create())
                                {
                                    var participants =
                                        db.CompetitionUsers.Where(o => o.CompetitionId == 1)
                                            .Include(o => o.User)
                                            .ToList();

                                    CFDGlobal.LogLine("found " + participants.Count + " participants");

                                    var competitionResults = new List<CompetitionResult>();

                                    //for each user
                                    foreach (var competitionUser in participants)
                                    {
                                        if (string.IsNullOrEmpty(competitionUser.User.AyondoUsername))
                                        {
                                            CFDGlobal.LogLine("user " + competitionUser.UserId +
                                                              " has no ayondo account, skip");
                                            continue;
                                        }

                                        var userPositions = new List<CompetitionUserPosition>();

                                        IList<PositionReport> openPositions;
                                        IList<PositionReport> historyReports;
                                        using (var clientHttp = new AyondoTradeClient())
                                        {
                                            openPositions = clientHttp.GetPositionReport(
                                                competitionUser.User.AyondoUsername, competitionUser.User.AyondoPassword);
                                            historyReports =
                                                clientHttp.GetPositionHistoryReport(competitionUser.User.AyondoUsername,
                                                    competitionUser.User.AyondoPassword, startDateUtc,
                                                    endDateUtc.AddMilliseconds(-1), true, false);
                                        }

                                        //yesterday created open positions
                                        var yesterdayOpenedPositions =
                                            openPositions.Where(
                                                o => o.CreateTime >= startDateUtc && o.CreateTime < endDateUtc)
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
                                                Date = startDateCN,
                                                SecurityId = secId,
                                                SecurityName = Translator.GetCName(prodDef.Name),
                                                UserId = competitionUser.UserId,
                                            };

                                            //invest
                                            var tradeValue = position.SettlPrice*prodDef.LotSize/prodDef.PLUnits*
                                                             (position.LongQty ?? position.ShortQty);
                                            var invest =
                                                FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2, "USD",
                                                    prodDefs,
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
                                                uplUSD = FX.ConvertPlByOutright(upl, prodDef.Ccy2, "USD", prodDefs,
                                                    quotes);
                                            }
                                            competitionUserPosition.PL = uplUSD;

                                            userPositions.Add(competitionUserPosition);
                                        }

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
                                                Date = startDateCN,
                                                SecurityId = secId,
                                                SecurityName = Translator.GetCName(prodDef.Name),
                                                UserId = competitionUser.UserId,
                                            };

                                            //invest
                                            var tradeValue = openReport.SettlPrice*prodDef.LotSize/prodDef.PLUnits*
                                                             (openReport.LongQty ?? openReport.ShortQty);
                                            var tradeValueUSD = tradeValue;
                                            if (prodDef.Ccy2 != "USD")
                                                tradeValueUSD = FX.ConvertByOutrightMidPrice(tradeValue.Value,
                                                    prodDef.Ccy2,
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
                                                Date = startDateCN,
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
                        }

                        _lastCalculatedDate = chinaToday;
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogException(e);
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
    }
}