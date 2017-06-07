using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 运营需要记录申请交易金的用户手机号
    /// </summary>
    [Table("RewardPhoneHistory")]
    public class RewardPhoneHistory
    {
        public int ID { get; set; }

        public string Phone { get; set; }

        public int ChannelID { get; set; }

        public string ChannelName { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
