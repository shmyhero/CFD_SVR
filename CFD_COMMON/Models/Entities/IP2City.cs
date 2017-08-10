namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class IP2City
    {
        [Key]
        [Column(Order = 0)]
        [MaxLength(16)]
        public byte[] StartAddress { get; set; }

        [Key]
        [Column(Order = 1)]
        [MaxLength(16)]
        public byte[] EndAddress { get; set; }

        [StringLength(2)]
        public string CountryCode { get; set; }

        [StringLength(200)]
        public string Province { get; set; }

        [StringLength(200)]
        public string City { get; set; }
    }
}
