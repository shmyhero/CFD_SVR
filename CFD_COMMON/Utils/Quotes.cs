using System;
using CFD_COMMON.Models.Cached;
using System.Collections.Generic;

namespace CFD_COMMON.Utils
{
    public class Quotes
    {
        public static decimal GetLastPrice(Quote quote)
        {
            int c1 = BitConverter.GetBytes(decimal.GetBits(quote.Offer)[3])[2];
            int c2 = BitConverter.GetBytes(decimal.GetBits(quote.Bid)[3])[2];
            int decimalCount = Math.Max(c1, c2);

            return Math.Round((quote.Offer + quote.Bid) / 2, decimalCount, MidpointRounding.AwayFromZero);
        }

        //public static decimal? GetLastPrice(AyondoSecurity security)
        //{
        //    return (security.Ask + security.Bid) / 2;
        //}

        public static decimal? GetLastPrice(ProdDef prodDef)
        {
            if (prodDef.Bid == null || prodDef.Offer == null)
                return null;

            int c1 = BitConverter.GetBytes(decimal.GetBits(prodDef.Offer.Value)[3])[2];
            int c2 = BitConverter.GetBytes(decimal.GetBits(prodDef.Bid.Value)[3])[2];
            int decimalCount = Math.Max(c1, c2);

            return Math.Round((prodDef.Offer.Value + prodDef.Bid.Value) / 2, decimalCount, MidpointRounding.AwayFromZero);
        }

        public static decimal? GetClosePrice(ProdDef prodDef)
        {
            if (prodDef.CloseAsk == null || prodDef.CloseBid == null)
                return null;

            int c1 = BitConverter.GetBytes(decimal.GetBits(prodDef.CloseAsk.Value)[3])[2];
            int c2 = BitConverter.GetBytes(decimal.GetBits(prodDef.CloseBid.Value)[3])[2];
            int decimalCount = Math.Max(c1, c2);

            return Math.Round((prodDef.CloseAsk.Value + prodDef.CloseBid.Value) / 2, decimalCount, MidpointRounding.AwayFromZero);
        }

        public static decimal? GetOpenPrice(ProdDef prodDef)
        {
            if (prodDef.OpenAsk == null || prodDef.OpenBid == null)
                return null;

            int c1 = BitConverter.GetBytes(decimal.GetBits(prodDef.OpenAsk.Value)[3])[2];
            int c2 = BitConverter.GetBytes(decimal.GetBits(prodDef.OpenBid.Value)[3])[2];
            int decimalCount = Math.Max(c1, c2);

            return Math.Round((prodDef.OpenAsk.Value + prodDef.OpenBid.Value) / 2, decimalCount, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 是否价格已经中断
        /// </summary>
        /// <param name="interval">中断的容忍周期</param>
        /// <returns></returns>
        public static bool IsPriceDown(KeyValuePair<int, int> interval, DateTime time)
        {
            if (default(KeyValuePair<int, int>).Equals(interval))
                return (DateTime.UtcNow - time).TotalSeconds >= 3 * 60;

            return (DateTime.UtcNow - time).TotalSeconds >= interval.Value;
        }
    }
}