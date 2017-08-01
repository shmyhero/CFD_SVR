using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("ScoreHistory")]
    public class ScoreHistory
    {
        public int ID { get; set; }
        public int UserID { get; set; }
        /// <summary>
        /// 积分
        /// </summary>
        public int Score { get; set; }
        /// <summary>
        /// 来源，如：点赞、被点赞、App分享、微信好友、微信朋友圈、实盘下单
        /// </summary>
        public string Source { get; set; }
        /// <summary>
        /// 卡片ID
        /// </summary>
        public int UserCardID { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
