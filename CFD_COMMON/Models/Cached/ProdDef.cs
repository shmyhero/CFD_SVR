using System;
using ServiceStack.DesignPatterns.Model;

namespace CFD_COMMON.Models.Cached
{
    public class ProdDef : IHasIntId
    {
        /// <summary>
        /// security id
        /// </summary>
        public int Id { get; set; }

        public DateTime Time { get; set; }

        public decimal? Bid { get; set; }
        public decimal? Offer { get; set; }

        public decimal? CloseBid { get; set; }
        public decimal? CloseAsk { get; set; }

        //public string Symbol { get; set; }
        public enmQuoteType QuoteType { get; set; }

        public string Name { get; set; }

        public string Symbol { get; set; }

        public string AssetClass { get; set; }

        public decimal MaxLeverage { get; set; }
        public bool Shortable { get; set; }
        public decimal MinSizeShort { get; set; }
        public decimal MaxSizeShort { get; set; }
        public decimal MinSizeLong { get; set; }
        public decimal MaxSizeLong { get; set; }

        //fields that are not from Ayondo
        public DateTime? LastOpen { get; set; }
        public DateTime? LastClose { get; set; }
        public decimal? OpenBid { get; set; }
        public decimal? OpenAsk { get; set; }
        public decimal? PreClose { get; set; }

        public decimal? PLUnits { get; set; }
        public decimal? LotSize { get; set; }
        public string Ccy2 { get; set; }
    }

    public enum enmQuoteType
    {
        Closed = 0,
        Open = 1,
        PhoneOnly = 2
    }
}