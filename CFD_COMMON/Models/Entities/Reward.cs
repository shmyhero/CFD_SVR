using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("Reward")]
    public class Reward
    {
        [Key]
        public int Id { get; set; }
        public int UserID { get; set; }
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
    }
}
