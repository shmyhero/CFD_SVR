using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 快钱支付的订单表
    /// </summary>
    [Table("KuaiQianOrder")]
    public class KuaiQianOrder
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string OrderNumber { get; set; }
        /// <summary>
        /// 创建订单时的金额
        /// </summary>
        public decimal? OrderAmount { get; set; }
        /// <summary>
        /// 支付成功后，在Receive页面得到的金额
        /// </summary>
        public decimal? PayAmount { get; set; }
        /// <summary>
        /// TradeHero扣除手续费之后的金额，手续费暂定1%
        /// </summary>
        public decimal? PayAmountAdjusted { get; set; }

        /// <summary>
        /// 根据AmountAdjusted计算得出
        /// </summary>
        public decimal? PayAmountUSD { get; set; }
        /// <summary>
        /// 汇率
        /// </summary>
        public decimal? FxRate { get; set; }
        /// <summary>
        /// 汇率的获取时间
        /// </summary>
        public DateTime? FxRateAt { get; set; }

        /// <summary>
        /// 快钱收取的手续费
        /// </summary>

        public decimal? KuaiQianFee { get; set; }

        /// <summary>
        /// 用户在快钱的支付方式
        /// 一般为00，代表所有的支付方式。如果是银行直连商户，该值为10,该值与提交时相同。
        /// </summary>
        public string PayType { get; set; }
        /// <summary>
        /// 银行代码，比如：BOC
        /// </summary>
        public string BankId { get; set; }
        /// <summary>
        /// 银行卡号
        /// </summary>
        public string BankCardId { get; set; }

        /// <summary>
        /// 快钱的交易Id
        /// </summary>
        public string DealId { get; set; }

        /// <summary>
        /// 调用时间
        /// </summary>
        public DateTime? CreatedAt { get; set; }
        /// <summary>
        /// 支付回调时间
        /// </summary>
        public DateTime? ReceiveAt { get; set; }
        /// <summary>
        /// 回调结果
        /// </summary>
        public string ReceiveResult { get; set; }



        public DateTime? AyTransReqSentAt { get; set; }
        [Column(TypeName = "ntext")]
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
