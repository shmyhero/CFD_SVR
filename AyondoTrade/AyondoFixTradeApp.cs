using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
using QuickFix;
using QuickFix.DataDictionary;
using QuickFix.Fields;
using QuickFix.FIX44;
using ServiceStack.Text;
using Message = QuickFix.Message;

namespace AyondoTrade
{
    public class AyondoFixTradeApp : MessageCracker, IApplication
    {
        public Session Session { get; set; }

        private readonly string _username = CFDGlobal.GetConfigurationSetting("ayondoFixTradeUsername");
        private readonly string _password = CFDGlobal.GetConfigurationSetting("ayondoFixTradePassword");
        private DataDictionary _dd;
        private string _account = "138927238972";
        private string _balanceId = "138927238972";
        private readonly IDictionary<string, DateTime> _userLastLoginTime = new Dictionary<string, DateTime>();

        //custom tags
        public int TAG_MDS_SendColRep;
        public int TAG_MDS_SendNoPos;
        public int TAG_StopOID;
        public int TAG_TakeOID;
        public int TAG_TakePx;
        public int TAG_MDS_PL;
        public int TAG_Leverage;
        public int TAG_MDS_RequestID;
        public int TAG_MDS_UPL;
        public int TAG_MDS_HistoryType;
        public int TAG_MDS_StartTime;
        public int TAG_MDS_EndTime;
        public int TAG_MDS_SetSize;
        public int TAG_MDS_SetIndex;

        public ConcurrentDictionary<string, string> UsernameAccounts = new ConcurrentDictionary<string, string>();
        public IDictionary<string, string> AccountUsernames = new Dictionary<string, string>();

        /// <summary>
        /// guid as key
        /// </summary>
        public ConcurrentDictionary<string, KeyValuePair<DateTime, RequestForPositionsAck>> RequestForPositionsAcks =
            new ConcurrentDictionary<string, KeyValuePair<DateTime, RequestForPositionsAck>>();

        /// <summary>
        /// guid as key
        /// </summary>
        public ConcurrentDictionary<string, IList<KeyValuePair<DateTime, PositionReport>>> PositionReports =
            new ConcurrentDictionary<string, IList<KeyValuePair<DateTime, PositionReport>>>();

        /// <summary>
        /// guid as key
        /// </summary>
        public ConcurrentDictionary<string, IList<KeyValuePair<DateTime, PositionReport>>> OrderPositionReports =
            new ConcurrentDictionary<string, IList<KeyValuePair<DateTime, PositionReport>>>();

        /// <summary>
        /// position id as key
        /// </summary>
        public ConcurrentDictionary<string, IList<KeyValuePair<DateTime, PositionReport>>> StopTakePositionReports =
            new ConcurrentDictionary<string, IList<KeyValuePair<DateTime, PositionReport>>>();

        /// <summary>
        /// guid as key
        /// </summary>
        public ConcurrentDictionary<string, KeyValuePair<DateTime, BusinessMessageReject>> BusinessMessageRejects =
            new ConcurrentDictionary<string, KeyValuePair<DateTime, BusinessMessageReject>>();

        /// <summary>
        /// guid as key
        /// </summary>
        public ConcurrentDictionary<string, KeyValuePair<DateTime, UserResponse>> FailedUserResponses =
            new ConcurrentDictionary<string, KeyValuePair<DateTime, UserResponse>>();

        /// <summary>
        /// guid as key
        /// </summary>
        public ConcurrentDictionary<string, KeyValuePair<DateTime, ExecutionReport>> RejectedExecutionReports =
            new ConcurrentDictionary<string, KeyValuePair<DateTime, ExecutionReport>>();

        /// <summary>
        /// guid as key
        /// </summary>
        public ConcurrentDictionary<string, KeyValuePair<DateTime, decimal>> Balances = new ConcurrentDictionary<string, KeyValuePair<DateTime, decimal>>();

        /// <summary>
        /// username as key
        /// </summary>
        public ConcurrentDictionary<string, IList<KeyValuePair<DateTime, PositionReport>>> AutoClosedPositionReports =
            new ConcurrentDictionary<string, IList<KeyValuePair<DateTime, PositionReport>>>();

        //ayondodemo01 136824778776
        //ivantradehero 138673044476

        #region IApplication members

        public void ToAdmin(Message message, SessionID sessionID)
        {
            string msgType = message.Header.GetString(Tags.MsgType);

            if (msgType == MsgType.LOGON)
            {
                //CFDGlobal.LogLine(" sending username and password...");

                message.SetField(new Username(_username));
                message.SetField(new Password(_password));
            }

            //CFDGlobal.LogLine("ToAdmin: ");
            if (msgType != MsgType.HEARTBEAT)
                CFDGlobal.LogLine("ToAdmin: " + message.ToString());
        }

        public void FromAdmin(Message message, SessionID sessionID)
        {
            if (message.Header.GetString(Tags.MsgType) != MsgType.HEARTBEAT)
            {
                //CFDGlobal.LogLine("FromAdmin: ");
                CFDGlobal.LogInformation("FromAdmin: " + message.ToString());
                //CFDGlobal.LogLine(GetMessageString(message));
            }
        }

