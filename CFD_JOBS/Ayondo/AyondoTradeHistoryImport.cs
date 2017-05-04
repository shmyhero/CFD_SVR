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
                    var dtNow = DateTime.UtcNow;

                    //bool needSave = false;

                    if (_lastEndTime == null) //first time started
                    {
                        AyondoTradeHistoryBase lastDbRecord;
                        using (var db = CFDEntities.Create())//find last record in db
                        {
                            lastDbRecord = isLive
                                ? (AyondoTradeHistoryBase) db.AyondoTradeHistory_Live.OrderByDescending(o => o.Id).FirstOrDefault()
                                : db.AyondoTradeHistories.OrderByDescending(o => o.Id).FirstOrDefault();
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

                    //using (var db = CFDEntities.Create())
                    //{
                    //    var lastTradeHistory = isLive
                    //        ? db.AyondoTradeHistory_Live.OrderByDescending(o => o.Id).FirstOrDefault()
                    //        : db.AyondoTradeHistories.OrderByDescending(o => o.Id).FirstOrDefault();

                    //    //如果上次同步时间超过24小时，则每次最多只取24小时
                    //    if ((DateTime.UtcNow - lastTradeHistory.TradeTime).Value.Hours > 24)
                    //    {
                    //        dtEnd = lastTradeHistory.TradeTime.Value.AddHours(24);
                    //    }

                    //    //最后一次结束时间为空，意味着服务重新开启，此时需要获取数据库中最后一条TradeHistory作为开始时间
                    //    if (_lastEndTime == null)
                    //    {
                    //        if(lastTradeHistory != null)
                    //        {
                    //            dtStart = lastTradeHistory.TradeTime.Value.AddMilliseconds(1);
                    //        }
                    //        else
                    //        {
                    //            dtStart = dtEnd - Interval; //fetch interval length of period
                    //        }
                    //    }
                    //    else
                    //    {
                    //        dtStart = _lastEndTime.Value.AddMilliseconds(1); //fetch data since last fetch
                    //    }
                    //}
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
                                var firstName = arr[3];
                                var lastName = arr[4];
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

                            //if(tradeHistory.UpdateType == "DELETE")
                            //        {
                            //            var newPositionHistory = isLive
                            //                ? db.NewPositionHistory_live.FirstOrDefault(h => h.Id == tradeHistory.PositionId)
                            //                : db.NewPositionHistories.FirstOrDefault(h => h.Id == tradeHistory.PositionId);

                            //            if(newPositionHistory != null)
                            //            {
                            //                newPositionHistory.ClosedPrice = tradeHistory.TradePrice;
                            //                newPositionHistory.ClosedAt = tradeHistory.TradeTime;
                            //                newPositionHistory.PL = tradeHistory.PL;
                            //                needSave = true;
                            //            }
                            //        }
                            //}

                            //CFDGlobal.LogLine("maxCreateTime: " + dbMaxCreateTime + " data:" + lineArrays.Count +
                            //                  " newData:" + entities.Count);

                            var newClosedTradeHistories = newTradeHistories.Where(o => o.UpdateType == "DELETE").ToList();

                            //update position table with new closed trade histories
                            if (newClosedTradeHistories.Count > 0)
                            {
                                var newClosedPosIDs = newClosedTradeHistories.Select(o => o.PositionId).ToList();
                                
                                var positionsToClose = isLive
                                    ? db.NewPositionHistory_live.Where(o => newClosedPosIDs.Contains(o.Id)).ToList().Select(o=> o as NewPositionHistoryBase).ToList()
                                    : db.NewPositionHistories.Where(o => newClosedPosIDs.Contains(o.Id)).ToList().Select(o => o as NewPositionHistoryBase).ToList();

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

                                CFDGlobal.LogLine("saving trade histories...");
                                db.SaveChanges();
                            }
                        }
                    }

                    //mark success time
                    _lastEndTime = dtEnd;
                        
                    //auto close alert/push
                    var autoClosedHistories =newTradeHistories.Where(x => x.UpdateType == "DELETE" && x.DeviceType == "NA").ToList();

                    if (autoClosedHistories.Count > 0)
                    {
                        CFDGlobal.LogLine("auto close record count: " + autoClosedHistories.Count );
                        NotifyUser(autoClosedHistories, isLive);
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                CFDGlobal.LogLine("");
                Thread.Sleep(Interval);
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