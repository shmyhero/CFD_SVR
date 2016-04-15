using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO.FormDTO
{
    public class NewPositionFormDTO
    {
        public int securityId { get; set; }
        public bool isLong { get; set; }
        public decimal invest { get; set; }
        public decimal leverage { get; set; }
    }

    public class NetPositionFormDTO
    {
        public string posId { get; set; }
        public int securityId { get; set; }
        public bool isPosLong { get; set; }
        public decimal posQty { get; set; }
    }
}