        public void ToApp(Message message, SessionID sessionId)
        {
            var msgType = message.Header.GetString(Tags.MsgType);
            if (msgType == MsgType.BUSINESS_MESSAGE_REJECT)
            {
                CFDGlobal.LogInformation("ToApp: " + message.ToString());
            }
            else if (msgType == MsgType.USERREQUEST) //35=BE login
            {
                CFDGlobal.LogLine("ToApp: " + message.ToString()); //log message

                if (message.Any(o => o.Key == Tags.Username))
                {
                    var username = message.GetString(Tags.Username);

                    //prevent multiple login of the same user at the same time
                    if (_userLastLoginTime.ContainsKey(username))
                    {
                        var dtLastLogin = _userLastLoginTime[username];

                        if (DateTime.UtcNow - dtLastLogin < TimeSpan.FromSeconds(5))
                        {
                            //don't send
                            throw new DoNotSend();

                            //message.Header.SetField(new MsgType("xx"));
                        }
                        else
                        {
                            _userLastLoginTime[username] = DateTime.UtcNow;
                        }
                    }
                    else
                        _userLastLoginTime.Add(username, DateTime.UtcNow);
                }
            }
            else
            {
                CFDGlobal.LogLine("ToApp: " + message.ToString()); //log message
            }
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            //return;

            try
            {
                //CFDGlobal.LogLine("FromApp: ");
                //CFDGlobal.LogLine(message.ToString());
                //CFDGlobal.LogLine(GetMessageString(message));

                var msgType = message.Header.GetString(Tags.MsgType);
                if (msgType == "MDS6")
                {
                    CFDGlobal.LogLine("MDS6:BalanceResponse: " + GetMessageString(message));

                    var guid = message.GetString(TAG_MDS_RequestID);
                    var quantity = message.GetDecimal(Tags.Quantity);
                    Balances.TryAdd(guid, new KeyValuePair<DateTime, decimal>(DateTime.UtcNow, quantity));

                    var account = message.GetString(Tags.Account);
                    CFDCacheManager.Instance.SetBalance(account, quantity);
                }
                else if (msgType == "MDS4")
                {
                    CFDGlobal.LogLine("MDS4:MDSTransfer: " + GetMessageString(message));
                }
                else
                    Crack(message, sessionID);
            }
            catch (Exception e)
            {
                CFDGlobal.LogInformation("FromApp: " + message.ToString());
                CFDGlobal.LogExceptionAsInfo(e);
            }
        }

        public void OnCreate(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnCreate: " + sessionID);
        }

        public void OnLogout(SessionID sessionID)
        {
            var sb = new StringBuilder();

            var st = new StackTrace();
            var stackFrames = st.GetFrames();
            if (stackFrames != null && stackFrames.Any())
            {
                foreach (var frame in stackFrames)
                {
                    var declaringType = frame.GetMethod().DeclaringType;
                    if (declaringType != null)
                        sb.AppendLine(declaringType.FullName);
                }
            }

            CFDCacheManager.Instance.ClearCache();

            CFDGlobal.LogLine("OnLogout: " + sessionID + " StackTrace: " + sb.ToString());
        }

        public void OnLogon(SessionID sessionID)
        {
            CFDGlobal.LogInformation("OnLogon: " + sessionID);

            Session = Session.LookupSession(sessionID);
            _dd = Session.ApplicationDataDictionary;

            TAG_MDS_SendColRep = _dd.FieldsByName["MDS_SendColRep"].Tag;
            TAG_MDS_SendNoPos = _dd.FieldsByName["MDS_SendNoPos"].Tag;
            TAG_StopOID = _dd.FieldsByName["StopOID"].Tag;
            TAG_TakeOID = _dd.FieldsByName["TakeOID"].Tag;
            TAG_TakePx = _dd.FieldsByName["TakePx"].Tag;
            TAG_MDS_PL = _dd.FieldsByName["MDS_PL"].Tag;
            TAG_Leverage = _dd.FieldsByName["Leverage"].Tag;
            TAG_MDS_RequestID = _dd.FieldsByName["MDS_RequestID"].Tag;
            TAG_MDS_UPL = _dd.FieldsByName["MDS_UPL"].Tag;
            TAG_MDS_HistoryType = _dd.FieldsByName["MDS_HistoryType"].Tag;
            TAG_MDS_StartTime = _dd.FieldsByName["MDS_StartTime"].Tag;
            TAG_MDS_EndTime = _dd.FieldsByName["MDS_EndTime"].Tag;
            TAG_MDS_SetSize = _dd.FieldsByName["MDS_SetSize"].Tag;
            TAG_MDS_SetIndex = _dd.FieldsByName["MDS_SetIndex"].Tag;

            ////testing
            //LogOn("thcn1", "3IcFhY");
            ////LogOn("thcn23Dbrd", "Gqb9tA");
        }

        #endregion

        #region OnMessage methods

        public void OnMessage(News news, SessionID sessionID)
        {
            CFDGlobal.LogLine("OnMessage:News: " + GetMessageString(news, true, true));

            if (news.Headline.Obj == "Fatal Error")
            {
                //var groupTags = news.GetGroupTags();
                //var tag = groupTags.FirstOrDefault(o => o == Tags.LinesOfText);
                var group = news.GetGroup(1, Tags.LinesOfText);
                var account = group.GetString(Tags.Text);

                if (AccountUsernames.ContainsKey(account))
                {
                    var username = AccountUsernames[account];

                    CFDGlobal.LogLine("deleting user " + username + " from online list...");

                    string value;
                    UsernameAccounts.TryRemove(username, out value);
                }

                CFDCacheManager.Instance.UserLogout(account);
            }
        }

        public void OnMessage(UserResponse response, SessionID sessionID)
        {
            CFDGlobal.LogLine("OnMessage:UserResponse: " + GetMessageString(response));

            if (response.Any(o => o.Key == Tags.Account)) //success
            {
                var account = response.GetString(Tags.Account);

                var username = response.Username.Obj;

                if (response.UserStatus.Obj == UserStatus.LOGGED_IN)
                {
                    //for console test
                    _account = account;

                    //add to onlinie user list
                    UsernameAccounts.AddOrUpdate(username, account, (k, v) => account);

                    if (AccountUsernames.ContainsKey(account))
                        AccountUsernames[account] = username;
                    else
                        AccountUsernames.Add(account, username);

                    CFDCacheManager.Instance.UserLogin(account);
                }
                else
                    CFDGlobal.LogInformation("UserResponse: Account:" + account + " UserStatus:" + response.UserStatus.Obj);
            }
            else
            {
                //var username = response.Username.Obj;
                //var userStatus = response.UserStatus.Obj;
                //var userStatusText = response.UserStatusText.Obj;

                var guid = response.UserRequestID.Obj;

                if (FailedUserResponses.ContainsKey(guid))
                    CFDGlobal.LogInformation("existed guid for FailedUserResponses");
                else
                    FailedUserResponses.TryAdd(guid, new KeyValuePair<DateTime, UserResponse>(DateTime.UtcNow, response));

                //CFDGlobal.LogInformation("UserResponse: Username:" + username + " UserStatus:" + userStatus + " UserStatusText:" + userStatusText);
            }
        }

