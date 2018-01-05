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

        public string Body { get; set; }

        public string ImgUrl { get; set; }

        public int? Color { get; set; }

        public string Language { get; set; }

        [StringLength(200)]
        public string CreatedBy { get; set; }

        public DateTime? CreatedAt { get; set; }

        [StringLength(200)]
        public string ExpiredBy { get; set; }

        public DateTime? Expiration { get; set; }
    }
}
