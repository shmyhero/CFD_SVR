namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Bookmark")]
    public partial class Bookmark
    {
        [Key]
        [Column(Order = 0)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int UserId { get; set; }

        [Key]
        [Column(Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int AyondoSecurityId { get; set; }

        //public DateTime? CreatedAt { get; set; }
        public int? DisplayOrder { get; set; }

        //public virtual AyondoSecurity AyondoSecurity { get; set; }
        public virtual User User { get; set; }
    }
}