        public void OnMessage(CollateralReport report, SessionID session)
        {
            //var db = CFDEntities.Create();
            //var userAyondos = db.UserAyondos.FirstOrDefault();
            //if (report.TotalNetValue.Obj != 0)
            //{
            //    userAyondos.BalanceCash = report.MarginExcess.Obj;
            //}
            //db.SaveChanges();
            var quantity = report.GetDecimal(Tags.Quantity);
            var account = report.GetString(Tags.Account);
            CFDCacheManager.Instance.SetBalance(account, quantity);

            _balanceId = report.GetString(Tags.CollRptID);

            CFDGlobal.LogLine("OnMessage:CollateralReport: " + GetMessageString(report));
        }

        public void OnMessage(ExecutionReport report, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:ExecutionReport: " + GetMessageString(report));

            if (report.OrdStatus.Obj == OrdStatus.REJECTED)
            {
                var clOrdID = report.ClOrdID.Obj;
                //var orderID = report.OrderID.Obj;

                //if (clOrdID == orderID) //when replace stop/take
                //{
                //    if (StopTakeExecutionReports.ContainsKey(clOrdID))
                //        StopTakeExecutionReports[clOrdID].Add(new KeyValuePair<DateTime, ExecutionReport>(DateTime.UtcNow, report));
                //    else
                //        StopTakeExecutionReports.TryAdd(clOrdID,
                //            new List<KeyValuePair<DateTime, ExecutionReport>> {new KeyValuePair<DateTime, ExecutionReport>(DateTime.UtcNow, report)});
                //}
                //else //when new order 
                RejectedExecutionReports.TryAdd(clOrdID, new KeyValuePair<DateTime, ExecutionReport>(DateTime.UtcNow, report));
            }
        }

        public void OnMessage(RequestForPositionsAck response, SessionID session)
        {
            //return;

            //CFDGlobal.LogLine("OnMessage:RequestForPositionsAck: " + GetMessageString(response));

            var guid = response.PosReqID.Obj;

            if (RequestForPositionsAcks.ContainsKey(guid))
            {
                throw new Exception("existed guid for RequestForPositionsAck");
            }

            RequestForPositionsAcks.TryAdd(guid, new KeyValuePair<DateTime, RequestForPositionsAck>(DateTime.UtcNow, response));
        }

        public void OnMessage(PositionReport report, SessionID session)
        {
            //return;

            //CFDGlobal.LogLine("OnMessage:PositionReport: " + GetMessageString(report, true, true));

            //var groupTags = report.GetGroupTags();
            //var noPositionsGroup = new PositionReport.NoPositionsGroup();
            //var @group2 = report.GetGroup(1, noPositionsGroup);

            //var noPositionsGroup = new PositionMaintenanceRequest.NoPositionsGroup();
            //report.GetGroup(1, noPositionsGroup);

            //var groupTags = report.GetGroupTags();
            //var indexOf = groupTags.IndexOf(Tags.NoPositions);
            //report.GetGroup(indexOf+1, noPositionsGroup);

            //throw new Exception("");

            //save result to dictionary

            var posReqId = report.PosReqID.Obj;
            if (posReqId == "Unsolicited") //not from positionreport request
            {
                if (report.Any(o => o.Key == Tags.ClOrdID)) //after order filled
                {
                    var clOrdID = report.GetString(Tags.ClOrdID);

                    if (report.Text.Obj == "Position DELETE by StopLossOrder" || report.Text.Obj == "Position DELETE by TakeProfitOrder") //auto closed by stop/take
                    {
                        //Text=Position DELETE by TakeProfitOrder
                        //Text=Position DELETE by StopLossOrder

                        var account = report.Account.Obj;
                        if (AccountUsernames.ContainsKey(account))
                        {
                            var username = AccountUsernames[account];
                            //using (var db = CFDEntities.Create())
                            //{
                            //    var user = db.Users.FirstOrDefault(o => o.AyondoUsername == username);
                            //    if (user != null && user.Phone != null)
                            //    {
                            //        var secId = Convert.ToInt32(report.SecurityID.Obj);
                            //        var sec = db.AyondoSecurities.FirstOrDefault(o => o.Id == secId);
                            //        var name = sec != null && sec.CName != null ? sec.CName : report.Symbol.Obj;
                            //        var stopTake = report.Text.Obj == "Position DELETE by StopLossOrder" ? "止损" : "止盈";
                            //        var price = report.SettlPrice;
                            //        var pl = report.GetDecimal(TAG_MDS_PL);
                            //        var sendSms = YunPianMessenger.SendSms("【MyHero运营】运营监控，您买的" + name + "已被" + stopTake + "在" + price + "，收益为" + pl.ToString("0.00")
                            //                                               + "，回T退订", user.Phone);
                            //        CFDGlobal.LogInformation(sendSms);
                            //    }
                            //}

                            CFDGlobal.LogInformation("AutoCloseMsg: username:" + username + " posId:" + report.PosMaintRptID.Obj + " secId:" + report.SecurityID.Obj + " text:" +
                                                     report.Text.Obj + " price:" + report.SettlPrice.Obj + " pl:" + report.GetDecimal(TAG_MDS_PL));

                            if (AutoClosedPositionReports.ContainsKey(username))
                                AutoClosedPositionReports[username].Add(new KeyValuePair<DateTime, PositionReport>(DateTime.UtcNow, report));
                            else
                                AutoClosedPositionReports.TryAdd(username,
                                    new List<KeyValuePair<DateTime, PositionReport>>() {new KeyValuePair<DateTime, PositionReport>(DateTime.UtcNow, report)});
                        }
                        //}
                        //catch (Exception e)
                        //{
                        //    CFDGlobal.LogException(e);
                        //}

                        CFDCacheManager.Instance.ClosePosition(account, report);

                    }
                    else //by market order
                    {
                        //Text=Position CREATE by MarketOrder

                        if (OrderPositionReports.ContainsKey(clOrdID))
                            OrderPositionReports[clOrdID].Add(new KeyValuePair<DateTime, PositionReport>(DateTime.UtcNow, report));
                        else
                            OrderPositionReports.TryAdd(clOrdID,
                                new List<KeyValuePair<DateTime, PositionReport>>() {new KeyValuePair<DateTime, PositionReport>(DateTime.UtcNow, report)});

                        if (report.Text.Obj == "Position DELETE by MarketOrder")
                        {
                            //Text=Position DELETE by MarketOrder
                            CFDCacheManager.Instance.ClosePosition(report.Account.Obj, report);
                        }
                        else
                        {
                            //Text=Position UPDATE by MarketOrder
                            //Text=Position UPDATE by TakeProfitOrder
                            //Text=Position UPDATE by StopLossOrder

                            //if position is updated by order and quantity become 0 then it will be deleted
                            if (report.Text.Obj != "Position UPDATE by MarketOrder" && report.Text.Obj != "Position UPDATE by TakeProfitOrder" && report.Text.Obj != "Position UPDATE by StopLossOrder")
                            {
                                CFDCacheManager.Instance.OpenPosition(report.Account.Obj, report);
                            }
                        }
                    }
                }
                else //after replace Stop/Take or new Stop/Take
                {
                    var posMaintRptID = report.GetString(Tags.PosMaintRptID);

                    if (StopTakePositionReports.ContainsKey(posMaintRptID))
                        StopTakePositionReports[posMaintRptID].Add(new KeyValuePair<DateTime, PositionReport>(DateTime.UtcNow, report));
                    else
                        StopTakePositionReports.TryAdd(posMaintRptID,
                            new List<KeyValuePair<DateTime, PositionReport>>() {new KeyValuePair<DateTime, PositionReport>(DateTime.UtcNow, report)});

                    CFDCacheManager.Instance.UpdatePosition(report.Account.Obj, report);
                }
            }
            else //after position report request
            {
                if (PositionReports.ContainsKey(posReqId))
                    PositionReports[posReqId].Add(new KeyValuePair<DateTime, PositionReport>(DateTime.UtcNow, report));
                else
                    PositionReports.TryAdd(posReqId, new List<KeyValuePair<DateTime, PositionReport>>() {new KeyValuePair<DateTime, PositionReport>(DateTime.UtcNow, report)});
            }
        }

