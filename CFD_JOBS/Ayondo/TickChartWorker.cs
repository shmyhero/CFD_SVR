using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Utils;
using ServiceStack.Redis.Generic;

namespace CFD_JOBS.Ayondo
{
    public class TickChartWorker
    {
        private static int CLEAR_HISTORY_WHEN_SIZE_1mTick = 60*24*10; //xx days' most possible count
        private static int CLEAR_HISTORY_TO_SIZE_1mTick = 60*24*7; //xx days' most possible count

        private static int CLEAR_HISTORY_WHEN_SIZE_10mTick = 6*24*15;
        private static int CLEAR_HISTORY_TO_SIZE_10mTick = 6*24*10;

        private static int CLEAR_HISTORY_WHEN_SIZE_1hTick = 24*60;
        private static int CLEAR_HISTORY_TO_SIZE_1hTick = 24*40;

        public static void Run()
        {
            //var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();
            //var redisQuoteClient = redisClient.As<Quote>();
            //var redisTickClient = redisClient.As<Tick>();

            //IList<Quote> lastQuotes = null;

            CFDGlobal.LogLine("Starting...");

            while (true)
            {
                try
                {
                    using (var redisClient = CFDGlobal.PooledRedisClientsManager.GetClient())
                    {
                        var redisQuoteClient = redisClient.As<Quote>();
                        var redisTickClient = redisClient.As<Tick>();
                        var redisProdDefClient = redisClient.As<ProdDef>();

                        var quotes = redisQuoteClient.GetAll();
                        var prodDefs = redisProdDefClient.GetAll();

                        var openingProds = prodDefs.Where(o => o.QuoteType == enmQuoteType.Open || o.QuoteType == enmQuoteType.PhoneOnly).ToList();

                        //the time of the last message received from Ayondo
                        var dtAyondoNow = quotes.Max(o => o.Time);

                        CFDGlobal.LogLine("prod: " + prodDefs.Count + " opening: " + openingProds.Count + " AyondoLastQuoteTime:" +
                                          dtAyondoNow.ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND));

                        //counters, for logging
                        int append1m = 0;
                        int update1m = 0;
                        int ignore1m = 0;
                        int append10m = 0;
                        int update10m = 0;
                        int ignore10m = 0;
                        int append1h = 0;
                        int update1h = 0;
                        int ignore1h = 0;

                        //for all OPEN products
                        foreach (var p in openingProds)
                        {
                            var quote = quotes.FirstOrDefault(o => o.Id == p.Id);

                            if (quote == null)
                            {
                                CFDGlobal.LogLine("cannot find quote for " + p.Id + " " + p.Name);
                                continue;
                            }

                            UpdateRedisTick(redisTickClient, p.Id, dtAyondoNow, quote, TickSize.OneMinute, ref append1m, ref update1m, ref ignore1m);
                            UpdateRedisTick(redisTickClient, p.Id, dtAyondoNow, quote, TickSize.TenMinute, ref append10m, ref update10m, ref ignore10m);
                            UpdateRedisTick(redisTickClient, p.Id, dtAyondoNow, quote, TickSize.OneHour, ref append1h, ref update1h, ref ignore1h);
                        }

                        CFDGlobal.LogLine("1m update: " + update1m + " append: " + append1m + " ignore: " + ignore1m);
                        CFDGlobal.LogLine("10m update: " + update10m + " append: " + append10m + " ignore: " + ignore10m);
                        CFDGlobal.LogLine("1h update: " + update1h + " append: " + append1h + " ignore: " + ignore1h);
                        CFDGlobal.LogLine("");

                        ////skip non-updated quotes to improve speed
                        //IList<Quote> updatedQuotes;
                        //if (lastQuotes != null)
                        //{
                        //    var idsToRemove = new List<int>();
                        //    foreach (var quote in quotes)
                        //    {
                        //        var lastQuote = lastQuotes.FirstOrDefault(o => o.Id == quote.Id);
                        //        if (lastQuote != null && lastQuote.Time == quote.Time)
                        //            idsToRemove.Add(quote.Id);
                        //    }
                        //    updatedQuotes = quotes.Where(o => !idsToRemove.Contains(o.Id)).ToList();
                        //}
                        //else
                        //    updatedQuotes = quotes.Select(o => o).ToList();

                        ////remember quotes for next check
                        //lastQuotes = quotes.Select(o => o).ToList();

                        ////save to redis
                        //SaveQuoteTicks(quotes, redisTickClient, TickSize.OneMinute);
                        //SaveQuoteTicks(quotes, redisTickClient, TickSize.TenMinute);
                        //SaveQuoteTicks(quotes, redisTickClient, TickSize.OneHour);
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
        }

        private static void UpdateRedisTick(IRedisTypedClient<Tick> redisTickClient, int secId, DateTime dtAyondoNow, Quote quote, TickSize tickSize,
            ref int appendCounter, ref int updateCounter, ref int ignoreCount)
        {
            //redis tick list
            var list = redisTickClient.Lists[Ticks.GetTickListNamePrefix(tickSize) + secId];

            var newTick = new Tick {P = Quotes.GetLastPrice(quote), Time = dtAyondoNow};

            if (list.Count == 0) //new products coming
            {
                appendCounter++;
                list.Add(newTick);
                return;
            }

            var last = list[list.Count - 1];

            //redis last tick is newer
            if (last.Time >= dtAyondoNow)
            {
                ignoreCount++;
                return;
            }

            //update last tick in redis
            if (Ticks.IsTickEqual(last.Time, dtAyondoNow, tickSize))
            {
                updateCounter++;
                list[list.Count - 1] = newTick;
            }
            else //append new last tick
            {
                appendCounter++;
                list.Add(newTick);
            }

            //clear history/prevent data increasing for good
            var clearWhenSize = GetClearWhenSize(tickSize);
            var clearToSize = GetClearToSize(tickSize);
            if (list.Count > clearWhenSize) //data count at most possible size (in x days )
            {
                CFDGlobal.LogLine(tickSize + " " + quote.Id + " Clearing data from " + list.Count + " to " + clearToSize);
                var ticks = list.GetAll();
                var newTicks = ticks.Skip(ticks.Count - clearToSize);
                list.RemoveAll();
                list.AddRange(newTicks);
            }
        }

        public static void SaveQuoteTicks(IList<Quote> quotes, IRedisTypedClient<Tick> redisTickClient, TickSize tickSize)
        {
            int overdueCount = 0;
            int identicalCount = 0;
            int updateCount = 0;
            int appendCount = 0;
            int newCount = 0;

            string redisListKeyPrefix = Ticks.GetTickListNamePrefix(tickSize);

            var clearWhenSize = GetClearWhenSize(tickSize);
            var clearToSize = GetClearToSize(tickSize);

            foreach (var quote in quotes)
            {
                var listName = redisListKeyPrefix + quote.Id;

                IRedisList<Tick> list = redisTickClient.Lists[listName];
                //var listCount = list.Count;
                //var listCount = redisClient.GetListCount(listName);

                var newTick = new Tick()
                {
                    P = Quotes.GetLastPrice(quote),
                    Time = quote.Time
                };

                if (list.Count == 0) //new quote?
                {
                    //CFDGlobal.LogLine(quote.Id+" new");
                    newCount++;
                    list.Add(newTick);
                    //redisClient.AddItemToList(listName, JsonConvert.SerializeObject(newTick));
                    continue;
                }

                //clear history/prevent data increasing for good
                if (list.Count > clearWhenSize) //data count at most possible size (in x days )
                {
                    CFDGlobal.LogLine(tickSize + " " + quote.Id + " Clearing data from " + list.Count + " to " + clearToSize);
                    var ticks = list.GetAll();
                    var newTicks = ticks.Skip(ticks.Count - clearToSize);
                    list.RemoveAll();
                    list.AddRange(newTicks);
                }

                var lastTick = list[list.Count - 1]; //last tick in cache
                //var lastTick = JsonConvert.DeserializeObject<Tick>(redisClient.GetItemFromList(listName, (int) listCount - 1)); //last tick in cache
                if (newTick.Time > lastTick.Time)
                {
                    if (Ticks.IsTickEqual(newTick.Time, lastTick.Time, tickSize))
                    {
                        //CFDGlobal.LogLine(quote.Id + " update");
                        updateCount++;
                        list[list.Count - 1] = newTick; //update last tick to new tick
                        //redisClient.SetItemInList(listName, (int) listCount - 1, JsonConvert.SerializeObject(newTick)); //update last tick to new tick
                    }
                    else
                    {
                        //CFDGlobal.LogLine(quote.Id + " append");
                        appendCount++;
                        list.Add(newTick); //append new tick
//                            redisClient.AddItemToList(listName, JsonConvert.SerializeObject(newTick)); //append new tick
                    }
                }
                else
                {
                    if (newTick.Time == lastTick.Time)
                        identicalCount++;
                    else
                    //CFDGlobal.LogLine(quote.Id + " skip");
                        overdueCount++;
                }
            }

            CFDGlobal.LogLine(tickSize + " total: " + quotes.Count +
                              " update: " + updateCount + " append: " + appendCount + " identical: " + identicalCount + " new: " + newCount + " overdue: " + overdueCount);
        }

        private static int GetClearWhenSize(TickSize tickSize)
        {
            switch (tickSize)
            {
                case TickSize.OneMinute:
                    return CLEAR_HISTORY_WHEN_SIZE_1mTick;
                    break;
                case TickSize.TenMinute:
                    return CLEAR_HISTORY_WHEN_SIZE_10mTick;
                    break;
                case TickSize.OneHour:
                    return CLEAR_HISTORY_WHEN_SIZE_1hTick;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("tickSize", tickSize, null);
            }
        }

        private static int GetClearToSize(TickSize tickSize)
        {
            switch (tickSize)
            {
                case TickSize.OneMinute:
                    return CLEAR_HISTORY_TO_SIZE_1mTick;
                    break;
                case TickSize.TenMinute:
                    return CLEAR_HISTORY_TO_SIZE_10mTick;
                    break;
                case TickSize.OneHour:
                    return CLEAR_HISTORY_TO_SIZE_1hTick;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("tickSize", tickSize, null);
            }
        }
    }
}