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
        //public string Symbol { get; set; }
        public enmQuoteType QuoteType { get; set; }

        public string Name { get; set; }

        public string Symbol { get; set; }
    }

    public enum enmQuoteType
    {
        Closed = 0,
        Open = 1,
        PhoneOnly = 2
    }
}