﻿using System;
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
    class Competition2
    {
        private static DateTime _lastCalculatedDate = DateTime.MinValue;

        public static void Run()
        {
            while (true)
            {
                var chinaNow = DateTimes.GetChinaNow();
                var chinaToday = chinaNow.Date;

                if (chinaNow.Hour == 0 //only run this job between 0:00~1:00 am
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
                                && prodDefs.Count > 0 && (DateTime.UtcNow - prodDefs.Max(o => o.LastClose)) > TimeSpan.FromHours(1)//for more than 1 hour
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

                                    var allPositionsYesterday =
                                        db.NewPositionHistories.Where(
                                            o => o.CreateTime >= startDateUtc && o.CreateTime < endDateUtc).ToList();

                                    var competitionResults = new List<CompetitionResult>();

                                    //for each user
                                    foreach (var competitionUser in participants)
                                    {
                                        try
                                        {
                                            if (string.IsNullOrEmpty(competitionUser.User.AyondoUsername))
                                            {
                                                CFDGlobal.LogLine("user " + competitionUser.UserId +
                                                                  " has no ayondo demo account, skip");
                                                continue;
                                            }

                                            var userPositions = new List<CompetitionUserPosition>();

                                            var allPositions =
                                                allPositionsYesterday.Where(o => o.UserId == competitionUser.UserId)
                                                    .ToList();

                                            if (allPositions.Count == 0)
                                                continue;

                                            var yesterdayOpenedPositions = allPositions.Where(o => o.ClosedAt == null).ToList();
                                            var closedPositions = allPositions.Where(o => o.ClosedAt.HasValue).ToList();

                                            CFDGlobal.LogLine("found " + allPositions.Count + "(" +
                                                              yesterdayOpenedPositions.Count + "/" +
                                                              closedPositions.Count + ")" + " positions for user " +
                                                              competitionUser.User.Id);

                                            foreach (var position in yesterdayOpenedPositions)
                                            {
                                                var secId = Convert.ToInt32(position.SecurityId);

                                                var prodDef = prodDefs.FirstOrDefault(o => o.Id == secId);
                                                if (prodDef == null) continue;

                                                var competitionUserPosition = new CompetitionUserPosition()
                                                {
                                                    CompetitionId = 1,
                                                    PositionId = Convert.ToInt64(position.Id),
                                                    Date = startDateCN,
                                                    SecurityId = secId,
                                                    SecurityName = Translator.GetCName(prodDef.Name),
                                                    UserId = competitionUser.UserId,
                                                };

                                                var tradeValue = position.SettlePrice * prodDef.LotSize / prodDef.PLUnits *
                                                                 (position.LongQty ?? position.ShortQty);
                                                //var invest =
                                                //    FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2, "USD",
                                                //        prodDefs,
                                                //        quotes) / position.Leverage.Value;
                                                //competitionUserPosition.Invest = invest;
                                                competitionUserPosition.Invest = position.InvestUSD.Value;

                                                //upl
                                                var quote = quotes.FirstOrDefault(o => o.Id == secId);
                                                decimal uplUSD = 0;
                                                if (quote != null)
                                                {
                                                    var upl = position.LongQty.HasValue
                                                        ? tradeValue.Value * (quote.Bid / position.SettlePrice.Value - 1)
                                                        : tradeValue.Value * (1 - quote.Offer / position.SettlePrice.Value);
                                                    uplUSD = FX.ConvertPlByOutright(upl, prodDef.Ccy2, "USD", prodDefs,
                                                        quotes);
                                                }
                                                competitionUserPosition.PL = uplUSD;

                                                userPositions.Add(competitionUserPosition);
                                            }

                                            foreach (var position in closedPositions)
                                            {
                                                var secId = Convert.ToInt32(position.SecurityId);

                                                var prodDef = prodDefs.FirstOrDefault(o => o.Id == secId);
                                                if (prodDef == null) continue;

                                                var competitionUserPosition = new CompetitionUserPosition()
                                                {
                                                    CompetitionId = 1,
                                                    PositionId = Convert.ToInt64(position.Id),
                                                    Date = startDateCN,
                                                    SecurityId = secId,
                                                    SecurityName = Translator.GetCName(prodDef.Name),
                                                    UserId = competitionUser.UserId,
                                                };

                                                competitionUserPosition.Invest = position.InvestUSD;

                                                competitionUserPosition.PL = position.PL;

                                                userPositions.Add(competitionUserPosition);
                                            }

                                            if (userPositions.Count > 0)
                                            {
                                                //    CFDGlobal.LogLine("found " + userPositions.Count + " positions for user " +
                                                //                      competitionUser.User.Id);

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
                                        catch (Exception e)
                                        {
                                            CFDGlobal.LogLine("error processing user " + competitionUser.UserId);
                                            CFDGlobal.LogException(e);
                                        }
                                    }

                                    //calculate ranking by ROI
                                    if (competitionResults.Count > 0)
                                    {
                                        CFDGlobal.LogLine("summing " + competitionResults.Count + " users' results");

                                        var orderedResults =
                                            competitionResults.OrderByDescending(o => o.PL / o.Invest).ToList();
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