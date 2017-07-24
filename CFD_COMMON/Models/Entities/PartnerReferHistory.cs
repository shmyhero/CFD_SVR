using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 合伙人的好友推荐历史纪录
    /// </summary>
    [Table("PartnerReferHistory")]
    public class PartnerReferHistory
    {
        public int ID { get; set; }
        /// <summary>
        /// 推荐人手机号
        /// </summary>
        public string RefereePhone { get; set; }
      
        /// <summary>
        /// 被推荐人手机号
        /// </summary>
        public string FriendPhone { get; set; }

        /// <summary>
        /// 是否已经被奖励过。 被推荐人入金之后奖励推荐人。
        /// </summary>
        public bool? IsRewarded { get; set; }

        public DateTime? RewardedAt { get; set; }

        public DateTime CreatedAt { get; set; }

    }
}
