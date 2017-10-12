namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("HitIP")]
    public partial class HitIP
    {
        public int hitcount { get; set; }
        [Key]
        public int userid { get; set; }
        [StringLength(50)]
        public string ip { get; set; }
    }
}