        public void OnMessage(BusinessMessageReject reject, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:BusinessMessageReject: " + GetMessageString(reject, true, true));

            if (reject.Any(o => o.Key == Tags.BusinessRejectRefID))
            {
                var guid = reject.BusinessRejectRefID.Obj;

                if (guid == "Unknown")
                    CFDGlobal.LogInformation("BusinessRejectRefID is Unknown");
                else
                {
                    if (BusinessMessageRejects.ContainsKey(guid))
                    {
                        CFDGlobal.LogInformation("existed guid for BusinessMessageRejects");
                    }
                    else
                        BusinessMessageRejects.TryAdd(guid, new KeyValuePair<DateTime, BusinessMessageReject>(DateTime.UtcNow, reject));
                }
            }
            else
            {
                CFDGlobal.LogInformation("no BusinessRejectRefID");
            }
        }

        #endregion

        public string LogOn(string username, string password)
        {
            var guid = Guid.NewGuid().ToString();

            var m = new UserRequest();
            m.UserRequestID = new UserRequestID(guid);

            m.UserRequestType = new UserRequestType(UserRequestType.LOGONUSER);
            m.Username = new Username(username);
            m.Password = new Password(password);
            m.SetField(new StringField(TAG_MDS_SendColRep) {Obj = "N"});
            m.SetField(new StringField(TAG_MDS_SendNoPos) {Obj = "0"});

            SendMessage(m);

            return guid;
        }

        public string RequestForPositions(string account)
        {
            var guid = Guid.NewGuid().ToString();

            var m = new RequestForPositions();
            m.PosReqID = new PosReqID(guid);

            m.PosReqType = new PosReqType(PosReqType.POSITIONS);
            m.ClearingBusinessDate = new ClearingBusinessDate("0-0-0");
            m.TransactTime = new TransactTime(DateTime.UtcNow);
            m.Account = new Account(account);
            m.AccountType = new AccountType(AccountType.ACCOUNT_IS_CARRIED_ON_CUSTOMER_SIDE_OF_BOOKS);
            SendMessage(m);

            return guid;
        }

        public string RequestForPositionHistories(string account, DateTime startTime, DateTime endTime)
        {
            var guid = Guid.NewGuid().ToString();

            var m = new Message();
            m.Header.SetField(new MsgType("MDS7"));
            m.SetField(new StringField(TAG_MDS_RequestID) {Obj = guid});
            m.SetField(new Account(account));
            m.SetField(new IntField(TAG_MDS_HistoryType) {Obj = 1}); // 1: position history
            m.SetField(new IntField(TAG_MDS_StartTime) {Obj = (int) startTime.ToUnixTime()});
            m.SetField(new IntField(TAG_MDS_EndTime) {Obj = (int) endTime.ToUnixTime()});

            //m.SetField(new IntField(7945) {Obj = 99999});
            //m.SetField(new IntField(7946) {Obj = 0});

            SendMessage(m);

            return guid;
        }

        public string NewOrderSingle(string account, string securityId, char ordType, char side = Side.BUY, decimal orderQty = 0,
            decimal? price = null, decimal? leverage = null, decimal? stopPx = null, decimal? takePx = null, string nettingPositionId = null)
        {
            var guid = Guid.NewGuid().ToString();

            NewOrderSingle m = new NewOrderSingle();

            m.ClOrdID = new ClOrdID(guid); //QueryClOrdID();
            m.TransactTime = new TransactTime(DateTime.UtcNow);

            m.Symbol = new Symbol(securityId);
            m.SecurityID = new SecurityID(securityId);

            //newOrderSingle.SecurityIDSource = new SecurityIDSource("G");

            m.Side = new Side(side);
            m.OrderQty = new OrderQty(orderQty);
            m.OrdType = new OrdType(ordType);

            if (stopPx.HasValue)
                m.StopPx = new StopPx(stopPx.Value);

            if (takePx.HasValue)
                m.SetField(new DecimalField(TAG_TakePx) {Obj = takePx.Value});

            if (ordType != OrdType.MARKET)
            {
                if (!price.HasValue)
                    throw new ArgumentNullException("price", "price cannot be null when ordType is not 1 (MARKET)");

                m.Price = new Price(price.Value);
            }

            if (string.IsNullOrEmpty(nettingPositionId))
                m.TargetStrategy = new TargetStrategy(5000);
            else
            {
                m.TargetStrategy = new TargetStrategy(5001);
                m.TargetStrategyParameters = new TargetStrategyParameters(nettingPositionId);
            }

            m.Account = new Account(account);

            if (leverage.HasValue)
                m.SetField(new DecimalField(TAG_Leverage) {Obj = leverage.Value});

            SendMessage(m);

            return guid;
        }

