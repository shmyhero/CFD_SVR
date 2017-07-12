using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Models.Entities
{
    /// <summary>
    /// 合伙人
    /// </summary>
    [Table("Partner")]
    public class Partner
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Province { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        /// <summary>
        /// 在线推广-网站、电子邮件、社交媒体、
        /// </summary>
        public bool? PromotionType1 { get; set; }
        /// <summary>
        /// 在线推广-与客户见面、进行讲座和课程
        /// </summary>
        public bool? PromotionType2 { get; set; }

        /// <summary>
        /// 上级推荐码，一级合作人此字段为空
        /// </summary>
        public string ParentCode { get; set; }

        /// <summary>
        /// 推荐码
        /// </summary>
        public string PromotionCode { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
