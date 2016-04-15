using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class PositionDTO
    {
        public string id { get; set; }

        //public int secId { get; set; }
        //public string symbol { get; set; }
        //public string name { get; set; }
        public SecurityDTO security { get; set; }

        public decimal invest { get; set; }
        public bool isLong { get; set; }
        public decimal leverage { get; set; }
        public decimal settlePrice { get; set; }
        public decimal quantity { get; set; }

        public decimal profit { get; set; }
        public DateTime createAt { get; set; }
    }
}