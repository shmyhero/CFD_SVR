using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
//using System.Net;
//using System.IO;
//using System.Web.Script.Serialization;
using System.Data.SqlTypes;

namespace CFD_COMMON.Service
{
    public class CardService
    {
        //卡片服务地址(目前还没实现)
        //private const string cardServiceUrl = "http://cfd-webapi.chinacloudapp.cn/api/card";

        public CFDEntities db { get; set; }

        public CardService(CFDEntities db)
        {
            this.db = db;
        }

        public Card GetCard(decimal pl, decimal plRate, decimal openPrice)
        {
            var cards = db.Cards.Where(o => o.LowProfit <= pl && (!o.HighProfit.HasValue || o.HighProfit > pl)
            && o.LowProfitRate < plRate && (!o.HighProfitRate.HasValue || o.HighProfitRate >= plRate) && o.Expiration == SqlDateTime.MaxValue.Value).ToList();

            if (cards.Count==0)
            {
                return null;
            }

            Random ran = new Random(DateTime.UtcNow.Millisecond);
            int index = ran.Next(0, cards.Count - 1);
            return cards[index];
        }

        /// <summary>
        /// 根据收益、收益率，找到对应的卡片
        /// </summary>
        /// <param name="pl">收益</param>
        /// <param name="plRate">收益率</param>
        /// <param name="allCards">所有卡片</param>
        /// <returns></returns>
        public Card GetCard(decimal pl, decimal plRate, List<Card> allCards)
        {
            var cards = allCards.Where(o => o.LowProfit <= pl && (!o.HighProfit.HasValue || o.HighProfit > pl)
            && o.LowProfitRate < plRate && (!o.HighProfitRate.HasValue || o.HighProfitRate >= plRate)).ToList();

            if (cards.Count == 0)
            {
                return null;
            }

            Random ran = new Random(DateTime.UtcNow.Millisecond);
            int index = ran.Next(0, cards.Count - 1);
            return cards[index];
        }

        /// <summary>
        /// 根据交易历史生成卡片
        /// </summary>
        /// <param name="trade">单笔的PositionReport</param>
        /// <param name="positionHistory">包含了开仓平仓信息</param>
        /// <param name="userID">客户ID</param>
        /// <param name="allCards">所有卡片</param>
        public void DeliverCard(AyondoTradeHistoryBase trade, NewPositionHistoryBase positionHistory, int userID, List<Card> allCards)
        {
            var plRatePercent = positionHistory.LongQty.HasValue
                                                        ? (trade.TradePrice - positionHistory.SettlePrice) / positionHistory.SettlePrice * positionHistory.Leverage * 100
                                                        : (positionHistory.SettlePrice - trade.TradePrice) / positionHistory.SettlePrice * positionHistory.Leverage * 100;

            var card = GetCard(trade.PL.Value, plRatePercent.Value, allCards);
            if (card != null)
            {
                UserCard_Live uc = new UserCard_Live()
                {
                    UserId = userID,
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
                db.UserCards_Live.Add(uc);
                RewardService.AddTotalReward(userID, card.Reward.HasValue? card.Reward.Value : 0, db);
                db.SaveChanges();
            }
        }

        ///// <summary>
        ///// 异步发送交易历史列表到卡片服务 - 服务端还没实现。
        ///// </summary>
        ///// <param name="tradeHistoryList"></param>
        //public void PostAsync(List<AyondoTradeHistoryBase> tradeHistoryList)
        //{
        //    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(cardServiceUrl);
        //    request.Method = "POST";
        //    request.KeepAlive = true;
        //    request.Timeout = 3000;
        //    request.ContentType = "application/json;charset=utf-8";
        //    byte[] binaryData = Encoding.UTF8.GetBytes((new JavaScriptSerializer() { MaxJsonLength = Int32.MaxValue }).Serialize(tradeHistoryList));
        //    request.BeginGetRequestStream(new AsyncCallback(RequestStreamCallBack), new CardAsyncResult() { BinaryData = binaryData, Request = request });
        //}

        //public static void RequestStreamCallBack(IAsyncResult result)
        //{
        //    HttpWebRequest request = ((CardAsyncResult)result.AsyncState).Request;
        //    Stream reqStream = request.EndGetRequestStream(result);
        //    reqStream.Write(((CardAsyncResult)result.AsyncState).BinaryData, 0, ((CardAsyncResult)result.AsyncState).BinaryData.Length);
        //    reqStream.Close();
        //    //这里不用获取返回值，所以CallBack的方法为空
        //    request.BeginGetResponse(new AsyncCallback((obj) => { }), request);
        //}
    }

    //class CardAsyncResult
    //{
    //    public HttpWebRequest Request;
    //    public byte[] BinaryData;
    //}
}
