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

        [StringLength(50)]
        public string AyLivePassword { get; set; }

        public long? AyLiveAccountId { get; set; }

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

        public bool? AutoCloseAlert_Live { get; set; }

        public bool? IsOnLive { get; set; }

        public virtual ICollection<Bookmark> Bookmarks { get; set; }
    }
}
