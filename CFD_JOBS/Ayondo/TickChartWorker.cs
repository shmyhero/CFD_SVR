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
            //var basicRedisClientManager = CFDGlobal.GetNewBasicRedisClientManager();
            var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();
            var redisQuoteClient = redisClient.As<Quote>();
            var redisTickClient = redisClient.As<Tick>();

            IList<Quote> lastQuotes = null;

            CFDGlobal.LogLine("Starting...");

            while (true)
            {
                try
                {
                    var quotes = redisQuoteClient.GetAll();

                    //skip non-updated quotes to improve speed
                    IList<Quote> updatedQuotes;
                    if (lastQuotes != null)
                    {
                        var idsToRemove = new List<int>();
                        foreach (var quote in quotes)
                        {
                            var lastQuote = lastQuotes.FirstOrDefault(o => o.Id == quote.Id);
                            if (lastQuote != null && lastQuote.Time == quote.Time)
                                idsToRemove.Add(quote.Id);
                        }
                        updatedQuotes = quotes.Where(o => !idsToRemove.Contains(o.Id)).ToList();
                    }
                    else
                        updatedQuotes = quotes.Select(o => o).ToList();

                    //remember quotes for next check
                    lastQuotes = quotes.Select(o => o).ToList();

                    //save to redis
                    SaveQuoteTicks(updatedQuotes, redisTickClient, TickSize.OneMinute);
                    SaveQuoteTicks(updatedQuotes, redisTickClient, TickSize.TenMinute);
                    SaveQuoteTicks(updatedQuotes, redisTickClient, TickSize.OneHour);
                    CFDGlobal.LogLine("");
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        public static void SaveQuoteTicks(IList<Quote> quotes, IRedisTypedClient<Tick> redisTickClient, TickSize tickSize)
        {
            int overdueCount = 0;
            int identicalCount = 0;
            int updateCount = 0;
            int appendCount = 0;
            int newCount = 0;

            string redisListKeyPrefix;
            switch (tickSize)
            {
                case TickSize.OneMinute:
                    redisListKeyPrefix = "tick:";
                    break;
                case TickSize.TenMinute:
                    redisListKeyPrefix = "tick10m:";
                    break;
                case TickSize.OneHour:
                    redisListKeyPrefix = "tick1h:";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("tickSize", tickSize, null);
            }

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
                    if (IsTickEqual(newTick.Time, lastTick.Time, tickSize))
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

        private static bool IsTickEqual(DateTime t1, DateTime t2, TickSize tickSize)
        {
            switch (tickSize)
            {
                case TickSize.OneMinute:
                    return DateTimes.IsEqualDownToMinute(t1, t2);
                    break;
                case TickSize.TenMinute:
                    return DateTimes.IsEqualDownTo10Minute(t1, t2);
                    break;
                case TickSize.OneHour:
                    return DateTimes.IsEqualDownToHour(t1, t2);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("tickSize", tickSize, null);
            }
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

        public enum TickSize
        {
            OneMinute,
            TenMinute,
            OneHour
        }
    }
}