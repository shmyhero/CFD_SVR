using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("PingOrder")]
    public class PingOrder
    {
        /// <summary>
        /// Id就是OrderNumber，需要传入Ping++的接口
        /// </summary>
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string OrderNumber { get; set; }
        public decimal? AmountCNY { get; set; }
        /// <summary>
        /// 去掉Ping++手续费之后的人民币金额,通过Ping++的Callback接口得到
        /// </summary>
        public decimal? AmountNet { get; set; }
        /// <summary>
        /// TradeHero扣除手续费之后的金额，手续费暂定1%
        /// </summary>
        public decimal? AmountAdjusted { get; set; }

        /// <summary>
        /// 根据AmountAdjusted计算得出
        /// </summary>
        public decimal? AmountUSD { get; set; }
        /// <summary>
        /// 汇率
        /// </summary>
        public decimal? FxRate { get; set; }
        /// <summary>
        /// 汇率的获取时间
        /// </summary>
        public DateTime? FxRateAt { get; set; }
        /// <summary>
        /// 支付渠道，可能是alipay，alipay_wap，alipay_pc_direct，wx
       /// </summary>
        public string Channel { get; set; }
        /// <summary>
        /// 调用时间
        /// </summary>
        public DateTime? CreatedAt { get; set; }
        /// <summary>
        /// 支付回调时间
        /// </summary>
        public DateTime? WebHookAt { get; set; }
        /// <summary>
        /// 回调结果
        /// </summary>
        public string WebHookResult { get; set; }

        public DateTime? AyTransReqSentAt { get; set; }
        [StringLength(500)]
        public string AyTransReqSentResult { get; set; }
        [StringLength(50)]
        public string AyTransReqId { get; set; }
        public long? AyTransId { get; set; }
        [StringLength(20)]
        public string AyTransStatus { get; set; }
        public DateTime? AyTransUpdateAt { get; set; }
        [StringLength(50)]
        public string AyTransText { get; set; }

        public virtual User User { get; set; }
    }
}
