using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO.SignalRDTO
{
    public class QuoteFeed
    {
        public int id { get; set; }
        public decimal last { get; set; }
    }
}