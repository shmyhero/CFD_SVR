namespace CFD_COMMON.Models.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Device")]
    public partial class Device
    {
        public int Id { get; set; }
        public int? userId { get; set; }
        public string deviceToken { get; set; }
        public int deviceType { get; set; }
        public DateTime? UpdateTime { get; set; }

        public virtual User User { get; set; }
    }
}
