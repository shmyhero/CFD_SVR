using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("UserCard")]
    public class UserCard
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 用户Id
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// 卡片Id
        /// </summary>
        public int CardId { get; set; }

        /// <summary>
        /// 买涨、买跌
        /// </summary>
        public bool? IsLong { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        public decimal? Qty { get; set; }

        public decimal? Invest { get; set; }

        public decimal? Leverage { get; set; }

        public decimal? TradePrice { get; set; }

        public decimal? SettlePrice { get; set; }

        public DateTime? TradeTime { get; set; }

        /// <summary>
        /// 鼓励金
        /// </summary>
        public decimal? Reward { get; set; }

        public string CCY { get; set; }

        public string StockName { get; set; }

        public decimal? PL { get; set; }

        public DateTime? ClosedAt { get; set; }

        /// <summary>
        /// 点赞数量
        /// </summary>
        public int? Likes { get; set; }

        public bool? IsNew { get; set; }

        /// <summary>
        /// 是否被分享到首页
        /// </summary>
        public bool? IsShared { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? Expiration { get; set; }
    }
}
