using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class ScoreDTO
    {
        /// <summary>
        /// 累计获得积分
        /// </summary>
        public int total { get; set; }
        /// <summary>
        /// 剩余积分
        /// </summary>
        public int remaining { get; set; }
        /// <summary>
        /// 实盘下单积分
        /// </summary>
        public int liveOrder { get; set; }
        /// <summary>
        /// 点赞
        /// </summary>
        public int like { get; set; }
        /// <summary>
        /// 分享
        /// </summary>
        public int share { get; set; }
    }

    public class ScoreSetting
    {
        /// <summary>
        /// 点赞得分
        /// </summary>
        public int Like { get; set; }

        /// <summary>
        /// 被点赞得分
        /// </summary>
        public int Liked { get; set; }

        /// <summary>
        /// 首页分享得分
        /// </summary>
        public int AppShare { get; set; }

        /// <summary>
        /// 微信好友分享得分
        /// </summary>
        public int WechatFriend { get; set; }
        /// <summary>
        /// 微信朋友圈分享得分
        /// </summary>
        public int WechatCircle { get; set; }

        /// <summary>
        /// 实盘下单获得积分的百分比
        /// </summary>
        public decimal LiveOrder { get; set; }
    }

    /// <summary>
    /// 分享类型
    /// </summary>
    public enum ShareType
    {
        /// <summary>
        /// 分享到App首页
        /// </summary>
        App = 1,
        /// <summary>
        /// 分享到微信好友
        /// </summary>
        WechatFriend = 2,
        /// <summary>
        /// 朋友圈
        /// </summary>
        WechatCircle = 3,
    }

    /// <summary>
    /// 积分来源
    /// </summary>
    public struct ScoreSource
    {
        /// <summary>
        /// 点赞
        /// </summary>
        public const string Like = "Like";
        /// <summary>
        /// 被点赞
        /// </summary>
        public const string Liked = "Liked";
        /// <summary>
        /// App首页分享
        /// </summary>
        public const string AppShare = "AppShare";
        /// <summary>
        /// 微信好友
        /// </summary>
        public const string WechatFriend = "WechatFriend";
        /// <summary>
        /// 朋友圈
        /// </summary>
        public const string WechatCircle = "WechatCircle";
        /// <summary>
        /// 实盘下单
        /// </summary>
        public const string LiveOrder = "LiveOrder";
    }
}