        public string OrderCancelReplaceRequest(string account, string securityId, string orderId, decimal price)
        {
            var guid = Guid.NewGuid().ToString();

            OrderCancelReplaceRequest m = new OrderCancelReplaceRequest();

            m.ClOrdID = new ClOrdID(guid);
            m.TransactTime = new TransactTime(DateTime.UtcNow);
            m.Symbol = new Symbol(securityId);
            m.SecurityID = new SecurityID(securityId);
            m.SecurityIDSource = new SecurityIDSource("G");

            //ignored
            m.Side = new Side(Side.BUY);

            m.Price = new Price(price);

            m.Account = new Account(account);

            //ignored
            m.OrdType = new OrdType(OrdType.STOP);

            m.OrigClOrdID = new OrigClOrdID(guid);

            m.OrderID = new OrderID(orderId);

            SendMessage(m);

            return guid;
        }

        public string OrderCancelRequest(string account, string securityId, string orderId)
        {
            var guid = Guid.NewGuid().ToString();

            OrderCancelRequest m = new OrderCancelRequest();

            m.ClOrdID = new ClOrdID(guid);
            m.OrderID = new OrderID(orderId);
            m.TransactTime = new TransactTime(DateTime.UtcNow);

            m.Symbol = new Symbol(securityId);
            m.SecurityID = new SecurityID(securityId);
            m.SecurityIDSource = new SecurityIDSource("G");

            m.Side = new Side(Side.BUY); //ignored

            m.OrderQty = new OrderQty(1); //ignored
            m.OrigClOrdID = new OrigClOrdID(guid); //ignored

            m.Account = new Account(account);

            SendMessage(m);
            return guid;
        }

        public string MDS5BalanceRequest(string account)
        {
            var guid = Guid.NewGuid().ToString();

            var m = new Message();
            m.Header.SetField(new MsgType("MDS5"));
            m.SetField(new StringField(TAG_MDS_RequestID) {Obj = guid});
            m.SetField(new Account(account));
            SendMessage(m);

            return guid;
        }

        #region console test method

        public void Run()
        {
            //CFDGlobal.LogLine("initiator thread id "+Thread.CurrentThread.ManagedThreadId.ToString());

            while (true)
            {
                try
                {
                    char action = QueryAction();

                    if (action == (char)0)
                        ShowInfo();
                    else if (action == '1')
                        QueryEnterOrder();
                    else if (action == '2')
                        QueryReplaceOrder();
                    else if (action == '3')
                        QueryPositionReport();
                    else if (action == '4')
                        QueryBalance();
                    else if (action == '5')
                        QueryOrderMassStatus();
                    else if (action == '6')
                        QueryLogIn();
                    else if (action == '7')
                        QueryLogOut();
                    else if (action == '8')
                        QueryCancelOrder();
                    else if (action == '9')
                        QueryPositionHistory();
                    else if (action == 'c')
                        PrintCacheStatus();//show cache status
                    else if (action == 'r')
                        GetPositionReport(); //get open positions by calling trade service
                    else if (action == 'h')
                        GetPositionHistoryReport(); //get closed positions by calling trade service
                    else if (action == 'q' || action == 'Q')
                        break;
                    else if (action == 't')
                        TestUserLogin();
                    else if (action == 'p')
                        TestPositionReport();
                    else if (action == 'd')
                        TestNewOrder();
                    else if (action == 'a')
                        QueryMDS3();
                }
                catch (System.Exception e)
                {
                    Console.WriteLine("Message Not Sent: " + e.Message);
                    Console.WriteLine("StackTrace: " + e.StackTrace);
                }
            }
            Console.WriteLine("Program shutdown.");
        }

        private void QueryMDS3()
        {
            var guid = Guid.NewGuid().ToString();

            var m = new Message();
            m.Header.SetField(new MsgType("MDS3"));
            m.SetField(new StringField(TAG_MDS_RequestID) { Obj = guid });
            m.SetField(new Account(_account));

            var transferType = _dd.FieldsByName["MDS_TransferType"];
            m.SetField(new IntField(transferType.Tag){Obj = Convert.ToInt32(transferType.EnumDict.FirstOrDefault(o=>o.Value=="CUP_DEPOSIT").Key)});

            var transferAmount=_dd.FieldsByName["MDS_TransferAmount"];
            m.SetField(new DecimalField(transferAmount.Tag){Obj = 1.23m});

            var transferCurrency=_dd.FieldsByName["MDS_TransferCurrency"];
            m.SetField(new StringField(transferCurrency.Tag) { Obj = "USD" });
            
      //<field name="MDS_TransferLabel" required="N"/>
      //<field name="MDS_SourceBalanceID" required="N"/>

      //<field name="MDS_TargetBalanceID" required="N"/>
            var targetBalanceId = _dd.FieldsByName["MDS_TargetBalanceID"];
            m.SetField(new StringField(targetBalanceId.Tag) {Obj = _balanceId});

      //<field name="MDS_Actor" required="N"/>
      //<field name="MDS_CardType" required="N"/>
      //<field name="MDS_CardAlias" required="N"/>

            SendMessage(m);

            //return guid;
        }

        private void TestNewOrder()
        {
            foreach (var pair in UsernameAccounts)
            {
                var account = pair.Value;

                NewOrderSingle(account,"34820",'1','2',1,leverage:1);
            }
        }

        private void TestPositionReport()
        {
            foreach (var pair in UsernameAccounts)
            {
                var account = pair.Value;

                RequestForPositions(account);
            }
        }

        private void TestUserLogin()
        {
            var db = CFDEntities.Create();
            //var users = db.Users.Where(o => o.Id >= 2042 && o.Id <= 2087).ToList();
            var users = db.Users.Where(o => o.Id >= 2092 && o.Id <= 2141).ToList();

            IList<string> loginReqIds = new List<string>();
            foreach (var user in users)
            {
                var logOnReqId = LogOn(user.AyondoUsername, user.AyondoPassword);
                loginReqIds.Add(logOnReqId);
            }

            var dtLogon = DateTime.UtcNow;
            do
            {
                Thread.Sleep(1000);

                if (users.All(u => UsernameAccounts.ContainsKey(u.AyondoUsername)))
                    break;
            } while (DateTime.UtcNow - dtLogon <= TimeSpan.FromSeconds(60)); // timeout
        }

