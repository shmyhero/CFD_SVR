using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Spatial;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 动态打赏历史记录
    /// </summary>
    [Table("TrendRewardHistory")]
    public class TrendRewardHistory
    {
        public int ID { get; set; }
        /// <summary>
        /// 动态的ID
        /// </summary>
        public int TrendID { get; set; }
        /// <summary>
        /// 打赏人的ID
        /// </summary>
        public int RewardUserID { get; set; }
        /// <summary>
        /// 打赏金额
        /// </summary>
        public int Amount { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// 删除时间
        /// </summary>
        public DateTime ExpiredAt { get; set; }
    }
}
