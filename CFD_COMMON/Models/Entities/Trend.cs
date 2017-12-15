using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Spatial;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 动态
    /// </summary>
    [Table("Trend")]
    public class Trend
    {
        public int ID { get; set; }

        public int UserID { get; set; }
        /// <summary>
        /// 动态的内容
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// 点赞次数
        /// </summary>
        public int Likes { get; set; }
        /// <summary>
        /// 打赏次数
        /// </summary>
        public int RewardCount { get; set; }
        /// <summary>
        /// 总打赏积分
        /// </summary>
        public int TotalRewardedScore { get; set; }
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
