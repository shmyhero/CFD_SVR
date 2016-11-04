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
using CFD_COMMON.Utils;
using CFD_COMMON.Utils.Extensions;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Entities;
using CFD_JOBS_WinApp.Interface;
using System.Text;

namespace CFD_JOBS_WinApp.Jobs
{
    public class AyondoTradeHistoryLiveJob : BaseCFDJob, ICFDJob
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
        private static DateTime? _lastEndTime = null;

        private static StringBuilder sbInfo = new StringBuilder();

        private AyondoTradeHistoryLiveJob() { }

        public AyondoTradeHistoryLiveJob(string name)
        {
            base.JobName = name;
            CreatedAt = DateTime.UtcNow;
        }

        public void Run()
        {
            IsRunning = true;
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    if(!IsRunning)
                    {
                        break;
                    }

                    try
                    {
                        string log = string.Format("在{0}时运行", DateTime.UtcNow);
                        if (logInfoItems.Count > 100)//最多只显示100条
                        {
                            logInfoItems.RemoveAt(logInfoItems.Count - 1);
                        }
                        logInfoItems.Insert(0, log);
                        LogInfo = string.Join(System.Environment.NewLine, logInfoItems);
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogException(e);
                    }

                    Thread.Sleep(Interval);
                }
            });
        }

        public void Stop()
        {
            IsRunning = false;
        }

        /// <summary>
        /// push auto-close notification
        /// </summary>
        /// <param name="systemCloseHistory"></param>
        private static void Push(List<CFD_COMMON.Models.Entities.AyondoTradeHistory> systemCloseHistorys)
        {
            if (systemCloseHistorys == null || systemCloseHistorys.Count == 0)
                return;

            string msgTemplate = "{{\"id\":{3}, \"type\":\"1\", \"title\":\"盈交易\", \"StockID\":{1}, \"CName\":\"{2}\", \"message\":\"{0}\"}}";

            //me
            string msgContentTemplate = "{0}于{1}平仓，价格为{2}，{3}美元";

            List<KeyValuePair<string, string>> getuiPushList = new List<KeyValuePair<string, string>>();
            List<long> ayondoAccountIds = systemCloseHistorys.Where(o => o.AccountId.HasValue).Select(o => o.AccountId.Value).Distinct().ToList();
            using (var db = CFDEntities.Create())
            {
                //原先是只获取开启了自动提醒的用户。现在为了消息中心，改为获取全部用户，在后面的循环里面再判断是否要发推送（消息中心一定要保存的）。
                var query = from u in db.Users
                            join d in db.Devices on u.Id equals d.userId
                            into x
                            from y in x.DefaultIfEmpty()
                            where ayondoAccountIds.Contains(u.AyondoAccountId.Value) //&& u.AutoCloseAlert.HasValue && u.AutoCloseAlert.Value
                            select new { y.deviceToken, u.Id, u.AyondoAccountId, u.AutoCloseAlert };

                var result = query.ToList();
                //因为一个用户可能有多台设备，所以要在循环的时候判断一下，是否一条Position的平仓消息已经被记录过
                //Key - Position Id
                //Value - 生成的Message Id
                Dictionary<long, int> messageSaved = new Dictionary<long, int>();
                foreach (var h in systemCloseHistorys)
                {
                    if (!h.PositionId.HasValue)
                        continue;

                    foreach (var item in result)
                    {
                        if (item.AyondoAccountId == h.AccountId)
                        {
                            #region save Message
                            //针对每一个position id，只保存一次message
                            if (!messageSaved.ContainsKey(h.PositionId.Value))
                            {
                                Message msg = new Message();
                                msg.UserId = item.Id;
                                //如果PL大于零，则为止盈消息（因为系统自动平仓没有止盈，只有止损）
                                if (h.PL > 0)
                                {
                                    string msgFormat = "{0}已达到您设置的止盈价格:{1},盈利+{2}";
                                    msg.Title = "止盈消息";
                                    string pl = Math.Abs(Math.Round(h.PL.Value, 2)).ToString();
                                    msg.Body = string.Format(msgFormat, Translator.GetCName(h.SecurityName), Math.Round(h.TradePrice.Value, 2), pl + "美元");
                                    msg.CreatedAt = DateTime.UtcNow;
                                    msg.IsReaded = false;
                                }
                                else if (!isAutoClose(h, db))//如果是设置的止损
                                {
                                    string msgFormat = "{0}已达到您设置的止损价格:{1},亏损-{2}";
                                    msg.Title = "止损消息";
                                    string pl = Math.Abs(Math.Round(h.PL.Value, 2)).ToString();
                                    msg.Body = string.Format(msgFormat, Translator.GetCName(h.SecurityName), Math.Round(h.TradePrice.Value, 2), pl + "美元");
                                    msg.CreatedAt = DateTime.UtcNow;
                                    msg.IsReaded = false;
                                }
                                else//系统自动平仓
                                {
                                    string msgFormat = "{0}已经被系统自动平仓，平仓价格:{1},{2}";
                                    msg.Title = "平仓消息";
                                    string pl = h.PL.Value < 0 ? "亏损-" + Math.Abs(Math.Round(h.PL.Value, 2)).ToString() : "盈利+" + Math.Abs(Math.Round(h.PL.Value, 2)).ToString();
                                    msg.Body = string.Format(msgFormat, Translator.GetCName(h.SecurityName), Math.Round(h.TradePrice.Value, 2), pl + "美元");
                                    msg.CreatedAt = DateTime.UtcNow;
                                    msg.IsReaded = false;
                                }

                                db.Messages.Add(msg);
                                db.SaveChanges();
                                messageSaved.Add(h.PositionId.Value, msg.Id);
                            }
                            #endregion

                            #region Push notification
                            if (item.AutoCloseAlert.HasValue && item.AutoCloseAlert.Value && !string.IsNullOrEmpty(item.deviceToken))
                            {
                                string msgPart4 = string.Empty;
                                if (h.PL.HasValue)
                                {
                                    if (h.PL.Value < 0)
                                    {
                                        msgPart4 = "亏损" + Math.Abs(Math.Round(h.PL.Value)).ToString();
                                    }
                                    else
                                    {
                                        msgPart4 = "盈利" + Math.Abs(Math.Round(h.PL.Value)).ToString();
                                    }
                                }

                                string message = string.Format(msgContentTemplate, Translator.GetCName(h.SecurityName), DateTimes.UtcToChinaTime(h.TradeTime.Value).ToString(CFDGlobal.DATETIME_MASK_SECOND), Math.Round(h.TradePrice.Value, 2), msgPart4);
                                getuiPushList.Add(new KeyValuePair<string, string>(item.deviceToken, string.Format(msgTemplate, message, h.SecurityId, Translator.GetCName(h.SecurityName), messageSaved[h.PositionId.Value])));
                            }
                            #endregion

                        }
                    }
                }

            }

            var splitedPushList = getuiPushList.SplitInChunks(1000);
            var push = new GeTui();
            foreach (var pushList in splitedPushList)
            {
                var response = push.PushBatch(pushList);
                CFDGlobal.LogLine("Auto close notification push response:" + response);
            }
        }

        /// <summary>
        /// 判断某条平仓记录是系统自动平仓，还是设置止损后的平仓
        /// </summary>
        /// <param name="closedHistory"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static bool isAutoClose(CFD_COMMON.Models.Entities.AyondoTradeHistory closedHistory, CFDEntities db)
        {
            //用平仓记录的PositionID去找到开仓记录
            var openHistory = db.AyondoTradeHistories.FirstOrDefault(o => o.PositionId == closedHistory.PositionId && o.UpdateType == "CREATE");
            if (openHistory == null) //如果没有找到开仓记录（原则上不会发生），就认为是系统自动平仓
            {
                return true;
            }

            if (!openHistory.Quantity.HasValue || !openHistory.TradePrice.HasValue)//如果开仓记录没有价格或数量，就认为是系统自动平仓
            {
                return true;
            }

            decimal investment = openHistory.Quantity.Value * openHistory.TradePrice.Value;
            //如果亏损/投资比，大于0.9就认为是系统自动平仓
            if (0.9M < Math.Abs((closedHistory.PL / investment).Value))
            {
                return true;
            }

            return false;
        }
    }
}
