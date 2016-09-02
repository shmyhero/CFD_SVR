using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using CFD_API.Controllers.Attributes;
using CFD_COMMON.Models.Context;
using ServiceStack.Redis;

namespace CFD_API.Controllers
{
    public class MiscController : CFDController
    {
        public MiscController(CFDEntities db, IMapper mapper, IRedisClient redisClient) : base(db, mapper, redisClient)
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
        [ActionName("redis")]
        public HttpResponseMessage RedisTest()
        {
            var value = RedisClient.GetValue("anykey");
            return Request.CreateResponse(HttpStatusCode.OK, "dbsize " + RedisClient.DbSize);
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
    }
}