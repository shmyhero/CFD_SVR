namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Version")]
    public partial class Version
    {
        [Key]
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int IOSLatestInt { get; set; }

        [StringLength(20)]
        public string IOSLatestStr { get; set; }

        public int? AndroidLatestInt { get; set; }

        [Key]
        [Column(Order = 1)]
        [StringLength(20)]
        public string AndroidLatestStr { get; set; }
    }
}
