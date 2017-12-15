using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Spatial;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 动态点赞历史记录
    /// </summary>
    [Table("TrendLikeHistory")]
    public class TrendLikeHistory
    {
        public int ID { get; set; }
        /// <summary>
        /// 动态的ID
        /// </summary>
        public int TrendID { get; set; }
        public int UserID { get; set; }
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
