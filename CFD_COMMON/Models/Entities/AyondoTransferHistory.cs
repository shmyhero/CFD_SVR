namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public class AyondoTransferHistoryBase
    {
        public long Id { get; set; }

        [StringLength(20)]
        public string TransferType { get; set; }

        public long? AccountId { get; set; }

        [StringLength(50)]
        public string FirstName { get; set; }

        [StringLength(50)]
        public string LastName { get; set; }

        public decimal? Amount { get; set; }

        [StringLength(10)]
        public string Ccy { get; set; }

        public DateTime? Timestamp { get; set; }
        public DateTime? ApprovalTime { get; set; }

        [StringLength(50)]
        public string WhiteLabel { get; set; }

        [StringLength(200)]
        public string ProductName { get; set; }

        [StringLength(10)]
        public string BaseCcy { get; set; }

        [StringLength(10)]
        public string QuoteCcy { get; set; }

        public decimal? Quantity { get; set; }

        [StringLength(50)]
        public string InstrumentType { get; set; }

        public bool? IsAyondo { get; set; }

        [StringLength(50)]
        public string ClientClassification { get; set; }

        [StringLength(50)]
        public string Username { get; set; }

        public decimal? FinancingRate { get; set; }
        public long? TransactionId { get; set; }
        public long? TradingAccountId { get; set; }

        [StringLength(50)]
        public string AssetClass { get; set; }

        public long? PositionId { get; set; }
    }

    [Table("AyondoTransferHistory")]
    public partial class AyondoTransferHistory : AyondoTransferHistoryBase
    {
    }
}
