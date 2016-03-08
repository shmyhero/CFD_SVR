using System;
using ServiceStack.DesignPatterns.Model;

namespace CFD_JOBS.Models
{
    public class Quote : IHasStringId
    {
        /// <summary>
        /// security id
        /// </summary>
        public string Id { get; set; }

        public decimal? Bid { get; set; }
        public decimal? Offer { get; set; }

        public DateTime Time { get; set; }

//        public int 
    }
}