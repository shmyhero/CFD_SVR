using System;
using System.Linq;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using ServiceStack.Redis.Generic;

namespace CFD_JOBS.Ayondo
{
    public class TickChartWorker
    {
        public static void Run()
        {
            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();
            var redisClient = basicRedisClientManager.GetClient();
            var redisQuoteClient = redisClient.As<Quote>();
            var redisTickClient = redisClient.As<Tick>();

            while (true)
            {
                try
                {
                    var quotes = redisQuoteClient.GetAll();

                    //var tickListNames = redisClient.SearchKeys("tick:*");

                    foreach (var quote in quotes)
                    {
                        //if (DateTime.UtcNow - quote.Time > TimeSpan.FromMinutes(1))
                        //    continue;

                        var listName = "tick:" + quote.Id;
                        IRedisList<Tick> list = redisTickClient.Lists[listName];

                        if (list.Count == 0)
                        {
                            list.Add(new Tick() {P = quote.Offer, Time = quote.Time});
                            continue;
                        }

                        //var list = redisTickClient.Lists["tick:" + quote.Id];

                        //if(list.)

                        var last = list.Last();

                        var o = quote.Time - last.Time;

                        //if(last.Time)

                        //var tick = new Tick() { P = quote.Offer, Time = quote.Time };
                        //list.Add(tick);

                        //var all = list.GetAll();

                        //l.Add(tick);
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
    }
}