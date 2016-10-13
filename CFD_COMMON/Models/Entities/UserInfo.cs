namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("UserInfo")]
    public partial class UserInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int UserId { get; set; }

        [StringLength(10)]
        public string OcrRealName { get; set; }

        [StringLength(20)]
        public string OcrIdCode { get; set; }

        [StringLength(50)]
        public string OcrAddr { get; set; }

        public bool? OcrGender { get; set; }

        [StringLength(10)]
        public string OcrEthnic { get; set; }

        [Column(TypeName = "ntext")]
        public string OcrFaceImg { get; set; }

        [StringLength(50)]
        public string OcrIssueAuth { get; set; }

        [StringLength(50)]
        public string OcrValidPeriod { get; set; }

        [StringLength(50)]
        public string OcrTransId { get; set; }
    }
}
