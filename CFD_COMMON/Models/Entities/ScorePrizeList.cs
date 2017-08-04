using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 预先设置的奖品领取列表
    /// </summary>
    [Table("ScorePrizeList")]
    public class ScorePrizeList
    {
        public int ID { get; set; }
        public int PrizeID { get; set; }
        public string PrizeName { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
