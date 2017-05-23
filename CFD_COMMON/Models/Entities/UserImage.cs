using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("UserImage")]
    public class UserImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int UserId { get; set; }

        [Column(TypeName = "ntext")]
        public string IdFrontImg { get; set; }

        [StringLength(10)]
        public string IdFrontImgExt { get; set; }

        [Column(TypeName = "ntext")]
        public string IdBackImg { get; set; }

        [StringLength(10)]
        public string IdBackImgExt { get; set; }

        [Column(TypeName = "ntext")]
        public string OcrFaceImg { get; set; }

        /// <summary>
        /// 地址证明的第一张图(户口本开户人页)
        /// </summary>
        [Column(TypeName = "ntext")]
        public string ProofOfAddress { get; set; }

        /// <summary>
        /// 地址证明的第二张图(户口本户主页)
        /// </summary>
        [Column(TypeName = "ntext")]
        public string ProofOfAddressII { get; set; }
    }
}
