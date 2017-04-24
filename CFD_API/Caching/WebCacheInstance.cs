using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
using ServiceStack.Redis;
using CFD_COMMON.Models.Entities;

namespace CFD_API.Caching
{
    public class WebCacheInstance
    {
        private  Timer _timerProdDef;
        private  Timer _timerQuote;
        private  Timer _timerTick;
        private  Timer _timerTickRaw;
        //private  Timer _timerPriceDown;
        private  Timer _timerProdSetting;

        private static TimeSpan _updateIntervalProdDef = TimeSpan.FromSeconds(3);
        private static TimeSpan _updateIntervalQuote = TimeSpan.FromMilliseconds(500);
        private static TimeSpan _updateIntervalTick = TimeSpan.FromSeconds(10);
        private static TimeSpan _updateIntervalTickRaw = TimeSpan.FromMilliseconds(1000);
        private  IMapper mapper;
        private  IRedisClientsManager _redisClientsManager;

        public WebCacheInstance(bool isLive = false)
        {
            //initialize
            ProdDefs = new List<ProdDef>();
            Quotes = new List<Quote>();
            TickRaw = new ConcurrentDictionary<int, List<TickDTO>>();
            TickToday = new ConcurrentDictionary<int, List<TickDTO>>();
            TickWeek = new ConcurrentDictionary<int, List<TickDTO>>();
            TickMonth = new ConcurrentDictionary<int, List<TickDTO>>();
            //PriceDownInterval = new Dictionary<int, int>();
            ProdSettingList = new List<ProdSetting>();

            mapper = MapperConfig.GetAutoMapperConfiguration().CreateMapper();

            _redisClientsManager = CFDGlobal.GetDefaultPooledRedisClientsManager(isLive);

            //get value from Redis
            using (var redisClient = _redisClientsManager.GetClient())
            {
                try
                {
                    ProdDefs = redisClient.As<ProdDef>().GetAll();
                    Quotes = redisClient.As<Quote>().GetAll();
                }
                catch (Exception e)
                {
                    CFDGlobal.LogExceptionAsInfo(e);
                }
            }
            
            //set timer
            _timerProdDef = new Timer(UpdateProdDefs, null, _updateIntervalProdDef, TimeSpan.FromMilliseconds(-1));
            _timerQuote = new Timer(UpdateQuotes, null, _updateIntervalQuote, TimeSpan.FromMilliseconds(-1));
            _timerTick = new Timer(UpdateTicks, null, _updateIntervalTick, TimeSpan.FromMilliseconds(-1));
            _timerTickRaw = new Timer(UpdateRawTicks, null, _updateIntervalTickRaw, TimeSpan.FromMilliseconds(-1));
            //_timerPriceDown = new Timer(UpdatePriceDownInterval, null,0, 3 * 60 * 1000);
            _timerProdSetting = new Timer(UpdateProdSetting, null, 0, 3 * 60 * 1000);
        }

        public  IList<ProdDef> ProdDefs { get; private set; }
        public  IList<Quote> Quotes { get; private set; }
        public  ConcurrentDictionary<int, List<TickDTO>> TickRaw { get; private set; }
        public  ConcurrentDictionary<int, List<TickDTO>> TickToday { get; private set; }
        public  ConcurrentDictionary<int, List<TickDTO>> TickWeek { get; private set; }
        public  ConcurrentDictionary<int, List<TickDTO>> TickMonth { get; private set; }
        ///// <summary>
        ///// 价格中断的最大可接受时间
        ///// </summary>
        //public  Dictionary<int, int> PriceDownInterval { get; private set; }

        /// <summary>
        /// 包含了最小投资本金、价格中断的最大可接受时间
        /// </summary>
        public List<ProdSetting> ProdSettingList { get; private set; }

