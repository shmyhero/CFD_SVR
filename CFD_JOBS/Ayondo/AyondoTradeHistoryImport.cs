using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using ServiceStack.Text;
using System.Threading.Tasks;
using AutoMapper;
using CFD_COMMON.Utils;
using CFD_COMMON.Utils.Extensions;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using System.Data.SqlTypes;
using System.Web;

namespace CFD_JOBS.Ayondo
{
    public class AyondoTradeHistoryImport
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan MaxDuration = TimeSpan.FromHours(4);
        private static DateTime? _lastEndTime = null;
        private static readonly IMapper Mapper = MapperConfig.GetAutoMapperConfiguration().CreateMapper();

        public static void Run(bool isLive = false)
        {
            while (true)
            {
                try
                {
                    DateTime dtStart;
                    DateTime dtEnd;

                    //1 minute buffer just in case when ayondo's data is not ready when requested
                    //fetching latest data without buffer time might causing record missing
                    var dtNow = DateTime.UtcNow.AddMinutes(-1);

                    //bool needSave = false;

                    if (_lastEndTime == null) //first time started
                    {
                        AyondoTradeHistoryBase lastDbRecord;
                        using (var db = CFDEntities.Create())//find last record in db
                        {
                            lastDbRecord = isLive
                                ? (AyondoTradeHistoryBase) db.AyondoTradeHistory_Live.OrderByDescending(o => o.TradeTime).FirstOrDefault()
                                : db.AyondoTradeHistories.OrderByDescending(o => o.TradeTime).FirstOrDefault();
                        }

                        if (lastDbRecord == null || lastDbRecord.TradeTime == null) //db is empty
                        {
                            dtStart = dtNow.AddDays(-30);
                            dtEnd = dtStart + MaxDuration;
                        }
                        else //last record in db is found
                        {
                            var dtLastDbRecord = lastDbRecord.TradeTime.Value;

                            dtStart = dtLastDbRecord.AddMilliseconds(1);

                            //最多取...
                            dtEnd = dtNow - dtLastDbRecord > MaxDuration ? dtStart + MaxDuration : dtNow;
                        }
                    }
                    else
                    {
                        dtStart = _lastEndTime.Value.AddMilliseconds(1);
                        //最多取...
                        dtEnd = dtNow - dtStart > MaxDuration ? dtStart + MaxDuration : dtNow;
                    }

                    var tsStart = dtStart.ToUnixTimeMs();//DateTime.SpecifyKind(DateTime.Parse("2017-01-18 8:07:49.767"), DateTimeKind.Utc).ToUnixTimeMs();
                    var tsEnd = dtEnd.ToUnixTimeMs();//DateTime.SpecifyKind(DateTime.Parse("2017-01-18 8:09:49.767"), DateTimeKind.Utc).ToUnixTimeMs();

                    var webClient = new WebClient();

                    CFDGlobal.LogLine("Fetching data " + dtStart + " ~ " + dtEnd);

                    var url = CFDGlobal.GetConfigurationSetting("ayondoTradeHistoryHost" + (isLive ? "_Live" : ""))
                              + (isLive ? "live" : "demo") + "/reports/tradehero/cn/tradehistory?start="
                              + tsStart + "&end=" + tsEnd;
                    CFDGlobal.LogLine("url: " + url);

                    var dtDownloadStart = DateTime.UtcNow;
                    var downloadString = webClient.DownloadString(url);

                    CFDGlobal.LogLine("Done. " + (DateTime.UtcNow - dtDownloadStart).TotalSeconds + "s");

                    if (downloadString == "error") throw new Exception("API returned \"error\"");

                    var lines = downloadString.Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);

                    var lineArrays = lines.Skip(1) //skip headers
                        .Select(o => o.Split(','))
                        //.Where(o => o.Last() == "NA") //DeviceType == NA
                        .ToList();

                    var newTradeHistories = new List<AyondoTradeHistoryBase>();

                    if (lineArrays.Count == 0)
                    {
                        CFDGlobal.LogLine("no data received");
                    }
                    else
                    {
                        CFDGlobal.LogLine("got " + lineArrays.Count + " records");

                        using (var db = CFDEntities.Create())
                        {
                            //var dbMaxCreateTime = db.AyondoTradeHistories.Max(o => o.CreateTime);

                            //PositionID,TradeID,AccountID,FirstName,LastName,
                            //TradeTime,ProductID,ProductName,Direction,Trade Size,
                            //Trade Price,Realized P&L,GUID,StopLoss,TakeProfit,
                            //CreationTime,UpdateType,DeviceType
                            foreach (var arr in lineArrays)
                            {
                                var posId = Convert.ToInt64(arr[0]);
                                var tradeId = Convert.ToInt64(arr[1]);
                                var accountId = Convert.ToInt64(arr[2]);
                                var firstName = HttpUtility.HtmlDecode(arr[3]);
                                var lastName = HttpUtility.HtmlDecode(arr[4]);
                                var time = DateTime.ParseExact(arr[5], CFDGlobal.AYONDO_DATETIME_MASK,
                                    CultureInfo.CurrentCulture,
                                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                                var secIdD = Convert.ToInt32(arr[6]);
                                var secName = arr[7];
                                var direction = arr[8];
                                var qty = decimal.Parse(arr[9], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint);
                                var price = decimal.Parse(arr[10], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint);
                                var pl = decimal.Parse(arr[11], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign);
                                var guid = arr[12];
                                decimal? stopLoss = arr[13] == "" 
                                    ? (decimal?) null 
                                    : decimal.Parse(arr[13], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint);//1.0E-6
                                decimal? takeProfit = arr[14] == "" 
                                    ? (decimal?) null 
                                    : decimal.Parse(arr[14], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign);
                                var createTime = DateTime.ParseExact(arr[15], CFDGlobal.AYONDO_DATETIME_MASK,
                                    CultureInfo.CurrentCulture,
                                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                                var updateType = arr[16];
                                var deviceType = arr[17];

                                var tradeHistory = new AyondoTradeHistoryBase()
                                {
                                    PositionId = posId,
                                    TradeId = tradeId,
                                    AccountId = accountId,
                                    FirstName = firstName,
                                    LastName = lastName,
                                    TradeTime = time, //data time
                                    SecurityId = secIdD,
                                    SecurityName = secName,
                                    Direction = direction,
                                    Quantity = qty,
                                    TradePrice = price,
                                    PL = pl,
                                    GUID = guid,
                                    StopLoss = stopLoss,
                                    TakeProfit = takeProfit,
                                    CreateTime = createTime, //position created time
                                    UpdateType = updateType,
                                    DeviceType = deviceType,
                                };

                                //if (tradeHistory.TradeTime <= dbMaxCreateTime)
                                //    continue; //skip old data

                                newTradeHistories.Add(tradeHistory);
                            }

                            //CFDGlobal.LogLine("maxCreateTime: " + dbMaxCreateTime + " data:" + lineArrays.Count +
                            //                  " newData:" + entities.Count);
                            

                            ////insert into position table if a new created position is not in db
                            //var newCreatedTradeHistories = newTradeHistories.Where(o => o.UpdateType == "CREATE").ToList();
                            //if (newCreatedTradeHistories.Count > 0)
                            //{
                            //    var newCreatedPosIDs = newCreatedTradeHistories.Select(o => o.PositionId).ToList();

                            //    var positionsInDb = isLive
                            //        ? db.NewPositionHistory_live.Where(o => newCreatedPosIDs.Contains(o.Id)).ToList().Select(o => o as NewPositionHistoryBase).ToList()
                            //        : db.NewPositionHistories.Where(o => newCreatedPosIDs.Contains(o.Id)).ToList().Select(o => o as NewPositionHistoryBase).ToList();

                            //    var positionsInDbIds = positionsInDb.Select(o => o.Id).ToList();
                            //    var positionsToInsert = newCreatedTradeHistories.Where(o => !positionsInDbIds.Contains(o.PositionId.Value)).ToList();

                            //    CFDGlobal.LogLine("got " + newCreatedTradeHistories.Count + " CREATE records, " + positionsToInsert.Count + " not in db");

                            //    if (positionsToInsert.Count > 0)
                            //    {
                            //        var accountIds = positionsToInsert.Select(o => o.AccountId.Value).ToList();
                            //        var tradeUsers = db.Users.Where(o => accountIds.Contains(o.AyondoAccountId.Value)).ToList();

                            //        foreach (var his in positionsToInsert)
                            //        {
                            //           var pos=new NewPositionHistoryBase();
                            //            pos.Id = his.PositionId.Value;

                            //            var tradeUser = tradeUsers.FirstOrDefault(o => o.AyondoAccountId == his.AccountId);
                            //            if (tradeUser != null)
                            //                pos.UserId = tradeUser.Id;

                            //            pos.SecurityId = his.SecurityId;
                            //            pos.SettlePrice = his.TradePrice;
                            //            pos.CreateTime = his.CreateTime;
                            //            if (his.Direction == "Buy")
                            //                pos.LongQty = his.Quantity;
                            //            else
                            //                pos.ShortQty = his.Quantity;
                            //            //pos.Leverage
                            //            //pos.InvestUSD

                            //            if (isLive)
                            //            {
                            //                db.AyondoTradeHistory_Live.Add(Mapper.Map<AyondoTradeHistory_Live>(pos));
                            //            }
                            //            else
                            //            {
                            //                db.AyondoTradeHistories.Add(Mapper.Map<AyondoTradeHistory>(pos));
                            //            }
                            //        }

                            //        CFDGlobal.LogLine("inserting new CREATE positions...");
                            //        db.SaveChanges();
                            //    }
                            //}


                            //update position table with new closed trade histories
                            var newClosedTradeHistories = newTradeHistories.Where(o => o.UpdateType == "DELETE").ToList();
                            if (newClosedTradeHistories.Count > 0)
                            {
                                var newClosedPosIDs = newClosedTradeHistories.Select(o => o.PositionId).ToList();
                                
                                var positionsToClose = isLive
                                    ? db.NewPositionHistory_live.Where(o => newClosedPosIDs.Contains(o.Id)).ToList().Select(o=> o as NewPositionHistoryBase).ToList()
                                    : db.NewPositionHistories.Where(o => newClosedPosIDs.Contains(o.Id)).ToList().Select(o => o as NewPositionHistoryBase).ToList();

                                CFDGlobal.LogLine("got " + newClosedTradeHistories.Count + " DELETE records, " + positionsToClose.Count + " found in db");

                                if (positionsToClose.Count > 0)
                                {
                                    foreach (var pos in positionsToClose)
                                    {
                                        var closeTrade = newClosedTradeHistories.FirstOrDefault(o => o.PositionId == pos.Id);

                                        //update db fields
                                        pos.ClosedPrice = closeTrade.TradePrice;
                                        pos.ClosedAt = closeTrade.TradeTime;
                                        pos.PL = closeTrade.PL;

                                        if (closeTrade.DeviceType == "NA")
                                            pos.IsAutoClosed = true;
                                    }

                                    CFDGlobal.LogLine("updating position close time/price/pl...");
                                    db.SaveChanges();
                                }
                            }

                            //update history table
                            if (newTradeHistories.Count > 0)
                            {
                                if (isLive)
                                    db.AyondoTradeHistory_Live.AddRange(newTradeHistories.Select(o=> Mapper.Map<AyondoTradeHistory_Live>(o)));
                                else
                                    db.AyondoTradeHistories.AddRange(newTradeHistories.Select(o => Mapper.Map<AyondoTradeHistory>(o)));

                                CFDGlobal.LogLine("saving raw trade histories...");
                                db.SaveChanges();
                            }
                        }
                    }

                    //mark success time
                    _lastEndTime = dtEnd;
                        
                    //auto close alert/push
                    var autoClosedHistories =newTradeHistories.Where(x => x.UpdateType == "DELETE" && x.DeviceType == "NA").ToList();

                    //open/close operations
                    var createDeleteHistories = newTradeHistories.Where(x => x.UpdateType == "DELETE" || x.UpdateType == "CREATE").ToList();

                    if (autoClosedHistories.Count > 0)
                    {
                        CFDGlobal.LogLine("auto close record count: " + autoClosedHistories.Count );
                        NotifyUser(autoClosedHistories, isLive);
                    }

                    if (isLive && createDeleteHistories.Count > 0)
                    {
                        CFDGlobal.LogLine("open/close records (to notify followers) count: " + createDeleteHistories.Count);
                        NotifyFollower(createDeleteHistories);
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);

                    ElmahLogForJOB.Log(e);
                }

                CFDGlobal.LogLine("");
                Thread.Sleep(Interval);
            }
        }

        private static void NotifyFollower(List<AyondoTradeHistoryBase> createDeleteHistories)
        {
            var accountIds =
                createDeleteHistories.Where(o => o.AccountId.HasValue)
                    .Select(o => o.AccountId.Value)
                    .Distinct()
                    .ToList();
            var positionIds =
                createDeleteHistories.Where(o => o.PositionId.HasValue)
                    .Select(o => o.PositionId.Value)
                    .Distinct()
                    .ToList();
            using (var db = CFDEntities.Create())
            {
                var users = db.Users.Where(o => accountIds.Contains(o.AyLiveAccountId.Value)).ToList();
                var positions = db.NewPositionHistory_live.Where(o => positionIds.Contains(o.Id)).ToList();
                var ownerIds = users.Select(o => o.Id).ToList();
                var followers = db.UserFollows.Where(o => ownerIds.Contains(o.FollowingId)).ToList();

                string msgCreateTitle = "开仓消息";
                string msgCreateBody = "您关注的“{0}”，以{1}美元{2}倍杠杆，下单了{3}。";
                string msgDeletTitle = "平仓消息";
                string msgDeleteBody = "您关注的“{0}”，以{1}美元{2}倍杠杆交易的{3}，刚刚平仓了。";

                foreach (var h in createDeleteHistories)
                {
                    var owner = users.FirstOrDefault(o => o.AyLiveAccountId == h.AccountId);
                    if (owner.ShowOpenCloseData ?? CFDUsers.DEFAULT_SHOW_DATA)
                    {
                        var position = positions.FirstOrDefault(o => o.Id == h.PositionId);
                        var hisFollowers = followers.Where(o => o.FollowingId == owner.Id);

                        var title = h.UpdateType == "CREATE" ? msgCreateTitle : msgDeletTitle;
                        var body = string.Format(h.UpdateType == "CREATE" ? msgCreateBody : msgDeleteBody,
                            owner.Nickname,
                            position.InvestUSD.Value.ToString("0"),
                            position.Leverage.Value.ToString("0"),
                            Translator.GetCName(h.SecurityName));

                        foreach (var f in hisFollowers)
                        {
                            db.Message_Live.Add(new Message_Live()
                            {
                                Body = body,
                                CreatedAt = DateTime.UtcNow,
                                IsReaded = false,
                                Title = title,
                                UserId = f.UserId,
                            });
                        }
                    }
                }

                db.SaveChanges();
            }
        }

        /// <summary>
        /// push auto-close notification
        /// </summary>
        private static void NotifyUser(List<AyondoTradeHistoryBase> autoClosedHistories, bool isLive)
        {
            if (autoClosedHistories == null || autoClosedHistories.Count == 0)
                return;

            string msgTemplate = "{{\"id\":{3}, \"type\":\"1\", \"title\":\"盈交易\", \"StockID\":{1}, \"CName\":\"{2}\", \"message\":\"{0}\"}}";

            //me
            string msgContentTemplate = "{0}于{1}平仓，价格为{2}，{3}美元";

            List<KeyValuePair<string, string>> getuiPushList = new List<KeyValuePair<string, string>>();
            List<long> ayondoAccountIds = autoClosedHistories.Where(o => o.AccountId.HasValue).Select(o => o.AccountId.Value).Distinct().ToList();
            using (var db = CFDEntities.Create())
            {
                //原先是只获取开启了自动提醒的用户。现在为了消息中心，改为获取全部用户，在后面的循环里面再判断是否要发推送（消息中心一定要保存的）。
                var query = from u in db.Users
                            join d in db.Devices on u.Id equals d.userId
                            into x from y in x.DefaultIfEmpty()
                            where ayondoAccountIds.Contains(isLive ? u.AyLiveAccountId.Value : u.AyondoAccountId.Value) //&& u.AutoCloseAlert.HasValue && u.AutoCloseAlert.Value
                               select new {y.deviceToken, UserId = u.Id, u.AyondoAccountId, u.AyLiveAccountId, u.AutoCloseAlert, u.AutoCloseAlert_Live, u.IsOnLive, y.UpdateTime   };

                var users = query.ToList();

                //因为一个用户可能有多台设备，所以要在循环的时候判断一下，是否一条Position的平仓消息已经被记录过
                //Key - Position Id
                //Value - 生成的Message Id
                var messageSaved = new Dictionary<long, int>();
                var cardService = new CardService(db);
                var allCards = db.Cards.Where(item=> item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).ToList();

                List<long> posIDList = autoClosedHistories.Select(o => o.PositionId.Value).ToList();
                //根据PositionID找到相关记录
                IQueryable<NewPositionHistoryBase> positionHistoryQuery = null;
                if(isLive)
                {
                    positionHistoryQuery = from n in db.NewPositionHistory_live
                                          where posIDList.Contains(n.Id)
                                          select new NewPositionHistoryBase() { Id = n.Id, LongQty = n.LongQty, ShortQty = n.ShortQty, Leverage = n.Leverage, SettlePrice = n.SettlePrice, ClosedPrice = n.ClosedPrice, InvestUSD= n.InvestUSD, ClosedAt = n.ClosedAt, CreateTime = n.CreateTime, PL = n.PL, SecurityId = n.SecurityId, UserId =n.UserId };
                }
                else
                {
                    positionHistoryQuery = from n in db.NewPositionHistories
                                          where posIDList.Contains(n.Id)
                                          select new NewPositionHistoryBase() { Id = n.Id, LongQty = n.LongQty, ShortQty = n.ShortQty, Leverage = n.Leverage, SettlePrice = n.SettlePrice, ClosedPrice = n.ClosedPrice, InvestUSD = n.InvestUSD, ClosedAt = n.ClosedAt, CreateTime = n.CreateTime, PL = n.PL, SecurityId = n.SecurityId, UserId = n.UserId };
                }

                List<NewPositionHistoryBase> positionHistoryList = positionHistoryQuery.ToList();

                foreach (var trade in autoClosedHistories)
                {
                    if (!trade.PositionId.HasValue)
                        continue;

                    var positionHistory = positionHistoryList.FirstOrDefault(o => o.Id == trade.PositionId);
                    if (positionHistory == null)
                        continue;

                    var user = users.OrderByDescending(o=>o.UpdateTime).FirstOrDefault(o => (isLive ? o.AyLiveAccountId : o.AyondoAccountId) == trade.AccountId);
                  
                    if(user == null) continue;

                    #region save Message
                    CFDGlobal.LogLine("Start saving message for user:" + user.UserId);
                    //针对每一个position id，只保存一次message
                    if (!messageSaved.ContainsKey(trade.PositionId.Value))
                    {
                        MessageBase msg = isLive ? (MessageBase) new Message_Live() : (MessageBase) new Message();
                        msg.UserId = user.UserId;
                        //如果有盈利，则为止盈消息（因为系统自动平仓没有止盈，只有止损）
                        if (Quotes.IsProfit(positionHistory.LongQty.HasValue, positionHistory.SettlePrice, positionHistory.ClosedPrice))
                        {
                            string msgFormat = "{0}已达到您设置的止盈价格: {1}，盈利{2}";
                            msg.Title = "止盈消息";
                            string pl = Math.Abs(Math.Round(trade.PL.Value, 2)).ToString();
                            msg.Body = string.Format(msgFormat, Translator.GetCName(trade.SecurityName), Math.Round(trade.TradePrice.Value, 2), pl + "美元");
                            msg.CreatedAt = DateTime.UtcNow;
                            msg.IsReaded = false;
                        }
                        else if(!isAutoClose(positionHistory))//如果是设置的止损 //todo:multiple db access
                        {
                            string msgFormat = "{0}已达到您设置的止损价格: {1}，亏损{2}";
                            msg.Title = "止损消息";
                            string pl = Math.Abs(Math.Round(trade.PL.Value, 2)).ToString();
                            msg.Body = string.Format(msgFormat, Translator.GetCName(trade.SecurityName), Math.Round(trade.TradePrice.Value, 2), pl + "美元");
                            msg.CreatedAt = DateTime.UtcNow;
                            msg.IsReaded = false;
                        }
                        else//系统自动平仓
                        {
                            string msgFormat = "{0}已经被系统自动平仓，平仓价格: {1}，{2}";
                            msg.Title = "平仓消息";
                            string pl = trade.PL.Value < 0 ? "亏损" + Math.Abs(Math.Round(trade.PL.Value, 2)).ToString() : "盈利" + Math.Abs(Math.Round(trade.PL.Value, 2)).ToString();
                            msg.Body = string.Format(msgFormat, Translator.GetCName(trade.SecurityName), Math.Round(trade.TradePrice.Value, 2), pl + "美元");
                            msg.CreatedAt = DateTime.UtcNow;
                            msg.IsReaded = false;
                        }

                        if (isLive)
                            db.Message_Live.Add(msg as Message_Live);
                        else
                            db.Messages.Add(msg as Message);

                        db.SaveChanges();

                        messageSaved.Add(trade.PositionId.Value, msg.Id);
                    }
                    #endregion

                    #region Push notification
                    //TODO: push to only the latest device for each user
                    CFDGlobal.LogLine("Start pushing for user:" + user.UserId);
                    CFDGlobal.LogLine(string.Format("Device Token:{0}; IsLive:{1}; AutoCloseAlert_Live:{2};AutoCloseAlert:{3};IsOnLive:{4};TradeTime:{5};",
                        user.deviceToken, isLive, user.AutoCloseAlert_Live, user.AutoCloseAlert, user.IsOnLive, trade.TradeTime.HasValue? trade.TradeTime.Value.ToString("yyyy-MM-dd hh:mm:ss"): "--"));
                    if (!string.IsNullOrEmpty(user.deviceToken)//has device token
                        && ((isLive ? user.AutoCloseAlert_Live : user.AutoCloseAlert) ?? false)//auto close alert is enabled
                        && DateTime.UtcNow - trade.TradeTime < TimeSpan.FromHours(1)//do not send push if it's already late
                        && (isLive && user.IsOnLive == true || !isLive && user.IsOnLive != true)//add to push list only when user is on the same environment as which this job is running on
                        )
                    {
                        string msgPart4 = string.Empty;
                        if (trade.PL.HasValue)
                        {
                            if (trade.PL.Value < 0)
                            {
                                msgPart4 = "亏损" + Math.Abs(Math.Round(trade.PL.Value)).ToString();
                            }
                            else
                            {
                                msgPart4 = "盈利" + Math.Abs(Math.Round(trade.PL.Value)).ToString();
                            }
                        }

                        string message = string.Format(msgContentTemplate, Translator.GetCName(trade.SecurityName),
                            DateTimes.UtcToChinaTime(trade.TradeTime.Value).ToString(CFDGlobal.DATETIME_MASK_SECOND),
                            Math.Round(trade.TradePrice.Value, 2), msgPart4);

                        getuiPushList.Add(new KeyValuePair<string, string>(user.deviceToken,
                            string.Format(msgTemplate, message, trade.SecurityId,
                                Translator.GetCName(trade.SecurityName), messageSaved[trade.PositionId.Value])));
                    }

                    #endregion

                    #region 生成卡片
                    try
                    {
                        if(isLive)
                        {
                            cardService.DeliverCard(trade, positionHistory, user.UserId, allCards);
                        }
                    }
                    catch(Exception ex)
                    {
                        CFDGlobal.LogLine("Card exception:" + ex.Message);
                    }
                    

                    #endregion
                }
            }

            var splitedPushList = getuiPushList.SplitInChunks(1000);
            var push = new GeTui();
            foreach(var pushList in splitedPushList)
            {
                var response = push.PushBatch(pushList);
                CFDGlobal.LogLine("Auto close notification push response:" + response);
            }
        }

        /// <summary>
        /// 判断某条平仓记录是系统自动平仓，还是设置止损后的平仓
        /// </summary>
        /// <param name="closedHistory"></param>
        /// <returns></returns>
        private static bool isAutoClose(NewPositionHistoryBase positionHistory)
        {
            //如果亏损率大于90，就认为是系统自动平仓
            if(90M < Math.Abs(Quotes.GetProfitRate(positionHistory.LongQty.HasValue, positionHistory.SettlePrice, positionHistory.ClosedPrice, positionHistory.Leverage).Value))
            { 
                return true;
            }

            return false;
        }
    }
}