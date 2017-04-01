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
using System.Text;

namespace CFD_JOBS.Ayondo
{
    public class TimedWebClient : WebClient
    {
        public int Timeout { get; set; }

        public TimedWebClient()
        {
            this.Timeout = 10*60*1000;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var objWebRequest = base.GetWebRequest(address);
            objWebRequest.Timeout = this.Timeout;
            return objWebRequest;
            //return base.GetWebRequest(address);
        }
    }
    public class AyondoTransferHistoryImport
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan HistoryInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan HistoryIdentifier = TimeSpan.FromHours(2);
        private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(60*24);
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
                        AyondoTransferHistoryBase lastDbRecord;
                        using (var db = CFDEntities.Create())//find last record in db
                        {
                            lastDbRecord = isLive
                                ? (AyondoTransferHistoryBase)db.AyondoTransferHistory_Live.OrderByDescending(o => o.Id).FirstOrDefault()
                                : (AyondoTransferHistoryBase)db.AyondoTransferHistories.OrderByDescending(o => o.Id).FirstOrDefault();
                        }

                        if (lastDbRecord == null || lastDbRecord.Timestamp == null) //db is empty
                        {
                            dtStart = dtNow.AddDays(-70);
                            dtEnd = dtStart + MaxDuration;
                        }
                        else //last record in db is found
                        {
                            var dtLastDbRecord = DateTime.SpecifyKind(lastDbRecord.Timestamp.Value, DateTimeKind.Utc);

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

                    var tsStart = dtStart.ToUnixTimeMs();
                    var tsEnd = dtEnd.ToUnixTimeMs();

                    CFDGlobal.LogLine("Fetching data " + dtStart + " ~ " + dtEnd);

                    var url = CFDGlobal.GetConfigurationSetting("ayondoTradeHistoryHost" + (isLive ? "_Live" : ""))
                        + (isLive ? "live" : "demo") + "/reports/tradehero/cn/transferhistory?start="
                        + tsStart + "&end=" + tsEnd;
                    CFDGlobal.LogLine("url: " + url);

                    var dtDownloadStart = DateTime.UtcNow;
                    string downloadString;
                    using (var webClient = new TimedWebClient())
                    {
                        webClient.Encoding=Encoding.GetEncoding("iso-8859-1");
                        downloadString = webClient.DownloadString(url);
                    }

                    CFDGlobal.LogLine("Done. " + (DateTime.UtcNow - dtDownloadStart).TotalSeconds + "s");

                    if (downloadString == "error") throw new Exception("API returned \"error\"");

                    var lines = downloadString.Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);

                    var lineArrays = lines.Skip(1) //skip headers
                        .Select(o => o.Split(','))
                        //.Where(o => o.Last() == "NA") //DeviceType == NA
                        .ToList();

                    var newTransferHistories = new List<AyondoTransferHistoryBase>();

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
                                var transferType = arr[0];
                                var accountId = Convert.ToInt64(arr[1]);
                                var firstName = arr[2];
                                var lastName = arr[3];
                                var amount = decimal.Parse(arr[4], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign);
                                var currency = arr[5];
                                var timestamp = DateTime.ParseExact(arr[6], CFDGlobal.AYONDO_DATETIME_MASK,
                                    CultureInfo.CurrentCulture,
                                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                                var approvalTime = DateTime.ParseExact(arr[7], CFDGlobal.AYONDO_DATETIME_MASK,
                                    CultureInfo.CurrentCulture,
                                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                                var whiteLabel = arr[8];
                                var productName = arr[9] == "" ? null : arr[9];
                                var baseCurrency = arr[10] == "" ? null : arr[10];
                                var quoteCurrency = arr[11] == "" ? null : arr[11];
                                var units = arr[12] == ""
                                    ? (decimal?) null
                                    : decimal.Parse(arr[12], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign);
                                var instrumentType = arr[13] == "" ? null : arr[13];
                                var isAyondo = bool.Parse(arr[14]);
                                var clientClassification = arr[15];
                                var username = arr[16];
                                var financingRate = arr[17] == ""
                                    ? (decimal?)null
                                    : decimal.Parse(arr[17], NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign);
                                var transactionId = Convert.ToInt64(arr[18]);
                                var tradingAccountId = Convert.ToInt64(arr[19]);
                                var assetClass = arr[20] == "n/a" ? null : arr[20];
                                var posId = arr[21] == "n/a" ? (long?)null : Convert.ToInt64(arr[21]);
                                var transferId = arr[25];

                                var tradeHistory = new AyondoTransferHistoryBase()
                                {
                                    TransferType = transferType,
                                    AccountId = accountId,
                                    FirstName = firstName,
                                    LastName = lastName,
                                    Amount = amount,
                                    Ccy = currency,
                                    Timestamp = timestamp.AddHours(-8),//api returning UTC+8
                                    ApprovalTime = approvalTime.AddHours(-8),//api returning UTC+8
                                    WhiteLabel = whiteLabel,
                                    ProductName = productName,
                                    BaseCcy = baseCurrency,
                                    QuoteCcy = quoteCurrency,
                                    Quantity = units,
                                    InstrumentType = instrumentType,
                                    IsAyondo = isAyondo,
                                    ClientClassification = clientClassification,
                                    Username = username,
                                    FinancingRate = financingRate,
                                    TransactionId = transactionId,
                                    TradingAccountId = tradingAccountId,
                                    AssetClass = assetClass,
                                    PositionId = posId,
                                    TransferId = transferId,
                                };

                                //if (tradeHistory.TradeTime <= dbMaxCreateTime)
                                //    continue; //skip old data

                                newTransferHistories.Add(tradeHistory);
                            }

                            //update history table
                            if (newTransferHistories.Count > 0)
                            {
                                if(isLive)
                                    db.AyondoTransferHistory_Live.AddRange(newTransferHistories.Select(o => Mapper.Map<AyondoTransferHistory_Live>(o)));
                                else
                                    db.AyondoTransferHistories.AddRange(newTransferHistories.Select(o => Mapper.Map<AyondoTransferHistory>(o)));

                                CFDGlobal.LogLine("saving transfer histories...");
                                db.SaveChanges();
                            }
                        }
                    }

                    //mark success time
                    _lastEndTime = dtEnd;
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                CFDGlobal.LogLine("");

                if (_lastEndTime != null && DateTime.UtcNow - _lastEndTime < HistoryIdentifier) //normal import
                    Thread.Sleep(Interval);
                else //history import
                    Thread.Sleep(HistoryInterval);
            }
        }
    }
}