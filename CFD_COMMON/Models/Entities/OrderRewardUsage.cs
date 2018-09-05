using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("OrderRewardUsage")]
    public class OrderRewardUsage
    {
        [Key]
        public int Id { get; set; }

        public int? UserId { get; set; }

        public decimal? RewardFxRate { get; set; }

        public decimal? RewardAmountUSD { get; set; }

        public string OrderNumber { get; set; }

        public DateTime? CreatedAt { get;set;}
        
        /// <summary>
        /// Ping++订单的支付时间
        /// </summary>
        public DateTime? PingPaidAt { get; set; }

        /// <summary>
        /// Ayondo转账发起时间
        /// </summary>
        public DateTime? AyTransReqSentAt { get; set; }

        /// <summary>
        /// 转账结果
        /// </summary>
        public string AyTransReqSentResult { get; set; }

        /// <summary>
        /// 转账请求ID
        /// </summary>
        public string AyTransReqId { get; set; }

        /// <summary>
        /// 转账交易ID
        /// </summary>
        public long? AyTransId { get; set; }

        public string AyTransStatus { get; set; }

        /// <summary>
        /// 交易状态更新时间
        /// </summary>
        public DateTime? AyTransUpdateAt { get; set; }

        /// <summary>
        /// 交易信息
        /// </summary>
        public string AyTransText { get; set; }

        public virtual User User { get; set; }
    }
}
