using System;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServiceStack.Text;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void ServiceStackJsonConfig()
        {
            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();
            var redisClient = basicRedisClientManager.GetClient();
            var redisTypedClient = redisClient.As<ProdDef>();

            var nowUTC = DateTime.UtcNow;
            var nowLocal = DateTime.Now;

            JsConfig.AlwaysUseUtc = true;

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
        }
    }
}