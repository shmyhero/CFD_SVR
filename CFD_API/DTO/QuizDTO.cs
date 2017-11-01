using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class QuizDTO
    {
        public int ID { get; set; }
        public int ProdID { get; set; }
        public string ProdName { get; set; }
        /// <summary>
        /// 竞猜开始时间
        /// </summary>
        public DateTime? OpenAt { get; set; }
        /// <summary>
        /// 竞猜结束时间
        /// </summary>
        public DateTime? ClosedAt { get; set; }
        /// <summary>
        /// 竞猜对应的交易日
        /// </summary>
        public DateTime? TradeDay { get; set; }
        public string Result { get; set; }
        /// <summary>
        /// 买涨的金额
        /// </summary>
        public decimal? LongAmount { get; set; }
        /// <summary>
        /// 买涨的人数
        /// </summary>
        public int? LongPersons { get; set; }
        /// <summary>
        /// 买跌的金额
        /// </summary>
        public decimal? ShortAmount { get; set; }
        /// <summary>
        /// 买跌的人数
        /// </summary>
        public int? ShortPersons { get; set; }
    }

    public class QuizBetDTO
    {
        public int? ID { get; set; }
        public int? QID { get; set; }
        public int? ProdID { get; set; }
        public string ProdName { get; set; }
        public DateTime? OpenAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? TradeDay { get; set; }
        public DateTime? SettledAt { get; set; }

        public string Result { get; set; }
        public decimal? LongAmount { get; set; }
        public decimal? LongBenefit { get; set; }
        public int? LongPersons { get; set; }

        public decimal? ShortAmount { get; set; }
        public decimal? ShortBenefit { get; set; }
        public int? ShortPersons { get; set; }

        public decimal? AvailableBonus { get; set; }

        public decimal? BetAmount { get; set; }

        public string BetDirection { get; set; }

        public decimal? PL { get; set; }

        public bool? IsViewed { get; set; }
    }
}