        private char QueryAction()
        {
            HashSet<string> validActions = new HashSet<string>("1,2,3,4,5,6,7,8,9,q,Q,r,h,t,p,c,d,a".Split(','));

            string cmd = Console.ReadLine().Trim();
            if (cmd.Length != 1 || validActions.Contains(cmd) == false)
                return (char) 0;

              return cmd.ToCharArray()[0];
        }

        private void ShowInfo()
        {
            // Commands 'g' and 'x' are intentionally hidden.
            Console.Write("\n"
                          + "1) Enter Order\n"
                          + "2) Replace Order\n"
                          + "3) Position Report\n"
                          + "4) Balance\n"
                          + "5) Order Mass Status\n"
                          + "6) Log In\n"
                          + "7) Log Out\n"
                          + "8) Cancel Order\n"
                          + "9) Position History\n"
                          + "Q) Quit\n"
                          + "Action: "
                );
        }

        private void QueryLogIn()
        {
            var m = new UserRequest();
            m.UserRequestType = new UserRequestType(UserRequestType.LOGONUSER);
            m.Username = QueryUsername();
            m.Password = QueryPassword();
            m.UserRequestID = new UserRequestID("login:" + m.Username);
            m.SetField(new StringField(_dd.FieldsByName["MDS_SendColRep"].Tag) {Obj = "N"});
            m.SetField(new StringField(_dd.FieldsByName["MDS_SendNoPos"].Tag) {Obj = "0"});
            SendMessage(m);

            //m.UserRequestID=new UserRequestID(Guid.NewGuid().ToString());
            //SendMessage(m);
        }

        private void QueryLogOut()
        {
            var m = new UserRequest();
            //m.UserRequestID = new UserRequestID("logout:" + "ayondodemo01");
            m.UserRequestType = new UserRequestType(UserRequestType.LOGOFFUSER);
            m.Username = QueryUsername(); //any value will work
            //m.Password = QueryPassword();
            m.UserRequestID = new UserRequestID("logout:" + _account);
            //m.Username = new Username("ayondodemo01");
            //m.Password = new Password("demo2016!");
            //m.SetField(new StringField(DD.FieldsByName["MDS_SendColRep"].Tag) { Obj = "N" });
            //m.SetField(new StringField(DD.FieldsByName["MDS_SendNoPos"].Tag) { Obj = "0" });
            m.SetField(new Account(_account));
            //m.SetField(QueryAccount());
            SendMessage(m);
        }

        private void QueryPositionReport()
        {
            var m = new RequestForPositions();
            m.PosReqID = new PosReqID(Guid.NewGuid().ToString());
            m.PosReqType = new PosReqType(PosReqType.POSITIONS);
            m.ClearingBusinessDate = new ClearingBusinessDate("0-0-0");
            m.TransactTime = new TransactTime(DateTime.UtcNow);
            m.Account = new Account(_account);
            m.AccountType = new AccountType(AccountType.ACCOUNT_IS_CARRIED_ON_CUSTOMER_SIDE_OF_BOOKS);
            SendMessage(m);

            ////testing
            //m.PosReqID = new PosReqID(Guid.NewGuid().ToString());
            //SendMessage(m);
            //m.PosReqID = new PosReqID(Guid.NewGuid().ToString());
            //SendMessage(m);
            //m.PosReqID = new PosReqID(Guid.NewGuid().ToString());
            //SendMessage(m);
            //m.PosReqID = new PosReqID(Guid.NewGuid().ToString());
            //SendMessage(m);
            //m.PosReqID = new PosReqID(Guid.NewGuid().ToString());
            //SendMessage(m);
            //m.PosReqID = new PosReqID(Guid.NewGuid().ToString());
            //SendMessage(m);
            //m.PosReqID = new PosReqID(Guid.NewGuid().ToString());
            //SendMessage(m);
            //m.PosReqID = new PosReqID(Guid.NewGuid().ToString());
            //SendMessage(m);
            //m.PosReqID = new PosReqID(Guid.NewGuid().ToString());
            //SendMessage(m);
        }

        private void QueryOrderMassStatus()
        {
            var m = new OrderMassStatusRequest();
            m.MassStatusReqID = new MassStatusReqID("orderMass:" + _account);
            m.MassStatusReqType = new MassStatusReqType(MassStatusReqType.STATUS_FOR_ALL_ORDERS);
            m.Account = new Account(_account);
            SendMessage(m);
        }

        private void QueryBalance()
        {
            var m = new Message();
            m.Header.SetField(new MsgType("MDS5"));
            m.SetField(new StringField(TAG_MDS_RequestID) { Obj = "balance:" + _account });
            m.SetField(new Account(_account));
            SendMessage(m);
        }

        private void QueryEnterOrder()
        {
            Console.WriteLine("\nNewOrderSingle");

            NewOrderSingle m = QueryNewOrderSingle44();

            if (m != null && QueryConfirm("Send order"))
            {
                m.Header.GetField(Tags.BeginString);

                SendMessage(m);
            }
        }

        private void QueryReplaceOrder()
        {
            Console.WriteLine("\nCancelReplaceRequest");

            OrderCancelReplaceRequest m = QueryCancelReplaceRequest44();

            if (m != null && QueryConfirm("Send replace"))
                SendMessage(m);
        }

        private void QueryCancelOrder()
        {
            Console.WriteLine("\nCancelReplaceRequest");

            OrderCancelRequest m = QueryCancelRequest44();

            if (m != null && QueryConfirm("Send replace"))
                SendMessage(m);
        }

        private void QueryPositionHistory()
        {
            Message m = new Message();
            m.Header.SetField(new MsgType("MDS7"));
            m.SetField(new StringField(TAG_MDS_RequestID) {Obj = Guid.NewGuid().ToString()});
            m.SetField(new Account(_account));
            m.SetField(new IntField(TAG_MDS_HistoryType) {Obj = 1});
            m.SetField(new IntField(TAG_MDS_StartTime) {Obj = (int) (DateTimes.GetHistoryQueryStartTime(DateTime.UtcNow)).ToUnixTime()});
            m.SetField(new IntField(TAG_MDS_EndTime) {Obj = (int) (DateTime.UtcNow).ToUnixTime()});

            //m.SetField(new IntField(7945) {Obj = 99999});
            //m.SetField(new IntField(7946) {Obj = 0});

            SendMessage(m);
        }

