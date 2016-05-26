using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;

namespace CFD_JOBS.Ayondo
{
    public class AyondoOldProdCleanup
    {
        public static void Run()
        {
            var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();
            var redisProdDefClient = redisClient.As<ProdDef>();
            var redisQuoteClient = redisClient.As<Quote>();
            var redisTickClient = redisClient.As<Tick>();

            var prodDefs = redisProdDefClient.GetAll();

            var idsToRemove = prodDefs.Where(o => DateTime.UtcNow - o.Time > TimeSpan.FromDays(7)).Select(o=>o.Id).ToList();

            CFDGlobal.LogLine("deleting ticks...");
            foreach (var id in idsToRemove)
            {
                redisTickClient.RemoveEntry("tick:" + id);
                redisTickClient.RemoveEntry("tick10m:" + id);
                redisTickClient.RemoveEntry("tick1h:" + id);
            }
            CFDGlobal.LogLine("deleting prods...");
            redisProdDefClient.DeleteByIds(idsToRemove);
            CFDGlobal.LogLine("deleting quotes...");
            redisQuoteClient.DeleteByIds(idsToRemove);
        }
    }
}
