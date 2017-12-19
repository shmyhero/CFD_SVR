using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class TrendDTO
    {
        public int id;
        public string message;
        public int likes;
        public int rewardCount;
        /// <summary>
        /// 是否已点过赞
        /// </summary>
        public bool Liked;
        public DateTime? createdAt;
    }

    /// <summary>
    /// createdAt是string类型
    /// </summary>
    public class TrendDTOV2
    {
        public int id;
        public string message;
        public int likes;
        public int rewardCount;
        /// <summary>
        /// 是否已点过赞
        /// </summary>
        public bool Liked;
        public string createdAt;
    }
}