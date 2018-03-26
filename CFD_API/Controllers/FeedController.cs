using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using CFD_API.Caching;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/feed")]
    public class FeedController : CFDController
    {
        public FeedController(CFDEntities db) : base(db)
        {
        }

        [HttpGet]
        [Route("live/default")]
        [BasicAuth]
        public List<FeedDTO> GetDefaultFeeds(int count = 50, DateTime? newerThan = null, DateTime? olderThan =null)
        {
            var twoWeeksAgo = DateTimes.GetChinaToday().AddDays(-13);
            var twoWeeksAgoUtc = twoWeeksAgo.AddHours(-8);

            var rankedUsers =
                db.NewPositionHistory_live.Where(o => o.ClosedAt != null && o.ClosedAt >= twoWeeksAgoUtc)
                    .GroupBy(o => o.UserId)
                    .Select(g => new
                    {
                        id = g.Key.Value,
                        roi = g.Sum(p => p.PL.Value) / g.Sum(p => p.InvestUSD.Value),
                    })
                    .OrderByDescending(o => o.roi)
                    .Take(CFDGlobal.DEFAULT_PAGE_SIZE)
                    .ToList();

            //ranked user ids
            var feedUserIds = rankedUsers.Select(o => o.id).ToList();

            //var tryGetAuthUser = TryGetAuthUser();
            //if (tryGetAuthUser != null)
            //{
            var tryGetAuthUser = GetUser();
            ////remove me from ranked user ids
            //if (feedUserIds.Contains(tryGetAuthUser.Id))
            //        feedUserIds.Remove(tryGetAuthUser.Id);

                //following user ids
                var followingUserIds =
                    db.UserFollows.Where(o => o.UserId == tryGetAuthUser.Id).Select(o => o.FollowingId).ToList();

                feedUserIds = feedUserIds.Concat(followingUserIds).ToList();
            //}

            var users = db.Users.Where(o => feedUserIds.Contains(o.Id)).ToList();
            var feedShowOpenCloseDataUserIds = feedUserIds.Where(o =>
            {
                var user = users.FirstOrDefault(u => u.Id == o);
                if ((user.ShowData ?? CFDUsers.DEFAULT_SHOW_DATA) && (user.ShowOpenCloseData ?? CFDUsers.DEFAULT_SHOW_DATA))
                    return true;
                return false;
            }).ToList();

            //get open feeds
            var openFeedsWhereClause = db.NewPositionHistory_live.Where(o => feedShowOpenCloseDataUserIds.Contains(o.UserId.Value));
            if (olderThan != null) openFeedsWhereClause = openFeedsWhereClause.Where(o => o.CreateTime < olderThan);
            var openFeeds = openFeedsWhereClause
                .OrderByDescending(o => o.CreateTime).Take(count)
                .Select(o => new FeedDTO()
                {
                    user = new UserBaseDTO() { id = o.UserId.Value },
                    type = "open",
                    time = o.CreateTime.Value,
                    position =
                        new PositionBaseDTO() { id = o.Id.ToString(), invest = o.InvestUSD, leverage = o.Leverage, isLong = o.LongQty.HasValue },
                    security = new SecurityBaseDTO() { id = o.SecurityId.Value },
                })
                .ToList();

            //get close feeds
            var closeFeedsWhereClause = db.NewPositionHistory_live.Where(o => feedShowOpenCloseDataUserIds.Contains(o.UserId.Value) && o.ClosedAt != null);
            if (olderThan != null) closeFeedsWhereClause = closeFeedsWhereClause.Where(o => o.ClosedAt < olderThan);
            var closeFeeds = closeFeedsWhereClause
                .OrderByDescending(o => o.ClosedAt).Take(count)
                .Select(o => new FeedDTO()
                {
                    user = new UserBaseDTO() { id = o.UserId.Value },
                    type = "close",
                    time = o.ClosedAt.Value,
                    position = new PositionBaseDTO() { id = o.Id.ToString(), roi = o.PL / o.InvestUSD, isLong = o.LongQty.HasValue },
                    security = new SecurityBaseDTO() { id = o.SecurityId.Value },
                })
                .ToList();

            //get status feeds
            var statusFeedsWhereClause = db.Trends.Where(o => feedUserIds.Contains(o.UserID));
            if (olderThan != null) statusFeedsWhereClause = statusFeedsWhereClause.Where(o => o.CreatedAt < olderThan);
            var statusFeeds = statusFeedsWhereClause
                .OrderByDescending(o => o.CreatedAt).Take(count)
                .Select(o => new FeedDTO()
                {
                    user = new UserBaseDTO() { id = o.UserID },
                    type = "status",
                    time = o.CreatedAt,
                    status = o.Message,
                })
                .ToList();

            //get system feed
            var languages = Translator.GetLanguageByCulture();
            var systemFeedsWhereClause = db.Headlines.Where(o => o.Expiration.Value == SqlDateTime.MaxValue.Value && languages.Contains(o.Language));
            if (olderThan != null) systemFeedsWhereClause = systemFeedsWhereClause.Where(o => o.CreatedAt < olderThan);
            var systemFeeds = systemFeedsWhereClause
                .OrderByDescending(o => o.CreatedAt).Take(count)
                .Select(o => new FeedDTO()
                {
                    user = new UserBaseDTO() { picUrl = CFDGlobal.USER_PIC_BLOB_CONTAINER_URL+"11.jpg",nickname = "【热点】"},
                    type = "system",
                    time = o.CreatedAt.Value,
                    status = o.Body,
                })
                .ToList();

            //concat results
            var @resultEnumerable = openFeeds.Concat(closeFeeds).Concat(statusFeeds).Concat(systemFeeds);

            //filter by time param
            if (newerThan != null)
                @resultEnumerable = @resultEnumerable.Where(o => o.time > newerThan.Value);

            var result = @resultEnumerable.OrderByDescending(o => o.time).Take(count).ToList();

            //populate user/security info
            var prods = WebCache.Live.ProdDefs;
            foreach (var feedDto in result)
            {
                if (feedDto.user != null && feedDto.user.id != null)
                {
                    var user = users.FirstOrDefault(o => o.Id == feedDto.user.id);
                    feedDto.user.nickname = user.Nickname;
                    feedDto.user.picUrl = user.PicUrl;

                    feedDto.isRankedUser = rankedUsers.Any(o => o.id == feedDto.user.id);
                }

                if (feedDto.security != null)
                    feedDto.security.name =
                        Translator.GetProductNameByThreadCulture(
                            prods.FirstOrDefault(o => o.Id == feedDto.security.id).Name);
            }

            return result;
        }
    }
}