        private void PrintCacheStatus()
        {
            //Console.Write("account");
            //string account = Console.ReadLine();
            ////Console.Write(CFDCacheManager.Instance.PrintStatus(account));
            //Console.Write(CFDCacheManager.Instance.PrintStatusHtml(account));
            AyondoTradeService svr = new AyondoTradeService();
            Console.Write(svr.PrintCache("thcn2031"));
        }

        private void GetPositionReport()
        {
            AyondoTradeService svr = new AyondoTradeService();
            svr.GetPositionReport("thcn2031", "yJUKrh");
        }

        private void GetPositionHistoryReport()
        {
            AyondoTradeService svr = new AyondoTradeService();
            svr.GetPositionHistoryReport("thcn2031", "yJUKrh", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);
        }

        private bool QueryConfirm(string query)
        {
            Console.WriteLine();
            Console.WriteLine(query + "?: ");
            string line = Console.ReadLine().Trim();

            if (line == "") return false;

            return (line[0].Equals('y') || line[0].Equals('Y'));
        }

        private QuickFix.FIX44.NewOrderSingle QueryNewOrderSingle44()
        {
            QuickFix.Fields.OrdType ordType = null;

            //var newOrderSingle = new NewOrderSingle(
            //    new ClOrdID("newOrderSingle123"),
            //    new Symbol("12956"),
            //    new Side(Side.BUY),
            //    new TransactTime(DateTime.UtcNow),
            //    new OrdType('1'));
            //newOrderSingle.SecurityID = new SecurityID("12956");
            //newOrderSingle.SecurityIDSource = new SecurityIDSource("G");
            //newOrderSingle.Price = new Price(123.123m);
            //newOrderSingle.OrderQty = new OrderQty((decimal) 1);
            ////newOrderSingle.TargetStrategy = new TargetStrategy(5001);
            ////newOrderSingle.TargetStrategyParameters = new TargetStrategyParameters("138604815797");
            //newOrderSingle.TargetStrategy = new TargetStrategy(5000);
            //newOrderSingle.Account = new Account(response.GetString(Tags.Account));
            //Session.Send(newOrderSingle);

            QuickFix.FIX44.NewOrderSingle newOrderSingle = new QuickFix.FIX44.NewOrderSingle();
            newOrderSingle.ClOrdID = new ClOrdID("newOrderSingle:" + _account); //QueryClOrdID();
            newOrderSingle.TransactTime = new TransactTime(DateTime.UtcNow);

            var secId = QuerySymbol();
            //if (!string.IsNullOrEmpty(secId))
            //{
            newOrderSingle.Symbol = new Symbol(secId);
            newOrderSingle.SecurityID = new SecurityID(secId);
            //}

            //newOrderSingle.SecurityIDSource = new SecurityIDSource("G");

            newOrderSingle.Side = QuerySide();
            newOrderSingle.OrderQty = QueryOrderQty();
            newOrderSingle.OrdType = QueryOrdType(); // new OrdType('1');

            var queryStopPx = QueryStopPx();
            if (!string.IsNullOrEmpty(queryStopPx))
                newOrderSingle.StopPx = new StopPx(Convert.ToDecimal(queryStopPx));

            var queryTakePx = QueryTakePx();
            if (!string.IsNullOrEmpty(queryTakePx))
                newOrderSingle.SetField(new DecimalField(_dd.FieldsByName["TakePx"].Tag) {Obj = Convert.ToDecimal(queryTakePx)});

            if ((!string.IsNullOrEmpty(queryStopPx) || !string.IsNullOrEmpty(queryTakePx)) || newOrderSingle.OrdType.Obj != 1)
            {
                var queryPrice = QueryPrice();
                if (!string.IsNullOrEmpty(queryPrice))
                    newOrderSingle.Price = new Price(Convert.ToDecimal(queryPrice));
            }

            newOrderSingle.TargetStrategy = QueryTargetStrategy();
            if (newOrderSingle.TargetStrategy.Obj == 5001)
            {
                newOrderSingle.TargetStrategyParameters = QueryTargetStrategyParameters();
            }

            newOrderSingle.Account = new Account(_account);

            //newOrderSingle.Set(new HandlInst('1'));
            //newOrderSingle.Set(QueryTimeInForce());
            //if (ordType.getValue() == OrdType.LIMIT || ordType.getValue() == OrdType.STOP_LIMIT)
            //    newOrderSingle.Set(QueryPrice());
            //if (ordType.getValue() == OrdType.STOP || ordType.getValue() == OrdType.STOP_LIMIT)
            //    newOrderSingle.Set(QueryStopPx());

            return newOrderSingle;
        }

        private QuickFix.FIX44.OrderCancelReplaceRequest QueryCancelReplaceRequest44()
        {
            OrderCancelReplaceRequest m = new OrderCancelReplaceRequest();

            m.ClOrdID = QueryClOrdID();
            m.TransactTime = new TransactTime(DateTime.UtcNow);
            var symbol = QuerySymbol();
            m.Symbol = new Symbol(symbol);
            m.SecurityID = new SecurityID(symbol);
            m.SecurityIDSource = new SecurityIDSource("G");

            m.Side = QuerySide();

            var price = QueryPrice();
            if (!string.IsNullOrEmpty(price))
                m.Price = new Price(Convert.ToDecimal(price));

            var queryStopPx = QueryStopPx();
            if (!string.IsNullOrEmpty(queryStopPx))
                m.StopPx = new StopPx(Convert.ToDecimal(queryStopPx));

            var queryTakePx = QueryTakePx();
            if (!string.IsNullOrEmpty(queryTakePx))
                m.SetField(new DecimalField(_dd.FieldsByName["TakePx"].Tag) {Obj = Convert.ToDecimal(queryTakePx)});

            m.Account = new Account(_account);

            m.OrdType = QueryOrdType(); // new OrdType('1');
            m.OrigClOrdID = QueryOrigClOrdID();
            m.OrderID = QueryOrderID();

            return m;
        }

