using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack.DesignPatterns.Model;

namespace CFD_COMMON.Models
{
    public class ProdDef:IHasIntId
    {
        public int Id { get; set; }

        public DateTime Time { get; set; }
        //public string Symbol { get; set; }
        public int QuoteType { get; set; }
    }
}
