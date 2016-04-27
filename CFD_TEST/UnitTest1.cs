using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using AyondoTrade;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_JOBS.Ayondo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using ServiceStack.Redis.Generic;
using ServiceStack.Text;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void ServiceStackJsonConfig()
        {
            //var basicRedisClientManager = CFDGlobal.GetNewBasicRedisClientManager();
            var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();
            var redisTypedClient = redisClient.As<ProdDef>();

            redisTypedClient.DeleteById(1);
            redisTypedClient.DeleteById(2);

            var nowUTC = DateTime.UtcNow;
            var nowLocal = DateTime.Now;

            redisTypedClient.Store(new ProdDef()
            {
                Id = 1,
                Name = "test",
                QuoteType = enmQuoteType.PhoneOnly,
                Symbol = "symbol",
                Time = nowUTC
            });

            ProdDef prodDef = redisTypedClient.GetById(1);
            Assert.AreEqual(enmQuoteType.PhoneOnly, prodDef.QuoteType);
            Assert.AreEqual(DateTimeKind.Utc, prodDef.Time.Kind);
            Assert.AreEqual(nowUTC, prodDef.Time);

            redisTypedClient.Store(new ProdDef()
            {
                Id = 1,
                Name = "test",
                QuoteType = enmQuoteType.PhoneOnly,
                Symbol = "symbol",
                Time = nowLocal
            });
            prodDef = redisTypedClient.GetById(1);
            Assert.AreEqual(DateTimeKind.Utc, prodDef.Time.Kind);
            Assert.AreEqual(nowLocal, prodDef.Time.ToLocalTime());

            redisTypedClient.DeleteById(1);
            redisTypedClient.DeleteById(2);
        }

        [TestMethod]
        public void JsonDateTimeConvert()
        {
            //JsonConvert.DefaultSettings.
            var utc = JsonConvert.SerializeObject(DateTime.UtcNow);
            var local = JsonConvert.SerializeObject(DateTime.Now);
            var unspecify = JsonConvert.SerializeObject(new DateTime(2008, 12, 28));
            
        }

        [TestMethod]
        public void RedisTest()
        {
            var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();

            redisClient.SetEntry("key1","value1");

            var value = redisClient.GetValue("key1");

            Assert.AreEqual("value1",value);

            redisClient.RemoveEntry(new[] {"key1"});
        }

        [TestMethod]
        public void WCFTest()
        {
            //EndpointAddress edpTcp = new EndpointAddress("net.tcp://localhost:38113/ayondotradeservice.svc");
            //EndpointAddress edpHttp = new EndpointAddress("http://localhost:38113/ayondotradeservice.svc");
            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");

            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            //var r1 = clientTcp.Test("haha tcp");
            //var r2 = clientHttp.Test("haha http");
            var positionReport = clientHttp.GetPositionReport("jiangyi1985", "ivan");

            //// 创建Binding  
            //NetTcpBinding tcpBinding = new NetTcpBinding(SecurityMode.None);
            //BasicHttpBinding httpBinding = new BasicHttpBinding(BasicHttpSecurityMode.None);

            //// 创建通道  
            //ChannelFactory<IAyondoTradeService> factoryTcp = new ChannelFactory<IAyondoTradeService>(tcpBinding);
            //var channelTcp = factoryTcp.CreateChannel(edpTcp);

            //ChannelFactory<IAyondoTradeService> factoryHttp = new ChannelFactory<IAyondoTradeService>(httpBinding);
            //var channelHttp = factoryHttp.CreateChannel(edpHttp);

            //// 调用  
            //var r1 = channelTcp.Test("haha tcp");
            //var r2 = channelHttp.Test("haha http");

            //// 关闭通道  
            //((IClientChannel)channelHttp).Close();  
        }

        [TestMethod]
        public void RemoveInvalidQuotes()
        {
            var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();
            var redisProdDefClient = redisClient.As<ProdDef>();
            var redisTickClient = redisClient.As<Tick>();

            var prodDefs = redisProdDefClient.GetAll();

            foreach (var prodDef in prodDefs)
            {
                CFDGlobal.LogLine(prodDef.Id + "...");

                IRedisList<Tick> redisList = redisTickClient.Lists["tick10m:" + prodDef.Id];

                if(redisList.Count==0)
                    continue;

                var list =redisList.GetAll();
                var last = list[list.Count - 1];
                var last2 = list[list.Count - 2];

                if (last.Time.Year == 2016 && last.Time.Month == 4 && last.Time.Day == 27 && last.Time.Hour == 2 && last.Time.Minute == 23
                    && last.Time - last2.Time >= TimeSpan.FromHours(1))
                {
                    var totalHours = (last.Time - last2.Time).TotalHours;
                    CFDGlobal.LogLine(prodDef.Id+":"+totalHours.ToString());

                    var tick = redisTickClient.Lists["tick:" + prodDef.Id].Pop();
                }
            }
        }
    }
}