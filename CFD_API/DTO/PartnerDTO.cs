using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class PartnerDTO
    {
        public string name { get; set; }
        public string province { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        /// <summary>
        /// 在线推广-网站、电子邮件、社交媒体、
        /// </summary>
        public bool? promotionType1 { get; set; }
        /// <summary>
        /// 在线推广-与客户见面、进行讲座和课程
        /// </summary>
        public bool? promotionType2 { get; set; }
        /// <summary>
        /// 上级推广码
        /// </summary>
        public string parentCode { get; set; }
        /// <summary>
        /// 推广码
        /// </summary>
        public string promotionCode { get; set; }
        public DateTime? createdAt { get; set; }
    }

    public class PartnerLoginDTO
    {
        public string phone { get; set; }
        public string verifyCode { get; set; }
    }

    public class PartnerSignUpDTO
    {
        public string name { get; set; }
        public string province { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public string verifyCode { get; set; }
        /// <summary>
        /// 上级合伙人的推荐码
        /// </summary>
        public string promotionCode { get; set; }
        /// <summary>
        /// 在线推广-网站、电子邮件、社交媒体、
        /// </summary>
        public bool? promotionType1 { get; set; }
        /// <summary>
        /// 在线推广-与客户见面、进行讲座和课程
        /// </summary>
        public bool? promotionType2 { get; set; }

    }
}