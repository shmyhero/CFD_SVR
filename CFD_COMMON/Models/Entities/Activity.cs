using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("Activity")]
    public class Activity
    {
        public int ID { get; set; }
        public int ActivityID { get; set; }

        public string Name { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
    }
}
