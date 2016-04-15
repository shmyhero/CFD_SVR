﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace AyondoTrade.Model
{
    [DataContract]
    public class PositionReport
    {
        [DataMember]
        public string PosMaintRptID { get; set; }
        [DataMember]
        public string SecurityID { get; set; }
        [DataMember]
        public decimal SettlPrice { get; set; }

        [DataMember]
        public DateTime CreateTime { get; set; }

        [DataMember]
        public decimal? LongQty { get; set; }
        [DataMember]
        public decimal? ShortQty { get; set; }

        [DataMember]
        public decimal? StopPx { get; set; }
        [DataMember]
        public decimal? TakePx { get; set; }

        [DataMember]
        public string StopOID { get; set; }
        [DataMember]
        public string TakeOID { get; set; }
    }
}