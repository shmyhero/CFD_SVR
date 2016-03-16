using System;
using ServiceStack.DesignPatterns.Model;

namespace CFD_COMMON.Models.Cached
{
    public class Quote : IHasIntId
    {
        /// <summary>
        /// security id
        /// </summary>
        public int Id { get; set; }

        public decimal Bid { get; set; }
        public decimal Offer { get; set; }

        public DateTime Time { get; set; }

//        public int 
    }
}