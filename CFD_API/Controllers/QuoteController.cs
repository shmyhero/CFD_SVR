using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.Caching;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
using ServiceStack.Redis;
using ServiceStack.Redis.Generic;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/quote")]
    public class QuoteController : CFDController
    {
        //public QuoteController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
        //    : base(db, mapper, redisClient)
        //{
        //}
        public QuoteController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }

        #region 折线图
        //todo:for test only
        [HttpGet]
        [Route("{securityId}/tick")]
        public List<TickDTO> GetTicks(int securityId)
        {
            var redisTypedClient = RedisClient.As<Tick>();
            var ticks = redisTypedClient.Lists[Ticks.GetTickListNamePrefix(TickSize.OneMinute) + securityId].GetAll();

            //ticks = ticks.Where(o => DateTime.UtcNow - o.Time < TimeSpan.FromDays(1)).ToList();
            //foreach (var tick in ticks)
            //{
            //    //remove seconds and millionseconds
            //    tick.Time = new DateTime(tick.Time.Year, tick.Time.Month, tick.Time.Day, tick.Time.Hour, tick.Time.Minute, 0, DateTimeKind.Utc);
            //}

            return ticks.Select(o => Mapper.Map<TickDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("{securityId}/tick/10m")]
        [Route("live/{securityId}/tick/10m")]
        public List<TickDTO> Get10MinutesTicks(int securityId)
        {
            var cache = WebCache.GetInstance(IsLiveUrl);

            var rawTickDTOs = cache.GetOrCreateTickRaw(securityId);

            if (rawTickDTOs.Count == 0)
                return new List<TickDTO>();

            var lastTickTime = rawTickDTOs.Last().time;

            var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == securityId);
            if (prodDef != null && prodDef.QuoteType != enmQuoteType.Closed && prodDef.QuoteType != enmQuoteType.Inactive //is opening
                && lastTickTime - prodDef.LastOpen < TimeSpan.FromMinutes(10)) // is opened during the last 10 minutes of the ticks data
            {
                //return data since lastOpen
                return rawTickDTOs.Where(o => o.time >= prodDef.LastOpen).ToList();
            }

            return rawTickDTOs.Where(o => lastTickTime - o.time <= TimeSpan.FromMinutes(10)).ToList();
        }

        [HttpGet]
        [Route("{securityId}/tick/10m2")]
        public List<TickDTO> Get10MinutesTicks2(int securityId)
        {
            var redisTickClient = RedisClient.As<Tick>();

            var ticks = redisTickClient.Lists[Ticks.GetTickListNamePrefix(TickSize.Raw) + securityId].GetAll();

            if (ticks.Count == 0)
                return new List<TickDTO>();

            var lastTickTime = ticks.Last().Time;

            var prodDef = WebCache.Demo.ProdDefs.FirstOrDefault(o => o.Id == securityId);
            if (prodDef != null && prodDef.QuoteType != enmQuoteType.Closed && prodDef.QuoteType != enmQuoteType.Inactive //is opening
                && lastTickTime - prodDef.LastOpen < TimeSpan.FromMinutes(10)) // is opened during the last 10 minutes of the ticks data
            {
                //return data since lastOpen
                return ticks.Where(o => o.Time >= prodDef.LastOpen).Select(o => Mapper.Map<TickDTO>(o)).ToList();
            }

            return ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromMinutes(10)).Select(o => Mapper.Map<TickDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("{securityId}/tick/2h")]
        [Route("live/{securityId}/tick/2h")]
        public List<TickDTO> Get2HoursTicks(int securityId)
        {
            var cache = WebCache.GetInstance(IsLiveUrl);

            var tickToday = cache.GetOrCreateTickToday(securityId);

            if (tickToday.Count == 0)
                return new List<TickDTO>();

            var lastTickTime = tickToday.Last().time;
            return tickToday.Where(o => lastTickTime - o.time <= TimeSpan.FromHours(2)).ToList();
        }

        [HttpGet]
        [Route("{securityId}/tick/today")]
        [Route("live/{securityId}/tick/today")]
        public List<TickDTO> GetTodayTicks(int securityId)
        {
            var cache = WebCache.GetInstance(IsLiveUrl);

            var result = cache.GetOrCreateTickToday(securityId);
            return result;
        }

        /// <summary>
        /// get data directly from redis (no WebCache)
        /// </summary>
        /// <param name="securityId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{securityId}/tick/today2")]
        public List<TickDTO> GetTodayTicks2(int securityId)
        {
            var redisTickClient = RedisClient.As<Tick>();

            var ticks = redisTickClient.Lists[Ticks.GetTickListNamePrefix(TickSize.OneMinute) + securityId].GetAll();

            if (ticks.Count == 0)
                return new List<TickDTO>();

            var lastTickTime = ticks.Last().Time;

            var result = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromHours(12));

            return result.Select(o => Mapper.Map<TickDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("{securityId}/tick/week")]
        [Route("live/{securityId}/tick/week")]
        public List<TickDTO> GetWeekTicks(int securityId)
        {
            List<TickDTO> result;

            var cache = WebCache.GetInstance(IsLiveUrl);

            //get from WebCache
            var tryGetValue = cache.TickWeek.TryGetValue(securityId, out result);
            if (tryGetValue)
                return result;

            //get from Redis
            List<Tick> ticks;
            using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(IsLiveUrl).GetClient())
            {
                var redisTickClient = redisClient.As<Tick>();
                ticks = redisTickClient.Lists["tick10m:" + securityId].GetAll();
            }

            if (ticks.Count == 0)
                result = new List<TickDTO>();
            else
            {
                var lastTickTime = ticks.Last().Time;

                var ticksWeek = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromDays(7));

                result = ticksWeek.Select(o => Mapper.Map<TickDTO>(o)).ToList();
            }

            cache.TickWeek.AddOrUpdate(securityId, result, (k, v) => result);

            return result;
        }

        /// <summary>
        /// obsolete
        /// </summary>
        /// <param name="securityId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{securityId}/tick/week2")]
        public List<TickDTO> GetWeekTicks2(int securityId)
        {
            var redisTickClient = RedisClient.As<Tick>();

            var ticks = redisTickClient.Lists["tick10m:" + securityId].GetAll();

            if (ticks.Count == 0)
                return new List<TickDTO>();

            var lastTickTime = ticks.Last().Time;

            var result = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromDays(7));

            //var result = new List<Tick>();
            //Tick lastAdded = null;
            //foreach (var tick in allTicks)
            //{
            //    if (lastAdded != null &&
            //        (lastAdded.Time.Year == tick.Time.Year && lastAdded.Time.Month == tick.Time.Month && lastAdded.Time.Day == tick.Time.Day &&
            //         lastAdded.Time.Hour == tick.Time.Hour && lastAdded.Time.Minute/10 == tick.Time.Minute/10))
            //        continue;

            //    result.Add(tick);
            //    lastAdded = tick;
            //}

            return result.Select(o => Mapper.Map<TickDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("{securityId}/tick/month")]
        [Route("live/{securityId}/tick/month")]
        public List<TickDTO> GetMonthTicks(int securityId)
        {
            List<TickDTO> result;

            var cache = WebCache.GetInstance(IsLiveUrl);

            //get from WebCache
            var tryGetValue = cache.TickMonth.TryGetValue(securityId, out result);
            if (tryGetValue)
                return result;

            //get from Redis
            List<Tick> ticks;
            using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(IsLiveUrl).GetClient())
            {
                var redisTickClient = redisClient.As<Tick>();
                ticks = redisTickClient.Lists["tick1h:" + securityId].GetAll();
            }

            if (ticks.Count == 0)
                result = new List<TickDTO>();
            else
            {
                var lastTickTime = ticks.Last().Time;

                var ticksMonth = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromDays(30));

                result = ticksMonth.Select(o => Mapper.Map<TickDTO>(o)).ToList();
            }

            cache.TickMonth.AddOrUpdate(securityId, result, (k, v) => result);

            return result;
        }

        /// <summary>
        /// obsolete
        /// </summary>
        /// <param name="securityId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{securityId}/tick/month2")]
        public List<TickDTO> GetMonthTicks2(int securityId)
        {
            var redisTickClient = RedisClient.As<Tick>();

            var ticks = redisTickClient.Lists["tick1h:" + securityId].GetAll();

            if (ticks.Count == 0)
                return new List<TickDTO>();

            var lastTickTime = ticks.Last().Time;

            var result = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromDays(30));

            return result.Select(o => Mapper.Map<TickDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("{securityId}/tick/3month")]
        [Route("live/{securityId}/tick/3month")]
        public List<TickDTO> Get3MonthTicks(int securityId)
        {
            List<TickDTO> result;
            //get from Redis
            List<Tick> ticks;
            using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(IsLiveUrl).GetClient())
            {
                var redisTickClient = redisClient.As<Tick>();
                ticks = redisTickClient.Lists["tick1h:" + securityId].GetAll();
            }

            if (ticks.Count == 0)
                result = new List<TickDTO>();
            else
            {
                var lastTickTime = ticks.Last().Time;

                var ticksMonth = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromDays(90));

                result = ticksMonth.Select(o => Mapper.Map<TickDTO>(o)).ToList();
            }

            //用1小时的Tick数据，每3小时取一个值
            List<TickDTO> result3Month = new List<TickDTO>();
            for (int count = 0; count < result.Count; count++)
            {
                if(count % 3 ==0)
                {
                    result3Month.Add(new TickDTO() { p = result[count].p, time = result[count].time });
                }
            }

            return result3Month;
        }

        [HttpGet]
        [Route("{securityId}/tick/6month")]
        [Route("live/{securityId}/tick/6month")]
        public List<TickDTO> Get6MonthTicks(int securityId)
        {
            List<TickDTO> result;
            //get from Redis
            List<Tick> ticks;
            using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(IsLiveUrl).GetClient())
            {
                var redisTickClient = redisClient.As<Tick>();
                ticks = redisTickClient.Lists["tick1h:" + securityId].GetAll();
            }

            if (ticks.Count == 0)
                result = new List<TickDTO>();
            else
            {
                var lastTickTime = ticks.Last().Time;

                var ticksMonth = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromDays(180));

                result = ticksMonth.Select(o => Mapper.Map<TickDTO>(o)).ToList();
            }

            //用1小时的Tick数据，每6小时取一个值
            List<TickDTO> result6Month = new List<TickDTO>();
            for (int count = 0; count < result.Count; count++)
            {
                if (count % 6 == 0)
                {
                    result6Month.Add(new TickDTO() { p = result[count].p, time = result[count].time });
                }
            }

            return result6Month;
        }
        #endregion

        #region K线图
        [HttpGet]
        [Route("{securityId}/kline/1m")]
        [Route("live/{securityId}/kline/1m")]
        public List<KLineDTO> Get1mKLine(int securityId)
        {
            return GetKLines(KLineSize.OneMinute, securityId, TimeSpan.FromHours(4));
        }

        [HttpGet]
        [Route("{securityId}/kline/5m")]
        [Route("live/{securityId}/kline/5m")]
        public List<KLineDTO> Get5mKLineHorizontal(int securityId)
        {
            return GetKLines(KLineSize.FiveMinutes, securityId, TimeSpan.FromHours(24));
        }

        [HttpGet]
        [Route("{securityId}/kline/15m")]
        [Route("live/{securityId}/kline/15m")]
        public List<KLineDTO> Get15mKLineHorizontal(int securityId)
        {
            return GetKLines(KLineSize.FifteenMinutes, securityId, TimeSpan.FromHours(3*24));
        }

        [HttpGet]
        [Route("{securityId}/kline/60m")]
        [Route("live/{securityId}/kline/60m")]
        public List<KLineDTO> Get60mKLineHorizontal(int securityId)
        {
            return GetKLines(KLineSize.SixtyMinutes, securityId, TimeSpan.FromHours(12 * 24));
        }
      
        [HttpGet]
        [Route("{securityId}/kline/day")]
        [Route("live/{securityId}/kline/day")]
        public List<KLineDTO> GetDayKLine(int securityId)
        {
            using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(IsLiveUrl).GetClient())
            {
                var redisKLineClient = redisClient.As<KLine>();
                //var redisProdDefClient = redisClient.As<ProdDef>();

                var klines = redisKLineClient.Lists[KLines.GetKLineListNamePrefix(KLineSize.Day) + securityId];

                if (klines.Count == 0)
                    return new List<KLineDTO>();

                //get 100 records at max
                var beginIndex = klines.Count - 100;
                var result = klines.GetRange(beginIndex < 0 ? 0 : beginIndex, klines.Count - 1);

                return result.Select(o => new KLineDTO()
                {
                    close = o.Close,
                    high = o.High,
                    low = o.Low,
                    open = o.Open,
                    time = o.Time
                }).OrderBy(o => o.time).ToList();
            }
        }
        #endregion

        /// <summary>
        /// todo: for test use only
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("latest")]
        [Route("live/latest")]
        public List<QuoteTemp> GetLatestQuotes()
        {
            List<Quote> quotes;
            IList<ProdDef> prodDefs;
            using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(IsLiveUrl).GetClient())
            {
                var redisQuoteClient = redisClient.As<Quote>();
                quotes = redisQuoteClient.GetAll().OrderByDescending(o => o.Time).ToList();

                var redisProdDefClient = redisClient.As<ProdDef>();
                prodDefs = redisProdDefClient.GetAll();
            }

            //var securities = db.AyondoSecurities.ToList();

            var results = quotes.Select(o =>
            {
                //var security = securities.FirstOrDefault(s => s.Id == o.Id);
                var prodDef = prodDefs.FirstOrDefault(s => s.Id == o.Id);

                if (prodDef == null)
                    return new QuoteTemp
                    {
                        Id = o.Id,
                        Bid = o.Bid,
                        Offer = o.Offer,
                        Time = o.Time
                    };

                return new QuoteTemp
                {
                    Id = o.Id,
                    Bid = o.Bid,
                    Offer = o.Offer,
                    Time = o.Time,
                    Name = prodDef.Name,
                    Symbol = prodDef.Symbol,
                    AssetClass = prodDef.AssetClass,
                    //Financing = security == null ? null : security.Financing
                };
            }).ToList();

            return results;
        }

        private List<KLineDTO> GetKLines(KLineSize klineSize, int securityId, TimeSpan timeSpan)
        {
            List<KLine> klines;
            using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(IsLiveUrl).GetClient())
            {
                var redisKLineClient = redisClient.As<KLine>();
                klines = redisKLineClient.Lists[KLines.GetKLineListNamePrefix(klineSize) + securityId].GetAll();
            }

            if (klines.Count == 0)
                return new List<KLineDTO>();

            var lastKLineTime = klines.Last().Time;

            var result = klines.Where(o => lastKLineTime - o.Time <= timeSpan);

            return result.Select(o => new KLineDTO()
            {
                close = o.Close,
                high = o.High,
                low = o.Low,
                open = o.Open,
                time = o.Time
            }).OrderBy(o => o.time).ToList();
        }

        public class QuoteTemp : Quote
        {
            public string Name { get; set; }
            public string Symbol { get; set; }
            public string AssetClass { get; set; }
            public string Financing { get; set; }
        }
    }
}