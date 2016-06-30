namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("PhoneSignupHistory")]
    public partial class PhoneSignupHistory
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Phone { get; set; }

        public DateTime CreateAt { get; set; }
    }
}