        private OrderCancelRequest QueryCancelRequest44()
        {
            OrderCancelRequest m = new OrderCancelRequest();

            m.ClOrdID = QueryClOrdID();
            m.OrderID = QueryOrderID();
            m.TransactTime = new TransactTime(DateTime.UtcNow);

            var symbol = QuerySymbol();
            m.Symbol = new Symbol(symbol);
            m.SecurityID = new SecurityID(symbol);
            m.SecurityIDSource = new SecurityIDSource("G");

            var price = QueryPrice();
            if (!string.IsNullOrEmpty(price))
                m.SetField(new Price(Convert.ToDecimal(price)));

            m.Side = QuerySide();

            m.OrderQty = QueryOrderQty();
            m.OrigClOrdID = QueryOrigClOrdID();

            m.Account = new Account(_account);

            return m;
        }

        #endregion

        private void SendMessage(Message m)
        {
            if (Session != null)
            {
                try
                {
                    Session.Send(m);
                }
                catch (DoNotSend)
                {
                    CFDGlobal.LogLine("DoNotSend Caught: " + m.ToString());
                }
            }
            else
            {
                //// This probably won't ever happen.
                //Console.WriteLine("Can't send message: session not created.");

                throw new Exception("fix session is null. fix not logged on yet.");
            }
        }

        public string GetMessageString(Message message, bool showHeader = false, bool showTrailer = false)
        {
            if (_dd == null)
                return message.ToString();

            var sb = new StringBuilder();
            if (showHeader)
            {
                //sb.AppendLine("--------------------fix message-------------------");
                foreach (KeyValuePair<int, IField> pair in message.Header)
                {
                    var field = _dd.FieldsByTag[pair.Key];
                    var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                    sb.AppendLine(field.Name + "=" + value);
                }
                sb.AppendLine("------");
            }

            var groupTags = message.GetGroupTags();

            foreach (KeyValuePair<int, IField> pair in message)
            {
                var field = _dd.FieldsByTag[pair.Key];

                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                sb.AppendLine(field.Name + "=" + value);

                if (groupTags.Contains(pair.Key))
                {
                    var @group = message.GetGroup(1, pair.Key);
                    foreach (var item in @group)
                    {
                        var subField = _dd.FieldsByTag[item.Key];

                        var subValue = subField.HasEnums() ? subField.EnumDict[item.Value.ToString()] + "(" + item.Value + ")" : item.Value.ToString();
                        sb.AppendLine("\t" + subField.Name + "=" + subValue);
                    }

                    //sb.AppendLine("END GROUP");
                }
            }

            if (showTrailer)
            {
                sb.AppendLine("------");

                foreach (KeyValuePair<int, IField> pair in message.Trailer)
                {
                    var field = _dd.FieldsByTag[pair.Key];
                    var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                    sb.AppendLine(field.Name + "=" + value);
                }
            }
            sb.AppendLine("--------------------------------------------------");
            return sb.ToString();
        }

        #region field query private methods

        private ClOrdID QueryClOrdID()
        {
            Console.WriteLine();
            Console.Write("ClOrdID? ");
            return new ClOrdID(Console.ReadLine().Trim());
        }

        private OrderID QueryOrderID()
        {
            Console.WriteLine();
            Console.Write("OrderID? ");
            return new OrderID(Console.ReadLine().Trim());
        }

        private OrigClOrdID QueryOrigClOrdID()
        {
            Console.WriteLine();
            Console.Write("OrigClOrdID? ");
            return new OrigClOrdID(Console.ReadLine().Trim());
        }

        private string QuerySymbol()
        {
            Console.WriteLine();
            Console.Write("Symbol(SecId)? ");
            return Console.ReadLine().Trim();
        }

        private Side QuerySide()
        {
            Console.WriteLine();
            Console.WriteLine("1) Buy");
            Console.WriteLine("2) Sell");
            Console.Write("Side? ");
            string s = Console.ReadLine().Trim();

            char c = ' ';
            switch (s)
            {
                case "1":
                    c = Side.BUY;
                    break;
                case "2":
                    c = Side.SELL;
                    break;
                default:
                    throw new Exception("unsupported input");
            }
            return new Side(c);
        }

        private OrdType QueryOrdType()
        {
            Console.WriteLine();
            Console.WriteLine("1) Market");
            Console.WriteLine("2) Limit");
            Console.WriteLine("3) Stop");
            Console.WriteLine("4) Take");
            //Console.WriteLine("4) Take");
            Console.Write("OrdType? ");
            string s = Console.ReadLine().Trim();

            char c = ' ';
            switch (s)
            {
                case "1":
                    c = OrdType.MARKET;
                    break;
                case "2":
                    c = OrdType.LIMIT;
                    break;
                case "3":
                    c = OrdType.STOP;
                    break;
                case "4":
                    c = '4';
                    break;
                //case "2":;
                default:
                    throw new Exception("unsupported input");
            }
            return new OrdType(c);
        }

        private OrderQty QueryOrderQty()
        {
            Console.WriteLine();
            Console.Write("OrderQty? ");
            return new OrderQty(Convert.ToDecimal(Console.ReadLine().Trim()));
        }

        private string QueryPrice()
        {
            Console.WriteLine();
            Console.Write("Price? ");
            return Console.ReadLine().Trim();
        }

        private string QueryStopPx()
        {
            Console.WriteLine();
            Console.Write("StopPx? ");
            return Console.ReadLine().Trim();
        }

        private string QueryTakePx()
        {
            Console.WriteLine();
            Console.Write("TakePx? ");
            return Console.ReadLine().Trim();
        }

        private TargetStrategy QueryTargetStrategy()
        {
            Console.WriteLine();
            Console.Write("TargetStrategy(5000:new, 5001:exist position)? ");
            return new TargetStrategy(Convert.ToInt32(Console.ReadLine().Trim()));
        }

        private TargetStrategyParameters QueryTargetStrategyParameters()
        {
            Console.WriteLine();
            Console.Write("TargetStrategyParameters? ");
            return new TargetStrategyParameters(Console.ReadLine().Trim());
        }

        private Username QueryUsername()
        {
            Console.WriteLine();
            Console.Write("Username? ");
            return new Username(Console.ReadLine().Trim());
        }

        private Password QueryPassword()
        {
            Console.WriteLine();
            Console.Write("Password? ");
            return new Password(Console.ReadLine().Trim());
        }

        private Account QueryAccount()
        {
            Console.WriteLine();
            Console.Write("Account? ");
            return new Account(Console.ReadLine().Trim());
        }

        #endregion
    }

    public class FIXUser
    {
        public RequestForPositionsAck RequestForPositionsAck { get; set; }
        public IList<PositionReport> PositionReports { get; set; }
    }
}