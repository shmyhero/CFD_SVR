using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using ServiceStack.Redis;
using ServiceStack.Redis.Generic;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/quote")]
    public class QuoteController : CFDController
    {
        public QuoteController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }

        [HttpGet]
        [Route("{securityId}/tick")]
        public List<TickDTO> GetTicks(int securityId)
        {
            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();
            var redisTypedClient = basicRedisClientManager.GetClient().As<Tick>();
            var ticks = redisTypedClient.Lists["tick:" + securityId].GetAll();

            ticks = ticks.Where(o => DateTime.UtcNow - o.Time < TimeSpan.FromDays(1)).ToList();
            foreach (var tick in ticks)
            {
                //remove seconds and millionseconds
                tick.Time = new DateTime(tick.Time.Year, tick.Time.Month, tick.Time.Day, tick.Time.Hour, tick.Time.Minute, 0, DateTimeKind.Utc);
            }
            
            return ticks.Select(o=>Mapper.Map<TickDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("latest")]
        public List<QuoteTemp> GetLatestQuotes()
        {
            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();
            var redisTypedClient = basicRedisClientManager.GetClient().As<Quote>();
            var quotes = redisTypedClient.GetAll().OrderByDescending(o => o.Time).ToList();

            var securities = db.AyondoSecurities.ToList();

            var results = quotes.Select(o=>
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
