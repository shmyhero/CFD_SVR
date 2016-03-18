using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using Newtonsoft.Json;
using ServiceStack.Redis.Generic;

namespace CFD_JOBS.Ayondo
{
    public class TickChartWorker
    {
        private static int CLEAR_HISTORY_WHEN_COUNT_REACH = 60 * 24 * 20;//20 days' most possible count
        private static int CLEAR_HISTORY_TO_COUNT = 60 * 24 * 10;//10 days' most possible count

        public static void Run()
        {
            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();
            var redisClient = basicRedisClientManager.GetClient();
            var redisQuoteClient = redisClient.As<Quote>();
            var redisTickClient = redisClient.As<Tick>();

            IList<Quote> lastQuotes = null;

            CFDGlobal.LogLine("Starting...");

            while (true)
            {
                int overdueCount = 0;
                int identicalCount = 0;
                int updateCount = 0;
                int appendCount = 0;
                int newCount = 0;

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

                foreach (var quote in updatedQuotes)
                {
                    var listName = "tick:" + quote.Id;

                    IRedisList<Tick> list = redisTickClient.Lists[listName];
                    var listCount = list.Count;
                    //var listCount = redisClient.GetListCount(listName);

                    var newTick = new Tick()
                    {
                        P = quote.Offer,
                        Time = quote.Time
                    };

                    if (listCount == 0) //new quote?
                    {
                        //CFDGlobal.LogLine(quote.Id+" new");
                        newCount++;
                        list.Add(newTick);
                        //redisClient.AddItemToList(listName, JsonConvert.SerializeObject(newTick));
                        continue;
                    }

                    //clear history/prevent data increasing for good
                    if (list.Count > CLEAR_HISTORY_WHEN_COUNT_REACH) //data count at most possible size (in x days )
                    {
                        CFDGlobal.LogLine(quote.Id + " Clearing data from " + list.Count + " to " + CLEAR_HISTORY_TO_COUNT);
                        var ticks = list.GetAll();
                        var newTicks = ticks.Skip(ticks.Count - CLEAR_HISTORY_TO_COUNT);
                        list.RemoveAll();
                        list.AddRange(newTicks);
                    }

                    var lastTick = list[listCount - 1]; //last tick in cache
                    //var lastTick = JsonConvert.DeserializeObject<Tick>(redisClient.GetItemFromList(listName, (int) listCount - 1)); //last tick in cache
                    if (newTick.Time > lastTick.Time)
                    {
                        if (IsEqualDownToMinute(newTick.Time, lastTick.Time))
                        {
                            //CFDGlobal.LogLine(quote.Id + " update");
                            updateCount++;
                            list[listCount - 1] = newTick; //update last tick to new tick
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

                CFDGlobal.LogLine("updatedQuotes: " + updatedQuotes.Count +
                                  " update: " + updateCount + " append: " + appendCount + " identical: " + identicalCount + " new: " + newCount + " overdue: " + overdueCount);
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        private static bool IsEqualDownToMinute(DateTime t1, DateTime t2)
        {
            return t1.Minute == t2.Minute
                   && t1.Hour == t2.Hour
                   && t1.Day == t2.Day
                   && t1.Month == t2.Month
                   && t1.Year == t2.Year;
        }
    }
}