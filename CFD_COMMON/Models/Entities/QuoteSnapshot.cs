using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("QuoteSnapshot")]
    public class QuoteSnapshot
    {
        [Key]
        [Column(Order = 0)]
        public int SecurityId { get; set; } 

        [Key]
        [Column(Order = 1)]
        public DateTime Date { get; set; }

        public DateTime? QuoteTime { get; set; }
       
        public decimal? Bid { get; set; }

        public decimal? Ask { get; set; }
    }
}
