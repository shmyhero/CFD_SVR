using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("LiveRank")]
    public class LiveRank
    {
        public int Id { get; set; }
        public int Rank { get; set; }
        public string Description { get; set; }
    }
}
