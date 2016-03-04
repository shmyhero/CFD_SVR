using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class SecurityDTO
    {
        public string symbol { get; set; }
        public string name { get; set; }
        public string picUrl { get; set; }
        public string tag { get; set; }
        public decimal open { get; set; }
        public decimal last { get; set; }
    }
}