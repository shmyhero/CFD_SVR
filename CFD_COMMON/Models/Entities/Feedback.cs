namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Feedback")]
    public partial class Feedback
    {
        public int Id { get; set; }

        [StringLength(20)]
        public string Phone { get; set; }

        [StringLength(1000)]
        public string Text { get; set; }

        public DateTime? Time { get; set; }
    }
}
