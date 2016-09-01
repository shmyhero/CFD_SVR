using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Utils;

namespace CFD_JOBS.Ayondo
{
    public class StockAlert
    {
        public static void Run()
        {
            CFDGlobal.LogLine("Starting...");

            while (true)
            {
                try
                {
                    using (var redisClient = CFDGlobal.PooledRedisClientsManager.GetClient())
                    {
                        var redisQuoteClient = redisClient.As<Quote>();
                        var redisProdDefClient = redisClient.As<ProdDef>();

                        var quotes = redisQuoteClient.GetAll();
                        var prodDefs = redisProdDefClient.GetAll();

                        //var openingProds = prodDefs.Where(o => o.QuoteType == enmQuoteType.Open || o.QuoteType == enmQuoteType.PhoneOnly).ToList();

                        ////the time of the last message received from Ayondo
                        //var dtAyondoNow = quotes.Max(o => o.Time);

                        //CFDGlobal.LogLine("prod: " + prodDefs.Count + " opening: " + openingProds.Count + " AyondoLastQuoteTime:" +
                        //                  dtAyondoNow.ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND));

                        ////counters, for logging
                        //int append1m = 0;
                        //int update1m = 0;
                        //int ignore1m = 0;
                        //int append10m = 0;
                        //int update10m = 0;
                        //int ignore10m = 0;
                        //int append1h = 0;
                        //int update1h = 0;
                        //int ignore1h = 0;

                        ////for all OPEN products
                        //foreach (var p in openingProds)
                        //{
                        //    var quote = quotes.FirstOrDefault(o => o.Id == p.Id);

                        //    if (quote == null)
                        //    {
                        //        CFDGlobal.LogLine("cannot find quote for " + p.Id + " " + p.Name);
                        //        continue;
                        //    }

                        //    UpdateRedisTick(redisTickClient, p.Id, dtAyondoNow, quote, TickSize.OneMinute, ref append1m, ref update1m, ref ignore1m);
                        //    UpdateRedisTick(redisTickClient, p.Id, dtAyondoNow, quote, TickSize.TenMinute, ref append10m, ref update10m, ref ignore10m);
                        //    UpdateRedisTick(redisTickClient, p.Id, dtAyondoNow, quote, TickSize.OneHour, ref append1h, ref update1h, ref ignore1h);
                        //}

                        //CFDGlobal.LogLine("1m update: " + update1m + " append: " + append1m + " ignore: " + ignore1m);
                        //CFDGlobal.LogLine("10m update: " + update10m + " append: " + append10m + " ignore: " + ignore10m);
                        //CFDGlobal.LogLine("1h update: " + update1h + " append: " + append1h + " ignore: " + ignore1h);
                        //CFDGlobal.LogLine("");

                        //////skip non-updated quotes to improve speed
                        ////IList<Quote> updatedQuotes;
                        ////if (lastQuotes != null)
                        ////{
                        ////    var idsToRemove = new List<int>();
                        ////    foreach (var quote in quotes)
                        ////    {
                        ////        var lastQuote = lastQuotes.FirstOrDefault(o => o.Id == quote.Id);
                        ////        if (lastQuote != null && lastQuote.Time == quote.Time)
                        ////            idsToRemove.Add(quote.Id);
                        ////    }
                        ////    updatedQuotes = quotes.Where(o => !idsToRemove.Contains(o.Id)).ToList();
                        ////}
                        ////else
                        ////    updatedQuotes = quotes.Select(o => o).ToList();

                        //////remember quotes for next check
                        ////lastQuotes = quotes.Select(o => o).ToList();

                        //////save to redis
                        ////SaveQuoteTicks(quotes, redisTickClient, TickSize.OneMinute);
                        ////SaveQuoteTicks(quotes, redisTickClient, TickSize.TenMinute);
                        ////SaveQuoteTicks(quotes, redisTickClient, TickSize.OneHour);
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
        }
    }
}
