using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("Card")]
    public class Card
    {
        [Key]
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public decimal? LowProfit { get; set; }

        public decimal? HighProfit { get; set; }

        public decimal? LowProfitRate { get; set; }

        public decimal? HighProfitRate { get; set; }

        public int? CardType { get; set; }

        /// <summary>
        /// 大图片的地址
        /// </summary>
        public string CardImgUrlBig { get; set; }

        /// <summary>
        /// 中图片的地址
        /// </summary>
        public string CardImgUrlMiddle { get; set; }

        /// <summary>
        /// 小图片的地址
        /// </summary>
        public string CardImgUrlSmall { get; set; }

        /// <summary>
        /// 大图标底部的颜色(16进制)
        /// </summary>
        public string ThemeColor { get; set; }

        public decimal? Reward { get; set; }

        /// <summary>
        /// 大图标的标题
        /// </summary>
        public string Title { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? Expiration { get; set; }
    }
}
