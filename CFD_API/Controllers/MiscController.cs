using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Transactions;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.SignalR;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
using Microsoft.WindowsAzure.ServiceRuntime;
using Newtonsoft.Json.Linq;
using ServiceStack.Redis;
using IsolationLevel = System.Data.IsolationLevel;

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
                if (ratioOpen < 0.8)
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "商品开市率小于80%");
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
                if (ratioOpen < 0.8)
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "外汇开市率小于80%");
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
                if (ratioOpen < 0.8)
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "指数开市率小于80%");
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
                if (ratioOpen < 0.8)
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, "美股开市率小于80%");
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
        [Route("fix")]
        [Route("live/fix")]
        public HttpResponseMessage GetFixStatus()
        {
            bool isFixLoggedIn;
            using (var client = new AyondoTradeClient(IsLiveUrl))
            {
                isFixLoggedIn = client.IsFixLoggingIn();
            }

            if (isFixLoggedIn)
                return Request.CreateResponse(HttpStatusCode.OK, "FIX is logged in.");
            else
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "FIX is NOT logged in.");
        }

        [HttpGet]
        [Route("websocket")]
        public JObject GetWebsocketInfo()
        {
            var o = new JObject();
            o["demo"] = QuoteFeedTicker.Instance.GetSubscriptionCount(false);
            o["live"] = QuoteFeedTicker.Instance.GetSubscriptionCount(true);
            return o;
        }

        [HttpGet]
        [Route("websocket/aggregate")]
        public JObject GetWebsocketInfoAggregate()
        {
            var arr = new JArray();
            var client = new WebClient();
            foreach (var r in RoleEnvironment.Roles)
            {
                foreach (var i in r.Value.Instances)
                {
                    var ip = i.InstanceEndpoints.FirstOrDefault().Value.IPEndpoint.Address;
                    var o = JObject.Parse(client.DownloadString("http://" + ip + "/api/misc/websocket"));
                    o["ip"] = ip.ToString();
                    o["id"] = i.Id;
                    arr.Add(o);
                }
            }

            var result = new JObject();
            var demoSum = arr.Sum(o => o["demo"].Value<int>());
            var liveSum = arr.Sum(o => o["live"].Value<int>());
            result["total"] = demoSum + liveSum;
            result["demo"] = demoSum;
            result["live"] = liveSum;
            result["instances"] = arr;
            return result;
        }

        [HttpGet]
        [Route("roleInfo")]
        public HttpResponseMessage GetWebRoleInfo()
        {
            //var subscriptionId = "<your azure subscription id>";
            //var managementCertDataFromPublishSettingsFile = "<management cert data from a publish settings file>";
            //var cert = new X509Certificate2(Convert.FromBase64String(managementCertDataFromPublishSettingsFile));
            //var credentials = new CertificateCloudCredentials(subscriptionId, cert);
            //var computeManagementClient = new ComputeManagementClient(credentials);
            //var cloudServiceName = "<your cloud service name>";
            //var cloudServiceDetails = computeManagementClient.HostedServices.GetDetailed(cloudServiceName);
            //var deployments = cloudServiceDetails.Deployments;
            //foreach (var deployment in deployments)
            //{
            //    Console.WriteLine("Deployment Slot: " + deployment.DeploymentSlot);
            //    Console.WriteLine("-----------------------------------------------");
            //    foreach (var instance in deployment.RoleInstances)
            //    {
            //        Console.WriteLine("Instance name: " + instance.InstanceName + "; IP Address: " + string.Join(", ", instance.PublicIPs.Select(c => c.Address.ToString())));
            //    }
            //}

            string str = "";
            foreach (var r in RoleEnvironment.Roles)
            {
                str += r.Value.Name + " " + r.Value.Instances.Count + "[";
                foreach (var i in r.Value.Instances)
                {
                    str += i.Id + " " + i.Role.Name + " " + i.InstanceEndpoints.Count + "(";
                    foreach (var e in i.InstanceEndpoints)
                    {
                        str += e.Value.IPEndpoint.Address + ":" + e.Value.IPEndpoint.Port +
                            (
                            e.Value.PublicIPEndpoint!=null
                            ?"-"+e.Value.PublicIPEndpoint.Address + ":" + e.Value.PublicIPEndpoint.Port
                            :""
                            )
                            + ", ";
                    }
                    str += "), ";
                }
                str += ']';
            }

            return Request.CreateResponse(HttpStatusCode.OK, str);
        }

        [HttpGet]
        [Route("dau")]
        [IPAuth]
        public List<UserDailyApprovedCountDTO> GetDAU()
        {
            List<UserDailyApprovedCountDTO> result;
            //var dtStart=new DateTime(2017,1,1,0,0,0,DateTimeKind.Local);

            using (var scope=new TransactionScope(TransactionScopeOption.Required,new TransactionOptions{IsolationLevel =System.Transactions.IsolationLevel.ReadUncommitted}))
            {
                using (var db2 = CFDHistoryEntities.Create())
                {
                    result = db2.ApiHits.AsNoTracking().Where(o => o.UserId != null //&& o.HitAt>= dtStart
                    )
                        .GroupBy(
                            o =>
                                new
                                {
                                    userId = o.UserId,
                                    date = DbFunctions.TruncateTime(DbFunctions.AddHours(o.HitAt.Value, 8).Value)
                                })
                        .GroupBy(o => o.Key.date)
                        .Select(o => new UserDailyApprovedCountDTO() { date = o.Key, count = o.Count() })
                        .OrderBy(o => o.date)
                        .ToList();
                }

                scope.Complete();
            }
            
            return result;
        }

        [HttpGet]
        [Route("userLocation")]
        [IPAuth]
        public List<IPLocationDTO> UserLocation()
        {
            //return new List<IPLocationDTO>() {new IPLocationDTO() {count=100,province= "Guangdong" }, new IPLocationDTO() { count = 100, province = "Guangdong Sheng" } };

            List<IPLocationDTO> result;

            var monthAgo = DateTime.UtcNow.AddHours(8).Date.AddDays(-6);

            using (var db2 = CFDHistoryEntities.Create())
            {
                db2.Database.CommandTimeout = 600;
                var ipStrCount = db2.ApiHits.AsNoTracking().Where(o => o.HitAt >= monthAgo)
                    .GroupBy(o => o.Ip)
                    .Select(o => new {ip = o.Key, count = o.Count()})
                    //.OrderByDescending(o => o.count)
                    .ToList();

                var ipByteCount = ipStrCount
                    .Select(o =>
                    {
                        var bytes = IPAddress.Parse(o.ip).GetAddressBytes();
                        Array.Reverse(bytes);
                        var ipInt = BitConverter.ToUInt32(bytes, 0);
                        return new
                        {
                            ip = ipInt,
                            count = o.count,
                        };
                    })
                    //.OrderByDescending(o => o.count)
                    .ToList();

                var ipDB =
                    db.IP2City.AsNoTracking() //.Where(o => o.CountryCode == "CN")
                        .Select(o => new {s = o.StartAddress, e = o.EndAddress, p = o.Province})
                        .ToList();
                var ipInt32DB = ipDB.Select(o =>
                {
                    var s = o.s;
                    var e = o.e;
                    Array.Reverse(s);
                    Array.Reverse(e);
                    return new {s = BitConverter.ToUInt32(s, 0), e = BitConverter.ToUInt32(e, 0), p = o.p};
                })
                    .ToList();

                result = ipByteCount.Select(o =>
                {
                    //var city =
                    //    cnCities.FirstOrDefault(
                    //        c => Bytes.IsFormerBiggerOrEqual(o.ip, c.s) && Bytes.IsFormerBiggerOrEqual(c.e, o.ip));
                    var city = ipInt32DB.FirstOrDefault(c => o.ip >= c.s && c.e >= o.ip);
                    return new IPLocationDTO
                    {
                        province = city?.p,
                        count = o.count,
                    };
                })
                    .Where(o => o.province != null)
                    .ToList();

                result = result.GroupBy(o => o.province)
                    .Select(o => new IPLocationDTO() {province = o.Key, count = o.Sum(p => p.count)})
                    .OrderByDescending(o => o.count)
                    .ToList();
            }

            return result;
        }

        [HttpGet]
        [Route("userLocation/liveUserByIP")]
        [IPAuth]
        public List<IPLocationDTO> GetLiveUserIpLocation()
        {
            //return new List<IPLocationDTO>() {new IPLocationDTO() {count=100,province= "Guangdong" }, new IPLocationDTO() { count = 100, province = "Guangdong Sheng" } };

            List<IPLocationDTO> result;

            var monthAgo = DateTime.UtcNow.AddHours(8).Date.AddDays(-6);

            using (var db2 = CFDHistoryEntities.Create())
            {
                db2.Database.CommandTimeout = 600;
                var ipStrCount = db2.HitIPs.AsNoTracking()
                    .GroupBy(o => o.ip)
                    .Select(o => new { ip = o.Key, count = o.Count() })
                    //.OrderByDescending(o => o.count)
                    .ToList();

                var ipByteCount = ipStrCount
                    .Select(o =>
                    {
                        var bytes = IPAddress.Parse(o.ip).GetAddressBytes();
                        Array.Reverse(bytes);
                        var ipInt = BitConverter.ToUInt32(bytes, 0);
                        return new
                        {
                            ip = ipInt,
                            count = o.count,
                        };
                    })
                    //.OrderByDescending(o => o.count)
                    .ToList();

                var ipDB =
                    db.IP2City.AsNoTracking() //.Where(o => o.CountryCode == "CN")
                        .Select(o => new { s = o.StartAddress, e = o.EndAddress, p = o.Province })
                        .ToList();
                var ipInt32DB = ipDB.Select(o =>
                {
                    var s = o.s;
                    var e = o.e;
                    Array.Reverse(s);
                    Array.Reverse(e);
                    return new { s = BitConverter.ToUInt32(s, 0), e = BitConverter.ToUInt32(e, 0), p = o.p };
                })
                    .ToList();

                result = ipByteCount.Select(o =>
                {
                    //var city =
                    //    cnCities.FirstOrDefault(
                    //        c => Bytes.IsFormerBiggerOrEqual(o.ip, c.s) && Bytes.IsFormerBiggerOrEqual(c.e, o.ip));
                    var city = ipInt32DB.FirstOrDefault(c => o.ip >= c.s && c.e >= o.ip);
                    return new IPLocationDTO
                    {
                        province = city?.p,
                        count = o.count,
                    };
                })
                    .Where(o => o.province != null)
                    .ToList();

                result = result.GroupBy(o => o.province)
                    .Select(o => new IPLocationDTO() { province = o.Key, count = o.Sum(p => p.count) })
                    .OrderByDescending(o => o.count)
                    .ToList();
            }

            return result;
        }
    }
}