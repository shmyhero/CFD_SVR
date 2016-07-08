using System;
using System.Collections.Generic;
using System.Linq;
using CFD_COMMON.Models.Cached;

namespace CFD_COMMON.Utils
{
    public class FX
    {
        //public static decimal Convert(decimal value, string fromCcy, string toCcy, IRedisClient redisClient)
        //{
        //    var redisProdDefClient = redisClient.As<ProdDef>();
        //    var redisQuoteClient = redisClient.As<Quote>();

        //    if (fromCcy == toCcy)
        //        return value;

        //    //get fxRate and convert 
        //    //the fx for convertion! not the fx that is being bought!
        //    decimal fxRate;

        //    var fxProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == fromCcy + toCcy);

        //    if (fxProdDef == null)
        //    {
        //        //CFDGlobal.LogInformation("Cannot find fx rate: " + fromCcy + "/" + toCcy + ". Trying: " + toCcy + "/" + fromCcy);

        //        fxProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == toCcy + fromCcy);

        //        if (fxProdDef == null)
        //        {
        //            throw new Exception("Cannot find fx rate: " + fromCcy + "/" + toCcy + " or " + toCcy + "/" + fromCcy);
        //        }

        //        if (DateTime.UtcNow - fxProdDef.Time > CFDGlobal.PROD_DEF_ACTIVE_IF_TIME_NOT_OLDER_THAN_TS)
        //            CFDGlobal.LogWarning("fx rate too old:" + fxProdDef.Id + " " + fxProdDef.Symbol);

        //        var fxQuote = redisQuoteClient.GetById(fxProdDef.Id);
        //        fxRate = 1/Quotes.GetLastPrice(fxQuote);
        //    }
        //    else
        //    {
        //        var fxQuote = redisQuoteClient.GetById(fxProdDef.Id);
        //        fxRate = Quotes.GetLastPrice(fxQuote);
        //    }

        //    return value*fxRate;
        //}

        public static decimal Convert(decimal value, string fromCcy, string toCcy, IList<ProdDef> prodDefs, IList<Quote> quotes)
        {
            if (fromCcy == toCcy)
                return value;

            //get fxRate and convert 
            //the fx for convertion! not the fx that is being bought!
            decimal fxRate;

            var fxProdDef = prodDefs.FirstOrDefault(o => o.Symbol == fromCcy + toCcy);

            if (fxProdDef == null)
            {
                //CFDGlobal.LogInformation("Cannot find fx rate: " + fromCcy + "/" + toCcy + ". Trying: " + toCcy + "/" + fromCcy);

                fxProdDef = prodDefs.FirstOrDefault(o => o.Symbol == toCcy + fromCcy);

                if (fxProdDef == null)
                {
                    throw new Exception("Cannot find fx rate: " + fromCcy + "/" + toCcy + " or " + toCcy + "/" + fromCcy);
                }

                var fxQuote = quotes.FirstOrDefault(o => o.Id == fxProdDef.Id);
                fxRate = 1/Quotes.GetLastPrice(fxQuote);
            }
            else
            {
                var fxQuote = quotes.FirstOrDefault(o => o.Id == fxProdDef.Id);
                fxRate = Quotes.GetLastPrice(fxQuote);
            }

            return value*fxRate;
        }

        public static decimal ConvertUSDtoCcy(decimal value, string toCcy, IList<ProdDef> prodDefs, IList<Quote> quotes)
        {
            return Convert(value, "USD", toCcy, prodDefs, quotes);
        }

        /// <summary>
        /// for margin fx conversion
        /// use mid price of outright products
        /// </summary>
        public static decimal ConvertByOutrightMidPrice(decimal value, string fromCcy, string toCcy, IList<ProdDef> prodDefs, IList<Quote> quotes)
        {
            if (fromCcy == toCcy || value == 0)
                return value;

            decimal fxRate;

            var fxProdDef = prodDefs.FirstOrDefault(o => o.Symbol == fromCcy + toCcy && o.Name.EndsWith(" Outright"));

            if (fxProdDef == null)
            {
                fxProdDef = prodDefs.FirstOrDefault(o => o.Symbol == toCcy + fromCcy && o.Name.EndsWith(" Outright"));

                if (fxProdDef == null)
                {
                    throw new Exception("Cannot find fx outright rate: " + fromCcy + "/" + toCcy + " or " + toCcy + "/" + fromCcy);
                }

                var fxQuote = quotes.FirstOrDefault(o => o.Id == fxProdDef.Id);

                fxRate = (fxQuote.Bid + fxQuote.Offer)/2; //reversed fx
                return value / fxRate;
            }
            else
            {
                var fxQuote = quotes.FirstOrDefault(o => o.Id == fxProdDef.Id);

                fxRate = (fxQuote.Bid + fxQuote.Offer)/2; //fx
                return value * fxRate;
            }
        }

        /// <summary>
        /// for pl/upl fx calculation
        /// use bid/offer -/+0.5% of outright products
        /// </summary>
        public static decimal ConvertPlByOutright(decimal pl, string fromCcy, string toCcy, IList<ProdDef> prodDefs, IList<Quote> quotes)
        {
            if (fromCcy == toCcy || pl == 0)
                return pl;

            decimal fxRate;

            var fxProdDef = prodDefs.FirstOrDefault(o => o.Symbol == fromCcy + toCcy && o.Name.EndsWith(" Outright"));

            if (fxProdDef == null)
            {
                fxProdDef = prodDefs.FirstOrDefault(o => o.Symbol == toCcy + fromCcy && o.Name.EndsWith(" Outright"));

                if (fxProdDef == null)
                {
                    throw new Exception("Cannot find fx outright rate: " + fromCcy + "/" + toCcy + " or " + toCcy + "/" + fromCcy);
                }

                var fxQuote = quotes.FirstOrDefault(o => o.Id == fxProdDef.Id);

                fxRate = pl > 0 ? fxQuote.Offer * 1.005m : fxQuote.Bid * 0.995m;
                return pl / fxRate;
            }
            else
            {
                var fxQuote = quotes.FirstOrDefault(o => o.Id == fxProdDef.Id);

                fxRate = pl > 0 ? fxQuote.Bid*0.995m : fxQuote.Offer*1.005m;
                return pl * fxRate;
            }
        }
    }
}