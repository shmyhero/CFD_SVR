using System;
using System.Linq;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;

namespace CFD_JOBS.Ayondo
{
    public class StockAlert
    {
        private static readonly TimeSpan _sleepInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan _tolerance = TimeSpan.FromMinutes(5);

        public static void Run()
        {
            CFDGlobal.LogLine("Starting...");

            while (true)
            {
                try
                {
                    using (var redisClient = CFDGlobal.PooledRedisClientsManager.GetClient())
                    {
                        var redisProdDefClient = redisClient.As<ProdDef>();
                        var redisQuoteClient = redisClient.As<Quote>();

                        var prodDefs = redisProdDefClient.GetAll();
                        var quotes = redisQuoteClient.GetAll();

                        using (var db = CFDEntities.Create())
                        {
                            var userAlerts =
                                db.UserAlerts.Where(o => o.HighEnabled.Value || o.LowEnabled.Value).ToList();

                            CFDGlobal.LogLine("Got " + userAlerts.Count + " alerts.");

                            var groups = userAlerts.GroupBy(o => o.SecurityId).ToList();

                            foreach (var group in groups)
                            {
                                var secId = group.Key;

                                CFDGlobal.LogLine("sec: " + secId + " alert_count: " + group.Count());

                                var prodDef = prodDefs.FirstOrDefault(o => o.Id == secId);
                                var quote = quotes.FirstOrDefault(o => o.Id == secId);

                                if (prodDef == null || quote == null)
                                {
                                    CFDGlobal.LogLine("cannot find prodDef/quote " + secId);
                                    continue;
                                }

                                if (prodDef.QuoteType == enmQuoteType.Closed ||
                                    prodDef.QuoteType == enmQuoteType.Inactive)
                                {
                                    CFDGlobal.LogLine("prod " + prodDef.Id + " quoteType is " + prodDef.QuoteType);
                                    continue;
                                }

                                if (DateTime.UtcNow - quote.Time > _tolerance)
                                {
                                    CFDGlobal.LogLine("quote " + quote.Id + " too old " + quote.Time);
                                    continue;
                                }

                                foreach (var alert in group)
                                {
                                    if (alert.HighEnabled.Value && quote.Bid >= alert.HighPrice)
                                    {
                                        alert.HighEnabled = false;
                                    }
                                    if (alert.LowEnabled.Value && quote.Offer <= alert.LowPrice)
                                    {
                                        alert.LowEnabled = false;
                                    }
                                }
                            }

                            db.SaveChanges();

                        }
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
        }
    }
}