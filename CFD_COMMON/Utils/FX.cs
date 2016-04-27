using System;
using System.Linq;
using CFD_COMMON.Models.Cached;
using ServiceStack.Redis;

namespace CFD_COMMON.Utils
{
    public class FX
    {
        public static decimal Convert(decimal value, string fromCcy, string toCcy, IRedisClient redisClient)
        {
            var redisProdDefClient = redisClient.As<ProdDef>();
            var redisQuoteClient = redisClient.As<Quote>();

            if (fromCcy == toCcy)
                return value;

            //get fxRate and convert 
            //the fx for convertion! not the fx that is being bought!
            decimal fxRate;

            var fxProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == fromCcy + toCcy);

            if (fxProdDef == null)
            {
                CFDGlobal.LogInformation("Cannot find fx rate: " + fromCcy + "/" + toCcy + ". Trying: " + toCcy + "/" + fromCcy);

                fxProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == toCcy + fromCcy);
                if (fxProdDef == null)
                {
                    throw new Exception("Cannot find fx rate: " + fromCcy + "/" + toCcy + " or " + toCcy + "/" + fromCcy);
                }

                var fxQuote = redisQuoteClient.GetById(fxProdDef.Id);
                fxRate = 1/Quotes.GetLastPrice(fxQuote);
            }
            else
            {
                var fxQuote = redisQuoteClient.GetById(fxProdDef.Id);
                fxRate = Quotes.GetLastPrice(fxQuote);
            }

            return value*fxRate;
        }

        public static decimal ConvertUSDtoCcy(decimal value, string toCcy, IRedisClient redisClient)
        {
            return Convert(value, "USD", toCcy, redisClient);
        }
    }
}