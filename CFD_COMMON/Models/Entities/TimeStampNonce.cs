using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("TimeStampNonce")]
    public class TimeStampNonce
    {
        [Key, Column(Order = 0)]
        public long TimeStamp { get; set; }
        [Key, Column(Order = 1)]
        public int Nonce { get; set; }

        public int UserID { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? Expiration { get; set; }
    }
}
