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

namespace CFD_JOBS.Ayondo
{
    public class AyondoTradeHistory
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private static DateTime? _lastEndTime = null;

        public static void Run()
        {
            while (true)
            {
                try
                {
                    DateTime dtStart;
                    DateTime dtEnd = DateTime.UtcNow;

                    if (_lastEndTime == null)
                    {
                        dtStart = dtEnd - Interval; //fetch interval length of period
                    }
                    else
                    {
                        dtStart = _lastEndTime.Value; //fetch data since last fetch
                    }

                    var tsStart = dtStart.ToUnixTimeMs();
                    var tsEnd = dtEnd.ToUnixTimeMs();

                    var webClient = new WebClient();

                    CFDGlobal.LogLine("Fetching data " + dtStart + " ~ " + dtEnd);

                    var dtDownloadStart = DateTime.UtcNow;
                    var downloadString = webClient.DownloadString(
                        "http://thvm-prod4.cloudapp.net:14535/demo/reports/tradehero/cn/tradehistory?start="
                        + tsStart + "&end=" + tsEnd);

                    CFDGlobal.LogLine("Done. " + (DateTime.UtcNow - dtDownloadStart).TotalSeconds + "s");

                    var lines = downloadString.Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);

                    var lineArrays = lines.Skip(1) //skip headers
                        .Select(o => o.Split(','))
                        //.Where(o => o.Last() == "NA") //DeviceType == NA
                        .ToList();

                    if (lineArrays.Count == 0)
                    {
                        CFDGlobal.LogLine("no data received");
                    }
                    else
                    {
                        var entities = new List<CFD_COMMON.Models.Entities.AyondoTradeHistory>();
                        using (var db = CFDEntities.Create())
                        {
                            var dbMaxCreateTime = db.AyondoTradeHistories.Max(o => o.CreateTime);
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
                                var qty = Convert.ToDecimal(arr[9]);
                                var price = Convert.ToDecimal(arr[10]);
                                var pl = Convert.ToDecimal(arr[11]);
                                var guid = arr[12];
                                decimal? stopLoss = arr[13] == ""
                                    ? (decimal?) null
                                    : decimal.Parse(arr[13], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint);
                                //1.0E-6
                                decimal? takeProfit = arr[14] == "" ? (decimal?) null : Convert.ToDecimal(arr[14]);
                                var createTime = DateTime.ParseExact(arr[15], CFDGlobal.AYONDO_DATETIME_MASK,
                                    CultureInfo.CurrentCulture,
                                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                                var updateType = arr[16];
                                var deviceType = arr[17];

                                var tradeHistory = new CFD_COMMON.Models.Entities.AyondoTradeHistory()
                                {
                                    PositionId = posId,
                                    TradeId = tradeId,
                                    AccountId = accountId,
                                    FirstName = firstName,
                                    LastName = lastName,
                                    TradeTime = time,//data time
                                    SecurityId = secIdD,
                                    SecurityName = secName,
                                    Direction = direction,
                                    Quantity = qty,
                                    TradePrice = price,
                                    PL = pl,
                                    GUID = guid,
                                    StopLoss = stopLoss,
                                    TakeProfit = takeProfit,
                                    CreateTime = createTime,//position created time
                                    UpdateType = updateType,
                                    DeviceType = deviceType,
                                };

                                if (tradeHistory.TradeTime <= dbMaxCreateTime)
                                    continue; //skip old data

                                entities.Add(tradeHistory);
                            }

                            CFDGlobal.LogLine("maxCreateTime: " + dbMaxCreateTime + " data:" + lineArrays.Count +
                                              " newData:" + entities.Count);

                            if (entities.Count > 0)
                            {
                                CFDGlobal.LogLine("saving to db...");
                                db.AyondoTradeHistories.AddRange(entities);
                                db.SaveChanges();
                            }
                        }

                        Task.Factory.StartNew(() => {
                            List<CFD_COMMON.Models.Entities.AyondoTradeHistory> systemClosedPositions = entities.Where(x => x.UpdateType == "DELETE" && x.DeviceType == "NA").ToList();
                            Push(systemClosedPositions);
                        });
                    }

                    CFDGlobal.LogLine("");
                    _lastEndTime = dtEnd;
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(Interval);
            }
        }

        /// <summary>
        /// push auto-close notification
        /// </summary>
        /// <param name="systemCloseHistory"></param>
        private static void Push(List<CFD_COMMON.Models.Entities.AyondoTradeHistory> systemCloseHistorys)
        {
            if (systemCloseHistorys == null || systemCloseHistorys.Count == 0)
                return;

            string msgTemplate = "{{\"type\":\"1\", \"title\":\"盈交易\", \"message\":{0}}}";

            //me
            string msgContentTemplate = "{0}于{1}平仓，价格为{2}美元,已{3}美元";

            List<KeyValuePair<string, string>> getuiPushList = new List<KeyValuePair<string, string>>();
            List<long> ayondoAccountIds = systemCloseHistorys.Where(o => o.AccountId.HasValue).Select(o => o.AccountId.Value).Distinct().ToList();
            using (var db = CFDEntities.Create())
            {
                var query = from d in db.Devices
                               join u in db.Users on d.userId equals u.Id
                               where ayondoAccountIds.Contains(u.AyondoAccountId.Value) && u.AutoCloseAlert.HasValue && u.AutoCloseAlert.Value
                               select new {d.deviceToken, d.userId, u.AyondoAccountId   };

                var result = query.ToList();

                foreach(var h in systemCloseHistorys)
                {
                    foreach (var item in result)
                    {
                        if(item.AyondoAccountId == h.AccountId)
                        {
                            string msgPart4 = string.Empty;
                            if(h.PL.HasValue)
                            {
                                if(h.PL.Value < 0)
                                {
                                    msgPart4 = "亏损" + Math.Abs(Math.Round(h.PL.Value)).ToString();
                                }
                                else
                                {
                                    msgPart4 = "盈利" + Math.Abs(Math.Round(h.PL.Value)).ToString();
                                }
                            }
                            string message = string.Format(msgContentTemplate, Translator.GetCName(h.SecurityName), h.TradeTime, Math.Round(h.TradePrice.Value,2), msgPart4);
                            getuiPushList.Add(new KeyValuePair<string, string>(item.deviceToken, string.Format(msgTemplate, message)));
                        }
                    }
                }
            }

            var splitedPushList = getuiPushList.SplitInChunks(1000);
            var push = new GeTui();
            foreach(var pushList in splitedPushList)
            {
                var response = push.PushBatch(pushList);
            }
        }
    }
}