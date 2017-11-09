using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    [Table("Channel")]
    public class Channel
    {
        public int ID { get; set; }
        public int ChannelID { get; set; }

        public string ChannelName { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
    }
}
