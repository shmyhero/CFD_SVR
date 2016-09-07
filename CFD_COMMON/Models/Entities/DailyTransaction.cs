using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Models.Entities
{
    [Table("DailyTransaction")]
    public class DailyTransaction
    {
        public int id { get; set; }
        public int UserId { get; set; }
        public DateTime? Date { get; set; } 
        public DateTime? DealAt { get; set; }
        public decimal Amount { get; set; }
        public bool? IsPaid { get; set; }
    }
}
