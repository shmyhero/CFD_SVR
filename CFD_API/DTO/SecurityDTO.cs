using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class SecurityDTO
    {
        public int id { get; set; }
        public string symbol { get; set; }
        public string name { get; set; }
        //public string picUrl { get; set; }
        public string tag { get; set; }
        public decimal open { get; set; }
        public decimal last { get; set; }
        public bool isOpen { get; set; }
    }

    public class SecurityDetailDTO : SecurityDTO
    {
        public decimal preClose { get; set; }
        public decimal longPct { get; set; }
    }
}