using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using AutoMapper;
using CFD_API.Caching;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using CFD_COMMON.Localization;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/card")]
    public class CardController : CFDController
    {
        public CardController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        /// <summary>
        /// 个人卡片中心
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("my")]
        [BasicAuth]
        public CardCollectionDTO GetMyCards()
        {
            CardCollectionDTO coll = new CardCollectionDTO();

            //这里的cardId用UserCard的ID，而不是Card.ID
            var myCards = from u in db.UserCards_Live
                          join c in db.Cards on u.CardId equals c.Id
                          into x
                          from y in x.DefaultIfEmpty()
                          where u.UserId == this.UserId
                          orderby u.CreatedAt descending
                          select new CardDTO()
                          {
                              cardId = u.Id,
                              //ccy = u.CCY,
                              imgUrlBig = y.CardImgUrlBig,
                              imgUrlMiddle = y.CardImgUrlMiddle,
                              imgUrlSmall = y.CardImgUrlSmall,
                              invest = u.Invest,
                              isLong = u.IsLong,
                              isNew = !u.IsNew.HasValue ? true : u.IsNew.Value,
                              shared = !u.IsShared.HasValue ? false : u.IsShared.Value,
                              leverage = u.Leverage,
                              likes = u.Likes,
                              reward = y.Reward,
                              settlePrice = u.SettlePrice,
                              //stockName = u.StockName,
                              stockID = u.SecurityId,
                              pl = u.PL,
                              plRate = ((u.SettlePrice - u.TradePrice) / u.TradePrice * u.Leverage * 100) * (u.IsLong.Value ? 1 : -1),
                              themeColor = y.ThemeColor,
                              tradePrice = u.TradePrice,
                              tradeTime = u.ClosedAt
                          };

            if (myCards != null)
            {
                var cache = WebCache.GetInstance(true);
                coll.cards = myCards.ToList();
                coll.cards.ForEach(cardDTO => {
                    var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == cardDTO.stockID);
                    if (prodDef != null)
                    {
                        cardDTO.ccy = prodDef.Ccy2;
                        cardDTO.stockName = Translator.GetCName(prodDef.Name);
                    }
                });
                coll.hasNew = coll.cards.Any(item => !item.isNew.HasValue || !item.isNew.Value);
            }
            else
            {
                coll.hasNew = false;
            }

            return coll;
        }

        [HttpGet]
        [Route("{id}")]
        public CardDTO GetCard(int id)
        {
            var cardDTO = (from u in db.UserCards_Live
                           join c in db.Cards on u.CardId equals c.Id
                           into x
                           from y in x.DefaultIfEmpty()
                           where u.Id == id
                           select new CardDTO()
                           {
                               cardId = u.Id,
                               //ccy = u.CCY,
                               imgUrlBig = y.CardImgUrlBig,
                               imgUrlMiddle = y.CardImgUrlMiddle,
                               imgUrlSmall = y.CardImgUrlSmall,
                               invest = u.Invest,
                               isLong = u.IsLong,
                               isNew = !u.IsNew.HasValue ? true : u.IsNew.Value,
                               shared = !u.IsShared.HasValue ? false : u.IsShared.Value,
                               leverage = u.Leverage,
                               likes = u.Likes,
                               reward = y.Reward,
                               settlePrice = u.SettlePrice,
                               stockID = u.SecurityId,
                               //stockName = u.StockName,
                               themeColor = y.ThemeColor,
                               tradePrice = u.TradePrice,
                               tradeTime = u.ClosedAt,
                               pl = u.PL,
                               plRate = ((u.SettlePrice - u.TradePrice) / u.TradePrice * u.Leverage * 100) * (u.IsLong.Value ? 1 : -1)
                           }).FirstOrDefault();

            if (cardDTO == null)
                return null;
            //实盘才有卡片
            var cache = WebCache.GetInstance(true);
            var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == cardDTO.stockID);
            if(prodDef != null)
            {
                cardDTO.ccy = prodDef.Ccy2;
                cardDTO.stockName = Translator.GetCName(prodDef.Name);
            }

            var authUserId = GetAuthUserId();

            if (authUserId!=0 && cardDTO != null && cardDTO.isNew.Value)
            {
                UserCard_Live card = db.UserCards_Live.Where(item => item.Id == id).FirstOrDefault();
                if (card != null)
                {
                    card.IsNew = false;
                    db.SaveChanges();
                }
            }

            return cardDTO;
        }

        [HttpGet]
        [Route("share/{id}")]
        [BasicAuth]
        public ResultDTO ShareCard(int id)
        {
            UserCard_Live uc = db.UserCards_Live.Where(o => o.Id == id && o.UserId == this.UserId).FirstOrDefault();
            if (uc != null)
            {
                uc.IsShared = true;
                db.SaveChanges();
            }
            else
            {
                return new ResultDTO(false);
            }

            return new ResultDTO(true);
        }

        [HttpGet]
        [Route("like/{id}")]
        [BasicAuth]
        public ResultDTO LikeCard(int id)
        {
            //todo: use TransactionScope

            if (db.LikeHistories.Any(o => o.UserId == this.UserId && o.UserCardId == id))
            {
                return new ResultDTO(false) { message = "您已赞过该卡片" };
            }

            UserCard_Live uc = db.UserCards_Live.Where(o => o.Id == id).FirstOrDefault();
            if (uc != null)
            {
                uc.Likes = uc.Likes.HasValue ? uc.Likes + 1 : 1;

                LikeHistory history = new LikeHistory() { UserCardId = uc.Id, UserId = this.UserId, CreatedAt = DateTime.UtcNow };
                db.LikeHistories.Add(history);

                db.SaveChanges();
            }
            else
            {
                return new ResultDTO(false);
            }

            return new ResultDTO(true);
        }

        /// <summary>
        /// 无验证信息或验证信息错误时返回0，否则返回UserID
        /// </summary>
        /// <returns></returns>
        private int GetAuthUserId()
        {
            if (HttpContext.Current.Request.Headers.AllKeys.Contains("Authorization"))
            {
                string auth = HttpContext.Current.Request.Headers["Authorization"];
                var authArray = auth.Split(' ');
                if (authArray.Length != 2)
                {
                    return 0;
                }

                var tokenArray = authArray[1].Split('_');
                if (tokenArray.Length != 2)
                {
                    return 0;
                }

                string userIdStr = tokenArray[0];
                int userId = 0;
                int.TryParse(userIdStr, out userId);

                return userId;
            }

            return 0;
        }

        /// <summary>
        /// 如果经过身份认证，则根据用户身份去DB获取相关信息
        /// 如果没有经过身份认证，则默认Liked=false. 前端会在点赞的时候做判断。
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("top")]
        public List<CardDTO> GetTopCards()
        {
            int userId = GetAuthUserId();
            List<CardDTO> topCards = null;

            if (userId == 0)
            {
                topCards = (from u in db.UserCards_Live
                            join c in db.Cards on u.CardId equals c.Id
                            into x
                            from y in x.DefaultIfEmpty()
                            join us in db.Users on u.UserId equals us.Id
                            where u.IsShared.HasValue && u.IsShared.Value
                            orderby u.ClosedAt descending, y.CardType descending
                            select new CardDTO()
                            {
                                cardId = u.Id,
                                //ccy = u.CCY,
                                imgUrlBig = y.CardImgUrlBig,
                                imgUrlMiddle = y.CardImgUrlMiddle,
                                imgUrlSmall = y.CardImgUrlSmall,
                                invest = u.Invest,
                                isLong = u.IsLong,
                                isNew = !u.IsNew.HasValue ? true : u.IsNew.Value,
                                leverage = u.Leverage,
                                likes = u.Likes,
                                reward = y.Reward,
                                settlePrice = u.SettlePrice,
                                stockID = u.SecurityId,
                                //stockName = u.StockName,
                                pl = u.PL,
                                plRate = ((u.SettlePrice - u.TradePrice) / u.TradePrice * u.Leverage * 100) * (u.IsLong.Value ? 1 : -1),
                                themeColor = y.ThemeColor,
                                tradePrice = u.TradePrice,
                                tradeTime = u.ClosedAt,
                                userName = us.Nickname,
                                profileUrl = us.PicUrl,
                                liked = false
                            }).Take(6).ToList();
            }
            else
            {
                topCards = (from u in db.UserCards_Live
                            join c in db.Cards on u.CardId equals c.Id
                            into x
                            from y in x.DefaultIfEmpty()
                            join us in db.Users on u.UserId equals us.Id
                            where u.IsShared.HasValue && u.IsShared.Value
                            orderby u.ClosedAt descending, y.CardType descending
                            select new CardDTO()
                            {
                                cardId = u.Id,
                                //ccy = u.CCY,
                                imgUrlBig = y.CardImgUrlBig,
                                imgUrlMiddle = y.CardImgUrlMiddle,
                                imgUrlSmall = y.CardImgUrlSmall,
                                invest = u.Invest,
                                isLong = u.IsLong,
                                isNew = !u.IsNew.HasValue ? true : u.IsNew.Value,
                                leverage = u.Leverage,
                                likes = u.Likes,
                                reward = y.Reward,
                                settlePrice = u.SettlePrice,
                                stockID = u.SecurityId,
                                //stockName = u.StockName,
                                pl = u.PL,
                                plRate = ((u.SettlePrice - u.TradePrice) / u.TradePrice * u.Leverage * 100) * (u.IsLong.Value ? 1 : -1),
                                themeColor = y.ThemeColor,
                                tradePrice = u.TradePrice,
                                tradeTime = u.ClosedAt,
                                userName = us.Nickname,
                                profileUrl = us.PicUrl,
                                liked = db.LikeHistories.Any(o => o.UserId == userId && o.UserCardId == u.Id)
                            }).Take(6).ToList();
            }
            //实盘才有卡片
            var cache = WebCache.GetInstance(true);
        
            topCards.ForEach(cardDTO =>
            {
                var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == cardDTO.stockID);
                if(prodDef != null)
                {
                    cardDTO.ccy = prodDef.Ccy2;
                    cardDTO.stockName = Translator.GetCName(prodDef.Name);
                }
            });

            int count = topCards.Count();
            if (count < 3) //优先补黄金
            {
                //从实盘的产品列表中取黄金
                var prodDef = WebCache.GetInstance(true).ProdDefs.FirstOrDefault(o => o.Id == 35990);
                if (prodDef != null)
                {
                    decimal last = GetLastPrice(prodDef);
                    topCards.Add(new CardDTO()
                    {
                        ccy = prodDef.Ccy2,
                        last = last,
                        preClose = Quotes.GetClosePrice(prodDef),
                        rate = (last - Quotes.GetClosePrice(prodDef)) / Quotes.GetClosePrice(prodDef) *100,
                        stockName = "黄金",
                        stockID = 35990
                    });
                }

                if (count < 2) //再补白银
                {
                    prodDef = WebCache.GetInstance(true).ProdDefs.FirstOrDefault(o => o.Id == 35996);
                    if (prodDef != null)
                    {
                        decimal last = GetLastPrice(prodDef);
                        topCards.Add(new CardDTO()
                        {
                            ccy = prodDef.Ccy2,
                            last = last,
                            preClose = Quotes.GetClosePrice(prodDef),
                            rate = (last - Quotes.GetClosePrice(prodDef)) / Quotes.GetClosePrice(prodDef) * 100,
                            stockName = "白银",
                            stockID = 35996
                        });
                    }
                }
            }

            return topCards.ToList();
        }

        private decimal GetLastPrice(ProdDef prodDef)
        {

            var quotes = WebCache.GetInstance(true).Quotes.Where(o => o.Id == prodDef.Id).ToList();
            //var prodDefs = redisProdDefClient.GetByIds(ids);
            var quote = quotes.FirstOrDefault(o => o.Id == prodDef.Id);
            if (quote != null)
            {
                return Quotes.GetLastPrice(quote);
            }

            return 0;
        }
    }
}