using System;
using Newtonsoft.Json;
using ServiceStack.DesignPatterns.Model;

namespace CFD_COMMON.Models.Cached
{
    public class Tick //: IHasIntId
    {
        ///// <summary>
        ///// security id
        ///// </summary>
        //public int Id { get; set; }

        /// <summary>
        /// price
        /// </summary>
        public decimal P { get; set; }

        public DateTime Time { get; set; }
    }
}