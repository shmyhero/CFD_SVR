namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("AyondoTradeHistory")]
    public partial class AyondoTradeHistory
    {
        public long Id { get; set; }

        public long? PositionId { get; set; }

        public long? TradeId { get; set; }

        public long? AccountId { get; set; }

        [StringLength(50)]
        public string FirstName { get; set; }

        [StringLength(50)]
        public string LastName { get; set; }

        public DateTime? TradeTime { get; set; }

        public int? SecurityId { get; set; }

        [StringLength(200)]
        public string SecurityName { get; set; }

        [StringLength(10)]
        public string Direction { get; set; }

        public decimal? Quantity { get; set; }

        public decimal? TradePrice { get; set; }

        public decimal? PL { get; set; }

        [StringLength(50)]
        public string GUID { get; set; }

        public decimal? StopLoss { get; set; }

        public decimal? TakeProfit { get; set; }

        public DateTime? CreateTime { get; set; }

        [StringLength(20)]
        public string UpdateType { get; set; }

        [StringLength(20)]
        public string DeviceType { get; set; }
    }
}
