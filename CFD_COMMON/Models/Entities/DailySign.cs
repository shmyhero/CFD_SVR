using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Models.Entities
{
    [Table("DailySign")]
    public class DailySign
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime? SignAt { get; set; }
        public int Continuity { get; set; }
    }
}
