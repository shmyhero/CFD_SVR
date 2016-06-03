using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using Elmah;

namespace CFD_API.Caching
{
    public class WebCache
    {
        private static Timer _timerProdDef;
        private static Timer _timerQuote;
        private static Timer _timerTick;

        private static TimeSpan _updateIntervalProdDef = TimeSpan.FromSeconds(5);
        private static TimeSpan _updateIntervalQuote = TimeSpan.FromSeconds(1);
        private static TimeSpan _updateIntervalTick = TimeSpan.FromSeconds(10);

        public static IList<ProdDef> ProdDefs { get; private set; }
        public static IList<Quote> Quotes { get; private set; }
        public static ConcurrentDictionary<int, List<TickDTO>> TickToday { get; private set; }

        static WebCache()
        {
            //initialize
            ProdDefs = new List<ProdDef>();
            Quotes = new List<Quote>();
            TickToday = new ConcurrentDictionary<int, List<TickDTO>>();

            //get value from Redis
            try
            {
                ProdDefs = CFDGlobal.BasicRedisClientManager.GetClient().As<ProdDef>().GetAll();
                Quotes = CFDGlobal.BasicRedisClientManager.GetClient().As<Quote>().GetAll();
            }
            catch (Exception e)
            {
                CFDGlobal.LogExceptionAsInfo(e);
            }

            //set timer
            _timerProdDef = new Timer(UpdateProdDefs, null, _updateIntervalProdDef, _updateIntervalProdDef);
            _timerQuote = new Timer(UpdateQuotes, null, _updateIntervalQuote, _updateIntervalQuote);
            _timerTick = new Timer(UpdateTicks, null, _updateIntervalTick, _updateIntervalTick);
        }

        private static void UpdateTicks(object state)
        {
            try
            {
                foreach (var pair in TickToday)
                {
                    //var count = TickToday[pair.Key].Count;
                    //var first = TickToday[pair.Key].First();
                    //var last = TickToday[pair.Key].Last();

                    var id = pair.Key;
                    //var tickDTOs = pair.Value;

                    var prodDef = ProdDefs.FirstOrDefault(o => o.Id == id);
                    if (prodDef == null ||
                        prodDef.QuoteType == enmQuoteType.Closed || prodDef.QuoteType == enmQuoteType.Inactive) //not opening
                        continue;

                    var quote = Quotes.FirstOrDefault(o => o.Id == id);
                    if (quote == null)
                        continue;

                    var newTick = new TickDTO()
                    {
                        p = CFD_COMMON.Utils.Quotes.GetLastPrice(quote),
                        time = quote.Time
                    };

                    if (pair.Value.Count == 0)
                    {
                        pair.Value.Add(newTick);
                        continue;
                    }

                    var lastInList = pair.Value.Last();
                    if (lastInList.time >= newTick.time)
                        continue;

                    if (CFD_COMMON.Utils.DateTimes.IsEqualDownToMinute(lastInList.time, newTick.time))
                        pair.Value[pair.Value.Count - 1] = newTick; //update
                    else
                    {
                        pair.Value.Add(newTick); //append
                    }

                    //delete old (before xxx hours ago)
                    var dtLast = pair.Value.Last().time;
                    var dtFirst = pair.Value.First().time;
                    if (dtLast - dtFirst > TimeSpan.FromHours(12))
                    {
                        TickToday[pair.Key] = pair.Value.Where(o => dtLast - o.time <= TimeSpan.FromHours(12)).ToList();
                    }

                    //var count2 = TickToday[pair.Key].Count;
                    //var first2 = TickToday[pair.Key].First();
                    //var last2 = TickToday[pair.Key].Last();
                    //CFDGlobal.LogLine(count + " => " + count2 + "   " + first.time + " ~ " + last.time + "  =>  " + first2.time + " ~ " + last2.time);
                }
            }
            catch(Exception e)
            {
                CFDGlobal.LogExceptionAsInfo(e);
            }
        }

        private static void UpdateProdDefs(object state)
        {
            //CFDGlobal.LogLine("Updating WebCache ProdDefs...");

            try
            {
                ProdDefs = CFDGlobal.BasicRedisClientManager.GetClient().As<ProdDef>().GetAll();
            }
            catch (Exception e)
            {
                CFDGlobal.LogExceptionAsInfo(e);
            }
        }

        private static void UpdateQuotes(object state)
        {
            //CFDGlobal.LogLine("Updating WebCache Quotes...");

            try
            {
                Quotes = CFDGlobal.BasicRedisClientManager.GetClient().As<Quote>().GetAll();
            }
            catch (Exception e)
            {
                CFDGlobal.LogExceptionAsInfo(e);
            }
        }
    }
}