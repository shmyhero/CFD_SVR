using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Spatial;

namespace CFD_COMMON.Models.Entities
{
    [Table("DepositReward")]
    public class DepositReward
    {
        public int id { get; set; }
        public int UserId { get; set; }
        public decimal? Amount { get; set; }
        public decimal? DepositAmount { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
