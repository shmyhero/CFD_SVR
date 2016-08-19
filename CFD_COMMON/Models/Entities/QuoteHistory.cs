namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("QuoteHistory")]
    public partial class QuoteHistory
    {
        [Key]
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int SecurityId { get; set; }

        [Key]
        [Column(Order = 1)]
        public DateTime Time { get; set; }

        public decimal? Bid { get; set; }

        public decimal? Ask { get; set; }
    }
}
