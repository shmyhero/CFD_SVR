﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CFD_COMMON.Models.Cached;

namespace CFD_API.DTO
{
    public class SecurityDTO
    {
        public int id { get; set; }
        public string symbol { get; set; }
        public string name { get; set; }
        //public string picUrl { get; set; }
        public string tag { get; set; }

        public decimal? preClose { get; set; }
        public decimal? open { get; set; }
        public decimal? last { get; set; }

        public bool? isOpen { get; set; }

        public string eName { get; set; }
    }

    public class SecurityDetailDTO : SecurityDTO
    {
        public int? dcmCount { get; set; }

        public decimal? bid { get; set; }
        public decimal? ask { get; set; }

        public DateTime? lastOpen { get; set; }
        public DateTime? lastClose { get; set; }

        public decimal? longPct { get; set; }

        public decimal? minValueLong { get; set; }
        public decimal? minValueShort { get; set; }
        public decimal? maxValueLong { get; set; }
        public decimal? maxValueShort { get; set; }
        public decimal? maxLeverage { get; set; }

        public decimal? smd { get; set; }
        public decimal? gsmd { get; set; }

        public string ccy { get; set; }

        /// <summary>
        /// 当前行情是否中断
        /// </summary>
        public bool isPriceDown { get; set; }

        public List<int> levList { get; set; }
    }

    /// <summary>
    /// for test api use only
    /// </summary>
    public class ProdDefDTO : ProdDef
    {
        public string cname { get; set; }
        public decimal? minValueLong { get; set; }
        public decimal? minValueShort { get; set; }
        public decimal? maxValueLong { get; set; }
        public decimal? maxValueShort { get; set; }
    }

    public class ByPopularityDTO
    {
        public int id { get; set; }
        public string symbol { get; set; }
        public string name { get; set; }
        public int userCount { get; set; }
        public int longCount { get; set; }
        public int shortCount { get; set; }
    }
}