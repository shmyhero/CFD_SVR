using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("ScoreConsumptionHistory")]
    public class ScoreConsumptionHistory
    {
        public int ID { get; set; }
        public int? UserID { get; set; }
        public int? Score { get; set; }
        public int? PrizeID { get; set; }
        public string PrizeName { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
