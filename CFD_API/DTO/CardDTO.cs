using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class CardDTO
    {
        public int cardId { get; set; }
        public decimal? invest { get; set; }
        public bool? isLong { get; set; }
        public decimal? leverage { get; set; }
        public decimal? tradePrice { get; set; }
        public decimal? settlePrice { get; set; }

        public string imgUrlBig { get; set; }

        public string imgUrlMiddle { get; set; }

        public string imgUrlSmall { get; set; }

        public decimal? reward { get; set; }

        public DateTime? tradeTime { get; set; }

        public string ccy { get; set; }

        public int? stockID { get; set; }
        public string stockName { get; set; }

        public string themeColor { get; set; }

        public decimal? pl { get; set; }

        public decimal? plRate { get; set; }

        /// <summary>
        /// 点赞的总数
        /// </summary>
        public int? likes { get; set; }
        /// <summary>
        /// 当前用户是否点赞过
        /// </summary>
        public bool? liked { get; set; }

        /// <summary>
        /// 是否被分享到首页
        /// </summary>
        public bool? shared { get; set; }

        /// <summary>
        /// 是否查看过
        /// </summary>
        public bool? isNew { get; set; }

        /// <summary>
        /// 在首页显示时需要人名
        /// </summary>
        public string userName { get; set; }
        /// <summary>
        /// 卡片头像
        /// </summary>
        public string profileUrl { get; set; }
        
        public decimal? last { get; set; }
        public decimal? preClose { get; set; }
        public decimal? rate { get; set; }
    }

    public class CardCollectionDTO
    {
        public List<CardDTO> cards { get; set; }
        public bool hasNew { get; set; }
    }
}