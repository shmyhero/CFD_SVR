using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Script.Serialization;

namespace CardService.Controller
{
    [RoutePrefix("api/card")]
    public class CardController : ApiController
    {
        /// <summary>
        /// 目前卡片只在Demo环境有
        /// </summary>
        private const bool isLive = false;

        [Route("hello/{id}")]
        [HttpGet]
        public string Hello(int id)
        {
            return "hello: " + id.ToString();
        }

        [HttpPost]
        [Route("")]
        public HttpResponseMessage PostCard()
        {
            string requestJson = Request.Content.ReadAsStringAsync().Result;
            //该变量表示Ayondo的PositionReport的集合(通常要么是开仓、要么是平仓，分开表示)
            var tradeList = ((new JavaScriptSerializer() { MaxJsonLength = Int32.MaxValue }).Deserialize(requestJson, typeof(List<AyondoTradeHistoryBase>))) as List<AyondoTradeHistoryBase>;
            if (tradeList == null || tradeList.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.OK);
            }

            List<long> ayondoAccountIds = tradeList.Where(o => o.AccountId.HasValue).Select(o => o.AccountId.Value).Distinct().ToList();

            using (var db = new CFDEntities(ConfigurationManager.ConnectionStrings["CFDEntities"].ConnectionString))
            {
                var query = from u in db.Users
                            where ayondoAccountIds.Contains(isLive ? u.AyLiveAccountId.Value : u.AyondoAccountId.Value) //&& u.AutoCloseAlert.HasValue && u.AutoCloseAlert.Value
                            select new { UserId = u.Id, u.AyondoAccountId, u.AyLiveAccountId, u.AutoCloseAlert_Live, u.IsOnLive };

                var users = query.ToList();

                var allCards = db.Cards.Where(item => item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).ToList();
                List<long> posIDList = tradeList.Select(o => o.PositionId.Value).ToList();
                var cardService = new CFD_COMMON.Service.CardService(db);

                IQueryable<NewPositionHistoryBase> positionHistoryQuery = null;
                if (isLive)
                {
                    positionHistoryQuery = from n in db.NewPositionHistory_live
                                           where posIDList.Contains(n.Id)
                                           select new NewPositionHistoryBase() { Id = n.Id, LongQty = n.LongQty, ShortQty = n.ShortQty, Leverage = n.Leverage, SettlePrice = n.SettlePrice, ClosedPrice = n.ClosedPrice, InvestUSD = n.InvestUSD, ClosedAt = n.ClosedAt, CreateTime = n.CreateTime, PL = n.PL, SecurityId = n.SecurityId, UserId = n.UserId };
                }
                else
                {
                    positionHistoryQuery = from n in db.NewPositionHistories
                                           where posIDList.Contains(n.Id)
                                           select new NewPositionHistoryBase() { Id = n.Id, LongQty = n.LongQty, ShortQty = n.ShortQty, Leverage = n.Leverage, SettlePrice = n.SettlePrice, ClosedPrice = n.ClosedPrice, InvestUSD = n.InvestUSD, ClosedAt = n.ClosedAt, CreateTime = n.CreateTime, PL = n.PL, SecurityId = n.SecurityId, UserId = n.UserId };
                }
                //该变量标识由PositionReport组合起来的一笔交易(包括开仓、平仓信息，在一条记录里面)
                List<NewPositionHistoryBase> positionHistoryList = positionHistoryQuery.ToList();

                foreach (var trade in tradeList)
                {
                    if (!trade.PositionId.HasValue)
                        continue;

                    var positionHistory = positionHistoryList.FirstOrDefault(o => o.Id == trade.PositionId);
                    if (positionHistory == null)
                        continue;

                    var user = users.FirstOrDefault(o => (isLive ? o.AyLiveAccountId : o.AyondoAccountId) == trade.AccountId);
                    if (user == null) continue;

                    if (!isLive)//目前只在Demo有卡片
                    {
                        var plRatePercent = positionHistory.LongQty.HasValue
                                                        ? (trade.TradePrice - positionHistory.SettlePrice) / positionHistory.SettlePrice * positionHistory.Leverage * 100
                                                        : (positionHistory.SettlePrice - trade.TradePrice) / positionHistory.SettlePrice * positionHistory.Leverage * 100;

                        var card = cardService.GetCard(trade.PL.Value, plRatePercent.Value, allCards);
                        if (card != null)
                        {
                            UserCard uc = new UserCard()
                            {
                                UserId = user.UserId,
                                CardId = card.Id,
                                ClosedAt = trade.TradeTime,
                                CreatedAt = DateTime.UtcNow,
                                Expiration = SqlDateTime.MaxValue.Value,
                                Invest = positionHistory.InvestUSD,
                                PositionId = trade.PositionId.Value,
                                IsLong = positionHistory.LongQty.HasValue,
                                Leverage = positionHistory.Leverage,
                                Likes = 0,
                                SecurityId = trade.SecurityId,
                                PL = trade.PL,
                                Qty = trade.Quantity,
                                Reward = card.Reward,
                                SettlePrice = trade.TradePrice,
                                TradePrice = positionHistory.SettlePrice,
                                TradeTime = positionHistory.CreateTime,
                                IsNew = false,
                                IsShared = false,
                                IsPaid = false
                            };
                            db.UserCards.Add(uc);
                            db.SaveChanges();
                        }
                    }
                }

                  
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }
    }
}
