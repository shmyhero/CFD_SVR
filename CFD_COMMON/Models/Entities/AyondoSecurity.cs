namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("AyondoSecurity")]
    public partial class AyondoSecurity
    {
        [StringLength(255)]
        public string Name { get; set; }

        [StringLength(255)]
        public string Symbol { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [StringLength(255)]
        public string Exchange { get; set; }

        [StringLength(10)]
        public string BaseCcy { get; set; }

        [StringLength(10)]
        public string QuoteCcy { get; set; }

        [StringLength(255)]
        public string AssetClass { get; set; }

        public decimal? DisplayDecimals { get; set; }

        [StringLength(255)]
        public string Financing { get; set; }

        public decimal? Bid { get; set; }

        public decimal? Ask { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public bool? Shortable { get; set; }

        [StringLength(255)]
        public string CName { get; set; }

        public DateTime? DefUpdatedAt { get; set; }

        public DateTime? QuoteUpdatedAt { get; set; }

        public decimal? MaxSizeLong { get; set; }
        public decimal? MinSizeLong { get; set; }
        public decimal? MaxSizeShort { get; set; }
        public decimal? MinSizeShort { get; set; }
        public decimal? MaxLeverage { get; set; }
    }
}
