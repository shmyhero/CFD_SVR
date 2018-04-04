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

            //same data as 2 weeks ranked users
            var rankedUsers =
                db.NewPositionHistory_live.Where(o => o.ClosedAt != null && o.ClosedAt >= twoWeeksAgoUtc)
                    .GroupBy(o => o.UserId)
                    .Select(g => new
                    {
                        id = g.Key.Value,
                        roi = g.Sum(p => p.PL.Value) / g.Sum(p => p.InvestUSD.Value),
                    })
                    .OrderByDescending(o => o.roi)
                    .Where(o => o.roi > 0)
                    .Take(CFDGlobal.DEFAULT_PAGE_SIZE)
                    .ToList();

            ////ranked user ids
            //var feedUserIds = rankedUsers.Select(o => o.id).ToList();

            //var tryGetAuthUser = TryGetAuthUser();
            //if (tryGetAuthUser != null)
            //{
            var tryGetAuthUser = GetUser();
            ////remove me from ranked user ids
            //if (feedUserIds.Contains(tryGetAuthUser.Id))
            //        feedUserIds.Remove(tryGetAuthUser.Id);

                //following user ids
                var feedUserIds =
                    db.UserFollows.Where(o => o.UserId == tryGetAuthUser.Id).Select(o => o.FollowingId).ToList();

            //feedUserIds = feedUserIds.Concat(followingUserIds).ToList();
            //}

            //and myself
            feedUserIds.Add(UserId);

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
            var languages = Translator.GetCultureNamesByThreadCulture();
            var systemFeedsWhereClause = db.Headlines.Where(o => o.Expiration.Value == SqlDateTime.MaxValue.Value && languages.Contains(o.Language));
            if (olderThan != null) systemFeedsWhereClause = systemFeedsWhereClause.Where(o => o.CreatedAt < olderThan);
            var systemFeeds = systemFeedsWhereClause
                .OrderByDescending(o => o.CreatedAt).Take(count)
                .Select(o => new FeedDTO()
                {
                    user =
                        new UserBaseDTO()
                        {
                            picUrl = CFDGlobal.USER_PIC_BLOB_CONTAINER_URL + "system1.png",
                            nickname = "盈交易官方",
                        },
                    type = "system",
                    time = o.CreatedAt.Value,
                    body = o.Body,
                    title = o.Header,
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
            
            //populate cards info for close feeds
            var closePositionIds = result.Where(o=>o.type=="close").Select(o => Convert.ToInt64(o.position.id)).ToList();
            if (closePositionIds.Count > 0)
            {
                var posCards = db.UserCards_Live.Where(o => closePositionIds.Contains(o.PositionId)).ToList();
                if (posCards.Count > 0)
                {
                    var posCardIds = posCards.Select(o => o.CardId).ToList();
                    var cardDefs = db.Cards.Where(o => posCardIds.Contains(o.Id)).ToList();
                    foreach (var feedDto in result)
                    {
                        if (feedDto.type == "close")
                        {
                            var card = posCards.FirstOrDefault(o => o.PositionId.ToString() == feedDto.position.id);
                            if(card==null) continue;

                            var cardDef = cardDefs.FirstOrDefault(o => o.Id == card.CardId);

                            var prodDef = prods.FirstOrDefault(o => o.Id == card.SecurityId);

                            feedDto.position.card = new CardDTO()
                            {
                                cardId = card.Id,
                                //ccy = u.CCY,
                                imgUrlBig = cardDef.CardImgUrlBig,
                                imgUrlMiddle = cardDef.CardImgUrlMiddle,
                                imgUrlSmall = cardDef.CardImgUrlSmall,
                                invest = card.Invest,
                                isLong = card.IsLong,
                                isNew = !card.IsNew.HasValue ? true : card.IsNew.Value,
                                leverage = card.Leverage,
                                likes = card.Likes,
                                reward = cardDef.Reward,
                                settlePrice = card.SettlePrice,
                                stockID = card.SecurityId,
                                stockName = Translator.GetProductNameByThreadCulture(prodDef.Name),
                                pl = card.PL,
                                plRate =
                                    ((card.SettlePrice - card.TradePrice)/card.TradePrice*card.Leverage*100)*
                                    (card.IsLong.Value ? 1 : -1),
                                themeColor = cardDef.ThemeColor,
                                title = cardDef.Title,
                                cardType = cardDef.CardType.HasValue ? cardDef.CardType.Value : 0,
                                tradePrice = card.TradePrice,
                                tradeTime = card.ClosedAt,
                                //userName = us.Nickname,
                                //profileUrl = us.PicUrl,
                                liked = false
                            };
                        }
                    }
                }
            }

            return result;
        }
    }
}
