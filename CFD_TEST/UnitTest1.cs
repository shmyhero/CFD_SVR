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
    }
}