namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public class NewPositionHistoryBase
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }

        public int? UserId { get; set; }
        public int? SecurityId { get; set; }
        public decimal? SettlePrice { get; set; }
        public DateTime? CreateTime { get; set; }
        public decimal? LongQty { get; set; }
        public decimal? ShortQty { get; set; }
        public decimal? Leverage { get; set; }
        public decimal? InvestUSD { get; set; }
        public DateTime? ClosedAt { get; set; }
        public decimal? PL { get; set; }
        public decimal? ClosedPrice { get; set; }
        public decimal? StopPx { get; set; }
        public decimal? TakePx { get; set; }
        public bool? IsAutoClosed { get; set; }
    }

    [Table("NewPositionHistory")]
    public partial class NewPositionHistory : NewPositionHistoryBase
    {
    }
}
