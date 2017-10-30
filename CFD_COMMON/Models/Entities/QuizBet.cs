using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Spatial;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 竞猜下注表
    /// </summary>
    [Table("QuizBet")]
    public class QuizBet
    {
        public int ID { get; set; }

        public int UserID { get; set; }

        /// <summary>
        /// 竞猜活动的ID
        /// </summary>
        public int QuizID { get; set; }

        /// <summary>
        /// 下注的金额
        /// </summary>
        public decimal? BetAmount { get; set; }
        /// <summary>
        /// 买涨/买跌
        /// </summary>
        public string BetDirection { get; set; }
        /// <summary>
        /// 记录的创建时间
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// 盈亏的金额
        /// </summary>
        public decimal? PL { get; set; }
        /// <summary>
        /// 盈亏的结算时间
        /// </summary>
        public DateTime? SettledAt { get; set; }
        /// <summary>
        /// 盈亏是否已经给用户看过
        /// </summary>
        public bool? IsPLViewed { get; set; }
    }
}
