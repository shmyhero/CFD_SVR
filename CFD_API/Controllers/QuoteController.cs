using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_JOBS.Models;
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
        [Route("latest")]
        public List<Quote> GetLatestQuotes()
        {
            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();
            var redisTypedClient = basicRedisClientManager.GetClient().As<Quote>();
            var orderByDescending = redisTypedClient.GetAll().OrderByDescending(o => o.Time).ToList();
            return orderByDescending;
        }
    }
}
