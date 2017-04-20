using System;
using CFD_COMMON.Models.Cached;
using System.Collections.Generic;
using CFD_COMMON.Models.Entities;

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
        public static bool IsPriceDown(ProdSetting prodSetting, DateTime time)
        {
            if (prodSetting == null || !prodSetting.PriceDownInterval.HasValue || prodSetting.PriceDownInterval.Value <= 0)
                return (DateTime.UtcNow - time).TotalSeconds >= 3 * 60;

            return (DateTime.UtcNow - time).TotalSeconds >= prodSetting.PriceDownInterval.Value;
        }

        /// <summary>
        /// 计算收益率
        /// </summary>
        /// <param name="buyLong">是否买涨</param>
        /// <param name="tradePrice">买入价</param>
        /// <param name="settlePrice">卖出价</param>
        /// <param name="levarage">杠杆</param>
        /// <returns>收益率</returns>
        public static decimal? GetProfitRate(bool buyLong, decimal? tradePrice, decimal? settlePrice, decimal? leverage)
        {
            var plRatePercent = buyLong?
                        (settlePrice - tradePrice) / tradePrice * leverage * 100
                        : (tradePrice - settlePrice) / tradePrice * leverage * 100;

            return plRatePercent;
        }

        /// <summary>
        /// 是否盈利
        /// </summary>
        /// <param name="buyLong">是否买涨</param>
        /// <param name="tradePrice">买入价</param>
        /// <param name="settlePrice">卖出价</param>
        /// <returns>是否盈利</returns>
        public static bool IsProfit(bool buyLong, decimal? tradePrice, decimal? settlePrice)
        {
            return buyLong ? settlePrice >= tradePrice : tradePrice >= settlePrice;
        }
    }
}