using System;

namespace CFD_API.DTO
{
    public class PositionDTO
    {
        public string id { get; set; }

        //public int secId { get; set; }
        //public string symbol { get; set; }
        //public string name { get; set; }
        public SecurityDetailDTO security { get; set; }

        public decimal invest { get; set; }
        public bool isLong { get; set; }
        public decimal leverage { get; set; }
        public decimal settlePrice { get; set; }
        public decimal quantity { get; set; }

        public decimal? pl { get; set; }
        public decimal? upl { get; set; }
        public DateTime createAt { get; set; }

        public decimal? stopPx { get; set; }
        public string stopOID { get; set; }
        public decimal? takePx { get; set; }
        public string takeOID { get; set; }
    }

    public class ReplaceStopTakeFormDTO
    {
        public string posId { get; set; }
        public int securityId { get; set; }
        public string orderId { get; set; }
        public decimal price { get; set; }
    }

    public class CancelTakeFormDTO
    {
        public string posId { get;set; }
        public string orderId { get; set; }
        public int securityId { get; set; }
    }

    public class NewTakeFormDTO
    {
        public string posId { get; set; }
        public int securityId { get; set; }
        public decimal price { get; set; }
    }
}