        private void UpdateRawTicks(object state)
        {
            var tsTickListLengthCleanWhen = TimeSpan.FromMinutes(60);
            var tsTickListLengthCleanTo = TimeSpan.FromMinutes(30);

            while (true)
            {
                //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                //the logic here must be identical as CFD_JOBS.Ayondo.AyondoFixFeedWorker
                //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                try
                {
                    foreach (var pair in TickRaw)
                    {
                        var id = pair.Key;

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

                        //always append
                        pair.Value.Add(newTick);

                        //delete old (before xxx hours ago)
                        var dtLast = pair.Value.Last().time;
                        var dtFirst = pair.Value.First().time;
                        if (dtLast - dtFirst > tsTickListLengthCleanWhen)
                        {
                            TickRaw[pair.Key] = pair.Value.Where(o => dtLast - o.time <= tsTickListLengthCleanTo).ToList();
                        }
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogExceptionAsInfo(e);
                }

                Thread.Sleep(_updateIntervalTickRaw);
            }
        }

        private void UpdateTicks(object state)
        {
            while (true)
            {
                //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                //the logic here must be identical as CFD_JOBS.Ayondo.TickChartWorker
                //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

                UpdateTicksByConditions(TickToday, TickSize.OneMinute, TimeSpan.FromHours(12));
                UpdateTicksByConditions(TickWeek, TickSize.TenMinute, TimeSpan.FromDays(7));
                UpdateTicksByConditions(TickMonth, TickSize.OneHour, TimeSpan.FromDays(30));

                Thread.Sleep(_updateIntervalTick);
            }
        }

        private void UpdateTicksByConditions(ConcurrentDictionary<int, List<TickDTO>> dicTicks, TickSize tickSize, TimeSpan tsTickListLength)
        {
            try
            {
                foreach (var pair in dicTicks)
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
                        //time = quote.Time
                        time = Quotes.Max(o => o.Time) //为了在价格不变的时候补点
                    };

                    if (pair.Value.Count == 0)
                    {
                        pair.Value.Add(newTick);
                        continue;
                    }

                    var lastInList = pair.Value.Last();
                    if (lastInList.time >= newTick.time)
                        continue;

                    if (Ticks.IsTickEqual(lastInList.time, newTick.time, tickSize))
                        pair.Value[pair.Value.Count - 1] = newTick; //update
                    else
                    {
                        pair.Value.Add(newTick); //append
                    }

                    //delete old (before xxx hours ago)
                    var dtLast = pair.Value.Last().time;
                    var dtFirst = pair.Value.First().time;
                    if (dtLast - dtFirst > tsTickListLength)
                    {
                        dicTicks[pair.Key] = pair.Value.Where(o => dtLast - o.time <= tsTickListLength).ToList();
                    }

                    //var count2 = TickToday[pair.Key].Count;
                    //var first2 = TickToday[pair.Key].First();
                    //var last2 = TickToday[pair.Key].Last();
                    //CFDGlobal.LogLine(count + " => " + count2 + "   " + first.time + " ~ " + last.time + "  =>  " + first2.time + " ~ " + last2.time);
                }
            }
            catch (Exception e)
            {
                CFDGlobal.LogExceptionAsInfo(e);
            }
        }

        private void UpdateProdDefs(object state)
        {
            while (true)
            {
                //CFDGlobal.LogLine("Updating WebCache ProdDefs...");
                using (var redisClient = _redisClientsManager.GetClient())
                {
                    try
                    {
                        ProdDefs = redisClient.As<ProdDef>().GetAll();
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogExceptionAsInfo(e);
                    }
                }

                Thread.Sleep(_updateIntervalProdDef);
            }
        }

        private void UpdateQuotes(object state)
        {
            while (true)
            {
                //CFDGlobal.LogLine("Updating WebCache Quotes...");
                using (var redisClient = _redisClientsManager.GetClient())
                {
                    try
                    {
                        Quotes = redisClient.As<Quote>().GetAll();
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogExceptionAsInfo(e);
                    }
                }

                Thread.Sleep(_updateIntervalQuote);
            }
        }

        //private void UpdatePriceDownInterval(object state)
        //{
        //    using (var db = CFDEntities.Create())
        //    {
        //        PriceDownInterval.Clear();
        //        db.PriceDownIntervals.ToList().ForEach(p => {
        //                                                        if (!PriceDownInterval.ContainsKey(p.SecurityID))
        //                                                        {
        //                                                            PriceDownInterval.Add(p.SecurityID, p.DownInterval);
        //                                                        }
        //        });
        //    }


        //}

        private void UpdateProdSetting(object state)
        {
            try
            {
                using (var db = CFDEntities.Create())
                {
                    ProdSettingList.Clear();
                    ProdSettingList.AddRange(db.ProdSettings.ToList());
                }
            }
            catch (Exception e)
            {
                CFDGlobal.LogExceptionAsInfo(e);
            }
        }

        public List<TickDTO> GetOrCreateTickRaw(int secId)
        {
            List<TickDTO> rawTickDTOs;

            //get from WebCache
            var tryGetValue = TickRaw.TryGetValue(secId, out rawTickDTOs);
            if (tryGetValue)
                return rawTickDTOs;

            //get from Redis
            List<Tick> ticks;
            using (var redisClient = _redisClientsManager.GetClient())
            {
                var redisTickClient = redisClient.As<Tick>();
                ticks = redisTickClient.Lists[Ticks.GetTickListNamePrefix(TickSize.Raw) + secId].GetAll();
            }

            if (ticks.Count == 0)
                rawTickDTOs = new List<TickDTO>();
            else
            {
                var lastTickTime = ticks.Last().Time;

                rawTickDTOs = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromMinutes(30)).Select(o => mapper.Map<TickDTO>(o)).ToList();
            }

            TickRaw.AddOrUpdate(secId, rawTickDTOs, (key, value) => rawTickDTOs);

            return rawTickDTOs;
        }

        public List<TickDTO> GetOrCreateTickToday(int secId)
        {
            List<TickDTO> tickDTOs;

            //get from WebCache
            var tryGetValue = TickToday.TryGetValue(secId, out tickDTOs);
            if (tryGetValue)
                return tickDTOs;

            //get from Redis
            List<Tick> ticks;
            using (var redisClient = _redisClientsManager.GetClient())
            {
                var redisTickClient = redisClient.As<Tick>();
                ticks = redisTickClient.Lists[Ticks.GetTickListNamePrefix(TickSize.OneMinute) + secId].GetAll();
            }

            if (ticks.Count == 0)
                tickDTOs = new List<TickDTO>();
            else
            {
                var lastTickTime = ticks.Last().Time;

                tickDTOs = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromHours(12)).Select(o => mapper.Map<TickDTO>(o)).ToList();
            }

            TickToday.AddOrUpdate(secId, tickDTOs, (key, value) => tickDTOs);

            return tickDTOs;
        }
    }
}