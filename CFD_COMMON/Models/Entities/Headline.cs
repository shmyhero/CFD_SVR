namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Headline")]
    public partial class Headline
    {
        public int Id { get; set; }

        [StringLength(200)]
        public string Header { get; set; }

        [StringLength(200)]
        public string Body { get; set; }

        [StringLength(200)]
        public string CreatedBy { get; set; }

        public DateTime? CreatedAt { get; set; }

        [StringLength(200)]
        public string ExpiredBy { get; set; }

        public DateTime? Expiration { get; set; }
    }
}
