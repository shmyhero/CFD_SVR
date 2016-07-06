namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("ApiHit")]
    public partial class ApiHit
    {
        public int Id { get; set; }

        [StringLength(50)]
        public string Ip { get; set; }

        [StringLength(200)]
        public string Url { get; set; }

        [StringLength(20)]
        public string HttpMethod { get; set; }

        [StringLength(200)]
        public string ApiName { get; set; }

        [StringLength(200)]
        public string Param { get; set; }

        public int? UserId { get; set; }

        public DateTime? HitAt { get; set; }

        public double? TimeSpent { get; set; }

        public bool? IsException { get; set; }
    }
}
