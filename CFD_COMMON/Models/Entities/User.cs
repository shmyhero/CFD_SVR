namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("User")]
    public partial class User
    {
        public int Id { get; set; }

        [StringLength(20)]
        public string Phone { get; set; }

        public DateTime? CreatedAt { get; set; }

        [StringLength(50)]
        public string Nickname { get; set; }

        [StringLength(200)]
        public string Token { get; set; }

        public DateTime? LastHitAt { get; set; }

        //public DateTime? LastLoginAt { get; set; }

        [StringLength(200)]
        public string PicUrl { get; set; }

        [StringLength(50)]
        public string WeChatOpenId { get; set; }

        [StringLength(50)]
        public string WeChatUnionId { get; set; }

        [StringLength(50)]
        public string AyondoUsername { get; set; }

        [StringLength(50)]
        public string AyondoPassword { get; set; }

        public long? AyondoAccountId { get; set; }

        public bool? AutoCloseAlert { get; set; }

        public string DeviceToken { get; set; }

        public int? DeviceType { get; set; }

        [StringLength(50)]
        public string AyLiveUsername { get; set; }

        [StringLength(100)]
        public string AyLivePassword { get; set; }

        public long? AyLiveAccountId { get; set; }

        public long? AyLiveBalanceId { get; set; }

        public long? AyLiveActorId { get; set; }

        [StringLength(50)]
        public string AyLiveAccountGuid { get; set; }

        public DateTime? AyLiveApplyAt { get; set; }

        [StringLength(50)]
        public string AyLiveAccountStatus { get; set; }

        [StringLength(50)]
        public string BankCardNumber { get; set; }
        [StringLength(50)]
        public string BankCardStatus { get; set; }

        [StringLength(50)]
        public string BankCardRejectReason { get; set; }

        [StringLength(50)]
        public string BankName { get; set; }
        [StringLength(50)]
        public string Branch { get; set; }
        [StringLength(50)]
        public string Province { get; set; }
        [StringLength(50)]
        public string City { get; set; }

        [StringLength(50)]
        public string ReferenceAccountGuid { get; set; }
        /// <summary>
        /// 何时提交的银行卡
        /// </summary>
        public DateTime? BankCardSubmitAt { get; set; }
        /// <summary>
        /// 何时Approve，或Reject
        /// </summary>
        public DateTime? BankCardApprovedAt { get; set; }

        public bool? AutoCloseAlert_Live { get; set; }

        public bool? IsOnLive { get; set; }

        /// <summary>
        /// Liver user rank
        /// </summary>
        public int? LiveRank { get; set; }

        /// <summary>
        /// show data in Profit List
        /// </summary>
        public bool? ShowData { get; set; }
        /// <summary>
        /// 显示持仓平仓数据，如果ShowData为False，此处必定为False
        /// 相当于ShowData的子开关
        /// </summary>
        public bool? ShowOpenCloseData { get; set; }
        public DateTime? AyLiveApproveAt { get; set; }

        /// <summary>
        /// 首日入金奖励是否点击过
        /// </summary>
        public bool? FirstDayClicked { get; set; }
        /// <summary>
        /// 拿到首日奖励的提示信息是否已经看过
        /// </summary>
        public bool? FirstDayRewarded { get; set; }

        /// <summary>
        /// 推广码
        /// </summary>
        public string PromotionCode { get; set; }

        /// <summary>
        /// 积分兑换奖品发货手机
        /// </summary>
        public string DeliveryPhone { get; set; }
        /// <summary>
        /// 积分兑换奖品发货地址
        /// </summary>
        public string DeliveryAddress { get; set; }

        public virtual ICollection<Bookmark> Bookmarks { get; set; }

        //[ForeignKey("Id")]
        //public virtual UserInfo UserInfo { get; set; }

        //渠道ID
        public int? ChannelID { get; set; }
        //活动ID
        public int? ActivityID { get; set; }

        /// <summary>
        /// culture string
        /// </summary>
        public string language { get; set; }

        public bool? ShowMyFeed { get; set; }
        public bool? ShowFollowingFeed { get; set; }
        public bool? ShowHeadlineFeed { get; set; }
    }
}
