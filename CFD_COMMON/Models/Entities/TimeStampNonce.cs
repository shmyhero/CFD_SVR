namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("TimeStampNonce")]
    public partial class TimeStampNonce
    {
        [Key]
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long TimeStamp { get; set; }

        [Key]
        [Column(Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Nonce { get; set; }

        public int? UserID { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? Expiration { get; set; }

        [StringLength(10)]
        public string CaptchaCode { get; set; }

        [Column(TypeName = "ntext")]
        public string CaptchaImg { get; set; }

        public int? CaptchaAttemps { get; set; }
    }
}
