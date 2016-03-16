using System;
using ServiceStack.DesignPatterns.Model;

namespace CFD_COMMON.Models.Cached
{
    public class ProdDef:IHasIntId
    {
        public int Id { get; set; }

        public DateTime Time { get; set; }
        //public string Symbol { get; set; }
        public int QuoteType { get; set; }

        public string Name { get; set; }
    }
}
