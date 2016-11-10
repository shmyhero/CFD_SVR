using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("LikeHistory")]
    public class LikeHistory
    {
        [Key]
        [Column(Order = 0)]
        public int Id { get; set; }

        public int UserId { get; set; }

        public int UserCardId { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
