using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.Caching;
using CFD_API.DTO;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
using ServiceStack.Redis;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/quote")]
    public class QuoteController : CFDController
    {
        public QuoteController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
            : base(db, mapper, redisClient)
        {
        }

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
        public List<TickDTO> Get10MinutesTicks(int securityId)
        {
            var rawTickDTOs = WebCache.GetOrCreateTickRaw(securityId, RedisClient);

            var lastTickTime = rawTickDTOs.Last().time;

            var prodDef = WebCache.ProdDefs.FirstOrDefault(o => o.Id == securityId);
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

            var prodDef = WebCache.ProdDefs.FirstOrDefault(o => o.Id == securityId);
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
        public List<TickDTO> Get2HoursTicks(int securityId)
        {
            var tickToday = WebCache.GetOrCreateTickToday(securityId, RedisClient);

            var lastTickTime = tickToday.Last().time;
            return tickToday.Where(o => lastTickTime - o.time <= TimeSpan.FromHours(2)).ToList();
        }

        [HttpGet]
        [Route("{securityId}/tick/today")]
        public List<TickDTO> GetTodayTicks(int securityId)
        {
            var result = WebCache.GetOrCreateTickToday(securityId, RedisClient);
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
        public List<TickDTO> GetWeekTicks(int securityId)
        {
            List<TickDTO> result;

            //get from WebCache
            var tryGetValue = WebCache.TickWeek.TryGetValue(securityId, out result);
            if (tryGetValue)
                return result;

            //get from Redis
            var redisTickClient = RedisClient.As<Tick>();
            var ticks = redisTickClient.Lists["tick10m:" + securityId].GetAll();

            if (ticks.Count == 0)
                result = new List<TickDTO>();
            else
            {
                var lastTickTime = ticks.Last().Time;

                var ticksWeek = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromDays(7));

                result = ticksWeek.Select(o => Mapper.Map<TickDTO>(o)).ToList();
            }

            WebCache.TickWeek.AddOrUpdate(securityId, result, ((i, dtos) => dtos));

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
        public List<TickDTO> GetMonthTicks(int securityId)
        {
            List<TickDTO> result;

            //get from WebCache
            var tryGetValue = WebCache.TickMonth.TryGetValue(securityId, out result);
            if (tryGetValue)
                return result;

            //get from Redis
            var redisTickClient = RedisClient.As<Tick>();
            var ticks = redisTickClient.Lists["tick1h:" + securityId].GetAll();

            if (ticks.Count == 0)
                result = new List<TickDTO>();
            else
            {
                var lastTickTime = ticks.Last().Time;

                var ticksMonth = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromDays(30));

                result = ticksMonth.Select(o => Mapper.Map<TickDTO>(o)).ToList();
            }

            WebCache.TickMonth.AddOrUpdate(securityId, result, ((i, dtos) => dtos));

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

        /// <summary>
        /// for test use only
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("latest")]
        public List<QuoteTemp> GetLatestQuotes()
        {
            var redisQuoteClient = RedisClient.As<Quote>();
            var quotes = redisQuoteClient.GetAll().OrderByDescending(o => o.Time).ToList();

            var redisProdDefClient = RedisClient.As<ProdDef>();
            var prodDefs = redisProdDefClient.GetAll();

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

        public class QuoteTemp : Quote
        {
            public string Name { get; set; }
            public string Symbol { get; set; }
            public string AssetClass { get; set; }
            public string Financing { get; set; }
        }
    }
}