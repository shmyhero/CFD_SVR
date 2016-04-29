using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
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
            var ticks = redisTypedClient.Lists["tick:" + securityId].GetAll();

            //ticks = ticks.Where(o => DateTime.UtcNow - o.Time < TimeSpan.FromDays(1)).ToList();
            //foreach (var tick in ticks)
            //{
            //    //remove seconds and millionseconds
            //    tick.Time = new DateTime(tick.Time.Year, tick.Time.Month, tick.Time.Day, tick.Time.Hour, tick.Time.Minute, 0, DateTimeKind.Utc);
            //}

            return ticks.Select(o => Mapper.Map<TickDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("{securityId}/tick/today")]
        public List<TickDTO> GetTodayTicks(int securityId)
        {
            var redisTickClient = RedisClient.As<Tick>();

            var ticks = redisTickClient.Lists["tick:" + securityId].GetAll();

            var lastTickTime = ticks.Last().Time;

            var result = ticks.Where(o => lastTickTime - o.Time <= TimeSpan.FromHours(12));

            return result.Select(o => Mapper.Map<TickDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("{securityId}/tick/week")]
        public List<TickDTO> GetWeekTicks(int securityId)
        {
            var redisTickClient = RedisClient.As<Tick>();

            var ticks = redisTickClient.Lists["tick10m:" + securityId].GetAll();

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
            var redisTickClient = RedisClient.As<Tick>();

            var ticks = redisTickClient.Lists["tick1h:" + securityId].GetAll();

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
            var redisTypedClient = RedisClient.As<Quote>();
            var quotes = redisTypedClient.GetAll().OrderByDescending(o => o.Time).ToList();

            var securities = db.AyondoSecurities.ToList();

            var results = quotes.Select(o =>
            {
                var security = securities.FirstOrDefault(s => s.Id == o.Id);

                if (security == null)
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
                    Name = security.Name,
                    Symbol = security.Symbol,
                    AssetClass = security.AssetClass,
                    Financing = security.Financing
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