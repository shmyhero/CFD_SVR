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

        public DateTime? TokenCreatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        [StringLength(50)]
        public string WeChatOpenId { get; set; }

        [StringLength(200)]
        public string WeChatToken { get; set; }

        [StringLength(50)]
        public string WeChatUnionId { get; set; }

        [StringLength(50)]
        public string WeChatNickname { get; set; }
    }
}
