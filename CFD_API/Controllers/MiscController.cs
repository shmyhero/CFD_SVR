﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using CFD_API.Controllers.Attributes;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
using ServiceStack.Redis;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/misc")]
    public class MiscController : CFDController
    {
        public MiscController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [HttpGet]
        [ActionName("version")]
        public HttpResponseMessage GetVersion()
        {
            //ApiGlobal.LogLine("");
            string dbName = db.Database.Connection.Database;

            return Request.CreateResponse(
                HttpStatusCode.OK,
#if DEBUG
                "TH API STATUS: OK [build=DEBUG]" +
#else
                "TH API STATUS: OK [build=RELEASE]" +
#endif
                    " -- v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()
                + " -- DB=[" + dbName + "]"
                //+" -- top-table cabling: brought to you by The A-Team."
                );
        }

        [HttpGet]
        [Route("redis")]
        public HttpResponseMessage RedisTest()
        {
            string value;
            using (var redisClient = CFDGlobal.PooledRedisClientsManager.GetClient())
            {
                value = redisClient.GetValue("anykey");
                return Request.CreateResponse(HttpStatusCode.OK, "dbsize " + redisClient.DbSize);
            }
        }

        [HttpGet]
        [Route("live/redis")]
        public HttpResponseMessage RedisLiveTest()
        {
            //string value;
            IList<ProdDef> prodDefs;
            IList<Quote> quotes;
            using (var redisClient = CFDGlobal.PooledRedisClientsManager_Live.GetClient())
            {
                prodDefs = redisClient.As<ProdDef>().GetAll();
                quotes = redisClient.As<Quote>().GetAll();
            }

            var now = DateTime.UtcNow;
            var h = now.Hour;
            var m = now.Minute;
            var dayOfWeek = now.DayOfWeek;

            if (dayOfWeek == DayOfWeek.Saturday)
                return Request.CreateResponse(HttpStatusCode.OK, "ok");

            //Commodities 21:00~22:00 close
            //Currencies 21:00~21:05 close
            //indices 21:00~22:00 close
            //US Stocks 13:30~19:59 open

            if (
                (dayOfWeek == DayOfWeek.Friday && (h < 20 || (h == 20 && m < 59)))
                ||
                (dayOfWeek == DayOfWeek.Sunday && ((h == 22 && m > 0) || h > 22))
                ||
                ((h < 20 || (h == 20 && m < 59)) && ((h == 22 && m > 0) || h > 22))
                )
            {
                var commodities = prodDefs.Where(o => o.QuoteType != enmQuoteType.Inactive && o.AssetClass == CFDGlobal.ASSET_CLASS_COMMODITY).ToList();
                var openCount = commodities.Count(o => o.QuoteType != enmQuoteType.Closed);
                var ratioOpen = (double)openCount/commodities.Count;
                if (ratioOpen < 0.9)
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "商品开市率小于90%");
            }

            if (
                (dayOfWeek == DayOfWeek.Friday && (h < 20 || (h == 20 && m < 59)))
                ||
                (dayOfWeek == DayOfWeek.Sunday && ((h == 21 && m > 5) || h > 21))
                ||
                ((h < 20 || (h == 20 && m < 59)) && ((h == 21 && m > 5) || h > 21))
                )
            {
                var currencies = prodDefs.Where(o => o.QuoteType != enmQuoteType.Inactive && o.AssetClass == CFDGlobal.ASSET_CLASS_FX && !o.Name.EndsWith(" Outright")).ToList();
                var openCount = currencies.Count(o => o.QuoteType != enmQuoteType.Closed);
                var ratioOpen = (double)openCount / currencies.Count;
                if (ratioOpen < 0.9)
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "外汇开市率小于90%");
            }

            if (
                (dayOfWeek == DayOfWeek.Friday && (h < 20 || (h == 20 && m < 59)))
                ||
                (dayOfWeek == DayOfWeek.Sunday && ((h == 22 && m > 0) || h > 22))
                ||
                ((h < 20 || (h == 20 && m < 59)) && ((h == 22 && m > 0) || h > 22))
                )
            {
                var indices = prodDefs.Where(o => o.QuoteType != enmQuoteType.Inactive && o.AssetClass == CFDGlobal.ASSET_CLASS_INDEX).ToList();
                var openCount = indices.Count(o => o.QuoteType != enmQuoteType.Closed);
                var ratioOpen = (double)openCount / indices.Count;
                if (ratioOpen < 0.9)
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "指数开市率小于90%");
            }

            if (
                dayOfWeek!= DayOfWeek.Sunday
                &&
                ((h > 13 || (h == 13 && m > 30)) && ((h == 19 && m < 58) || h < 19))
                )
            {
                var usStocks =prodDefs.Where(o => o.QuoteType != enmQuoteType.Inactive && o.AssetClass == CFDGlobal.ASSET_CLASS_STOCK && Products.IsUSStocks(o.Symbol)).ToList();
                var openCount = usStocks.Count(o => o.QuoteType != enmQuoteType.Closed);
                var ratioOpen = (double) openCount/ usStocks.Count;
                if (ratioOpen < 0.9)
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "美股开市率小于90%");
            }

            if (
                (dayOfWeek == DayOfWeek.Friday && (h < 20 || (h == 20 && m < 59)))
                ||
                (dayOfWeek == DayOfWeek.Sunday && ((h == 21 && m > 5) || h > 21))
                ||
                ((h < 20 || (h == 20 && m < 59)) && ((h == 21 && m > 5) || h > 21))
                )
            {
                var latestQuoteTime = quotes.Max(o => o.Time);
                if (now-latestQuoteTime>TimeSpan.FromMinutes(1))
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "超过1分钟未收到任何quote");
            }


            return Request.CreateResponse(HttpStatusCode.OK, "ok");
        }

        [HttpGet]
        [Route("live/redis/write")]
        public HttpResponseMessage RedisLiveWriteTest()
        {
            bool setValue;
            using (var redisClient = CFDGlobal.PooledRedisClientsManager_Live.GetClient())
            {
                setValue = redisClient.Set("anykey", DateTime.UtcNow.ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND));
                return Request.CreateResponse(HttpStatusCode.OK, "setValue result: " + setValue);
            }
        }

        [HttpGet]
        [ActionName("err")]
        public HttpResponseMessage TestErr()
        {
            //ApiGlobal.LogLine("about to throw test exception...");
            string s = null;
            string s2 = s.ToString();
            return Request.CreateResponse(HttpStatusCode.OK, s2);
        }

        [HttpGet]
        [ActionName("wcf")]
        public HttpResponseMessage TestWCF()
        {
            string result;
            using (var ayondoTradeClient = new AyondoTradeClient())
            {
                result = ayondoTradeClient.TestSleep(TimeSpan.FromSeconds(3));
            }
            return Request.CreateResponse(HttpStatusCode.OK, result);
        }

        [HttpGet]
        [ActionName("sleep")]
        public HttpResponseMessage TestSleep(int second = 5)
        {
            Thread.Sleep(TimeSpan.FromSeconds(second));
            return Request.CreateResponse(HttpStatusCode.OK, "");
        }

        [HttpGet]
        [ActionName("https")]
        [RequireHttps]
        public HttpResponseMessage TestHttps()
        {
            return Request.CreateResponse(HttpStatusCode.OK, "url scheme: " + Request.RequestUri.Scheme);
        }

        [HttpGet]
        [ActionName("log")]
        public HttpResponseMessage TestLog()
        {
            Trace.TraceInformation("this is a info trace");
            Trace.TraceWarning("this is a warning trace");
            Trace.TraceError("this is a error trace");

            Trace.WriteLine("this is a trace writeline");

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [HttpGet]
        [ActionName("fixTrade")]
        public bool GetFixTradeStatus()
        {
           var client=new AyondoTradeClient();
            return client.IsFixLoggingIn();
        }
    }
}