using AutoMapper;
using CefSharp.Wpf;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChromeNet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ChromiumWebBrowser webView = null;

        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan HistoryInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan HistoryIdentifier = TimeSpan.FromHours(2);
        private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(60 * 24);
        private static DateTime? _lastEndTime = null;
        private static IMapper Mapper = null;
        private static AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        public int Timeout { get; set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var setting = new CefSharp.CefSettings();
            CefSharp.Cef.Initialize(setting);

            webView = new CefSharp.Wpf.ChromiumWebBrowser();
            borderChrome.Child = webView;
            webView.FrameLoadEnd += WebView_FrameLoadEnd;

            Timeout = 10 * 60 * 1000;
            Mapper = new MapperConfiguration(cfg => {
                cfg.CreateMap<AyondoTransferHistoryBase, AyondoTransferHistory>();
                cfg.CreateMap<AyondoTransferHistoryBase, AyondoTransferHistory_Live>();
                cfg.CreateMap<MessageBase, Message_Live>();
            }).CreateMapper();
            //webView.Address = "https://www.tradehub.net/live/reports/tradehero/cn/transferhistory?start=1491984000001&end=1492070400001";


            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                GetTransferHistory();
            }));

        }

        private void WebView_FrameLoadEnd(object sender, CefSharp.FrameLoadEndEventArgs e)
        {
            if (e.Frame.IsMain)
            {
                e.Frame.GetTextAsync().ContinueWith(task => {
                    string downloadString = task.Result;
                    Dispatcher.BeginInvoke(new Action(() => {
                        txtLog.Text += DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "收到返回";
                        txtLog.Text += System.Environment.NewLine;
                        txtLog.Text += downloadString;
                        txtLog.Text += System.Environment.NewLine;
                    }));

                    if (downloadString == "error") throw new Exception("API returned \"error\"");

                    try
                    {
                        //var lines = downloadString.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                        //var lineArrays = lines.Skip(1) //skip headers
                        //    .Select(o => o.Split(','))
                        //    //.Where(o => o.Last() == "NA") //DeviceType == NA
                        //    .ToList();

                        #region Chomre里面拿到的返回没有换行符，所以才有下面这堆代码
                        var allItems = downloadString.Substring(downloadString.IndexOf("TransferId ") + 11).Split(',');

                        List<string[]> lineArrays = new List<string[]>();
                        string[] line = new string[26];
                        for (int x = 0; x < allItems.Length; x++)
                        {
                            if (x == 0)
                            {
                                lineArrays.Add(line);
                                line[0] = allItems[x];
                                continue;
                            }

                            if (x == allItems.Length - 1)
                            {
                                line[25] = allItems[x];
                                break;
                            }

                            if (x % 25 == 0) //一行有26个元素，新的一行开始
                            {
                                if (allItems[x].IndexOf(' ') > 0) //不是最后一个
                                {
                                    string transferID = allItems[x].Substring(0, allItems[x].IndexOf(' ')); //属于上一行
                                    string transferType = allItems[x].Substring(allItems[x].IndexOf(' ') + 1); //属于下一行
                                    line[25] = transferID;

                                    line = new string[26];
                                    line[0] = transferType;
                                    lineArrays.Add(line);
                                }
                            }
                            else
                            {
                                line[x % 25] = allItems[x];
                            }
                        }
                        #endregion

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
                                        ? (decimal?)null
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
                                    db.AyondoTransferHistory_Live.AddRange(newTransferHistories.Select(o => Mapper.Map<AyondoTransferHistory_Live>(o)));

                                    CFDGlobal.LogLine("saving transfer histories...");
                                    //db.SaveChanges();
                                }

                                var messages = new List<MessageBase>();
                                var referRewards = new List<ReferReward>();
                                var push = new GeTui();
                                //string pushTemplate = "{{\"type\":\"3\",\"title\":\"盈交易\",\"message\": \"{0}\",\"deepLink\":\"cfd://page/me\"}}";

                                #region 入金的短信、被推荐人首次入金送推荐人30元
                                //foreach (var transfer in newTransferHistories)
                                //{
                                //    //入金的短信
                                //    if (transfer.TransferType.ToLower() == "WeCollect - CUP".ToLower())
                                //    {
                                //        try
                                //        {
                                //            var query = from u in db.Users
                                //                        join d in db.Devices on u.Id equals d.userId
                                //                        into x
                                //                        from y in x.DefaultIfEmpty()
                                //                        where u.AyLiveAccountId == transfer.TradingAccountId
                                //                        select new { y.deviceToken, UserId = u.Id, u.Phone, u.AyondoAccountId, u.AyLiveAccountId, u.AutoCloseAlert, u.AutoCloseAlert_Live, u.IsOnLive, y.UpdateTime };
                                //            var user = query.FirstOrDefault();
                                //            if (user != null && !string.IsNullOrEmpty(user.deviceToken) && !string.IsNullOrEmpty(user.Phone))
                                //            {
                                //                //短信
                                //                YunPianMessenger.SendSms(string.Format("【盈交易】您入金的{0}美元已到账", transfer.Amount), user.Phone);

                                //                ////推送
                                //                //List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
                                //                //list.Add(new KeyValuePair<string, string>(user.deviceToken, string.Format(pushTemplate,string.Format("【盈交易】您入金的{0}元已到账", amount))));
                                //                //push.PushBatch(list);

                                //                //入金信息放到消息中心
                                //                MessageBase msg = new MessageBase();
                                //                msg.UserId = user.UserId;
                                //                msg.Title = "入金消息";
                                //                msg.Body = string.Format("您入金的{0}元已到账", transfer.Amount);
                                //                msg.CreatedAt = DateTime.UtcNow;
                                //                msg.IsReaded = false;
                                //                messages.Add(msg);


                                //                var referer = db.Users.FirstOrDefault(u => u.AyLiveAccountId == transfer.TradingAccountId);
                                //                if (referer != null && !string.IsNullOrEmpty(referer.Phone))
                                //                {
                                //                    var referHistory = db.ReferHistorys.FirstOrDefault(r => r.ApplicantNumber == referer.Phone);
                                //                    if (referHistory != null && referHistory.IsRewarded != true)
                                //                    {
                                //                        referHistory.IsRewarded = true;
                                //                        referHistory.RewardedAt = DateTime.Now;
                                //                        referRewards.Add(new ReferReward() { Amount = 30, UserID = referHistory.RefereeID, CreatedAt = DateTime.Now });
                                //                        //db.ReferRewards.Add(new ReferReward() { Amount = 30, UserID = referHistory.RefereeID, CreatedAt = DateTime.Now });
                                //                    }
                                //                }
                                //            }
                                //        }
                                //        catch (Exception ex)
                                //        {
                                //            CFDGlobal.LogLine("Sending SMS failed for user:" + transfer.TradingAccountId);
                                //        }


                                //    }
                                //}
                                #endregion

                                if (messages.Count > 0 || referRewards.Count > 0)
                                {
                                    if (messages.Count > 0)
                                    {
                                        db.Message_Live.AddRange(messages.Select(m => Mapper.Map<Message_Live>(m)));
                                    }
                                    if (referRewards.Count > 0)
                                    {
                                        db.ReferRewards.AddRange(referRewards);
                                    }
                                    CFDGlobal.LogLine(string.Format("Saving message: {0} & refer reward: {1}", messages.Count, referRewards.Count));
                                    //db.SaveChanges();
                                }
                            }
                        }

                        if (_lastEndTime != null && DateTime.UtcNow - _lastEndTime < HistoryIdentifier) //normal import
                            Thread.Sleep(Interval);
                        else //history import
                            Thread.Sleep(HistoryInterval);

                        this.Dispatcher.BeginInvoke(new Action(() => {
                            GetTransferHistory();
                        }));
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.BeginInvoke(new Action(() => {
                            txtLog.Text += DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ": " + ex.Message;
                            txtLog.Text += System.Environment.NewLine;
                        }));
                    }
                });
                
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            webView.Address = txtUrl.Text;
        }

        private void GetTransferHistory()
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
                        lastDbRecord = (AyondoTransferHistoryBase)db.AyondoTransferHistory_Live.OrderByDescending(o => o.Timestamp).FirstOrDefault();
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

                var url = CFDGlobal.GetConfigurationSetting("ayondoTradeHistoryHost_Live")
                    + "live" + "/reports/tradehero/cn/transferhistory?start="
                    + tsStart + "&end=" + tsEnd;

                //url = "https://www.tradehub.net/live/reports/tradehero/cn/transferhistory?start=1491984000001&end=1492070400001";
                CFDGlobal.LogLine("url: " + url);

                var dtDownloadStart = DateTime.UtcNow;
                //using (var webClient = new WebClient())
                //{
                //    webClient.Encoding = Encoding.GetEncoding("iso-8859-1");
                //    downloadString = webClient.DownloadString(url);
                //    //downloadString = System.IO.File.ReadAllText("Transfer.txt");
                //}
                webView.Address = url;
                txtLog.Text += DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "发出请求 - " + url;
                txtLog.Text += System.Environment.NewLine;

                txtUrl.Text = url;

                CFDGlobal.LogLine("Done. " + (DateTime.UtcNow - dtDownloadStart).TotalSeconds + "s");

                //mark success time
                _lastEndTime = dtEnd;
            }
            catch (Exception e)
            {
                CFDGlobal.LogException(e);
            }
        }
    }
}
