using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("PingOrder")]
    public class PingOrder
    {
        /// <summary>
        /// Id就是OrderNumber，需要传入Ping++的接口
        /// </summary>
        public int Id { get; set; }
        public decimal Amount { get; set; }

        public decimal ExchangeRate { get; set; }

        public DateTime? CreatedAt { get; set; }

        public bool? Paid { get; set; }
    }
}
