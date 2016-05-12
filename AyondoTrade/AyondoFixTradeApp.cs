using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
using QuickFix;
using QuickFix.DataDictionary;
using QuickFix.Fields;
using QuickFix.FIX44;
using Message = QuickFix.Message;

namespace CFD_JOBS.Ayondo
{
    public class AyondoFixTradeApp : MessageCracker, IApplication
    {
        public Session Session { get; set; }
        private DataDictionary DD;

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

        public IDictionary<string, string> UsernameAccounts = new Dictionary<string, string>();
        public IDictionary<string, string> AccountUsernames = new Dictionary<string, string>();

        //public ConcurrentDictionary<string, UserResponse> UserResponses = new ConcurrentDictionary<string, UserResponse>();
        public ConcurrentDictionary<string, RequestForPositionsAck> RequestForPositionsAcks = new ConcurrentDictionary<string, RequestForPositionsAck>();
        public ConcurrentDictionary<string, IList<PositionReport>> PositionReports = new ConcurrentDictionary<string, IList<PositionReport>>();
        public ConcurrentDictionary<string, IList<PositionReport>> OrderPositionReports = new ConcurrentDictionary<string, IList<PositionReport>>();

        public ConcurrentDictionary<string, IList<KeyValuePair<DateTime, PositionReport>>> StopTakePositionReports =
            new ConcurrentDictionary<string, IList<KeyValuePair<DateTime, PositionReport>>>();

        public ConcurrentDictionary<string, BusinessMessageReject> BusinessMessageRejects = new ConcurrentDictionary<string, BusinessMessageReject>();
        public ConcurrentDictionary<string, ExecutionReport> RejectedExecutionReports = new ConcurrentDictionary<string, ExecutionReport>();

        public ConcurrentDictionary<string, IList<KeyValuePair<DateTime, ExecutionReport>>> StopTakeExecutionReports =
            new ConcurrentDictionary<string, IList<KeyValuePair<DateTime, ExecutionReport>>>();

        public ConcurrentDictionary<string, decimal> Balances = new ConcurrentDictionary<string, decimal>();

        private string _account;
        //ayondodemo01 136824778776
        //ivantradehero 138673044476

        #region IApplication members

        public void ToAdmin(Message message, SessionID sessionID)
        {
            string msgType = message.Header.GetString(Tags.MsgType);

            if (msgType == MsgType.LOGON)
            {
                //CFDGlobal.LogLine(" sending username and password...");
                message.SetField(new Username("thcntrade"));
                message.SetField(new Password("d093gos3j"));
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
                CFDGlobal.LogLine("FromAdmin: " + message.ToString());
                //CFDGlobal.LogLine(GetMessageString(message));
            }
        }

        public void ToApp(Message message, SessionID sessionId)
        {
            if (message.Header.GetString(Tags.MsgType) == MsgType.BUSINESS_MESSAGE_REJECT)
            {
                CFDGlobal.LogInformation("ToApp: " + message.ToString());
            }
            else
            {
                CFDGlobal.LogLine("ToApp: " + message.ToString());
            }
        }

        public void FromApp(Message message, SessionID sessionID)
        {
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
                    Balances.TryAdd(guid, quantity);
                }
                else
                    Crack(message, sessionID);
            }
            catch (Exception e)
            {
                CFDGlobal.LogLine(message.ToString());
                CFDGlobal.LogException(e);
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

            CFDGlobal.LogInformation("OnLogout: " + sessionID +" StackTrace: "+ sb.ToString());
        }

        public void OnLogon(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnLogon: " + sessionID);

            Session = Session.LookupSession(sessionID);
            DD = Session.ApplicationDataDictionary;

            TAG_MDS_SendColRep = DD.FieldsByName["MDS_SendColRep"].Tag;
            TAG_MDS_SendNoPos = DD.FieldsByName["MDS_SendNoPos"].Tag;
            TAG_StopOID = DD.FieldsByName["StopOID"].Tag;
            TAG_TakeOID = DD.FieldsByName["TakeOID"].Tag;
            TAG_TakePx = DD.FieldsByName["TakePx"].Tag;
            TAG_MDS_PL = DD.FieldsByName["MDS_PL"].Tag;
            TAG_Leverage = DD.FieldsByName["Leverage"].Tag;
            TAG_MDS_RequestID = DD.FieldsByName["MDS_RequestID"].Tag;
            TAG_MDS_UPL = DD.FieldsByName["MDS_UPL"].Tag;
        }

        #endregion

        #region OnMessage methods

        public void OnMessage(News news, SessionID sessionID)
        {
            CFDGlobal.LogLine("OnMessage:News: " + GetMessageString(news, true, true));
        }

        public void OnMessage(UserResponse response, SessionID sessionID)
        {
            CFDGlobal.LogLine("OnMessage:UserResponse: " + GetMessageString(response));

            //for console test
            var account = response.GetString(Tags.Account);
            if (!string.IsNullOrEmpty(account))
                _account = account;

            var username = response.Username.Obj;

            //add to onlinie user list
            if (response.UserStatus.Obj == UserStatus.LOGGED_IN)
            {
                if (UsernameAccounts.ContainsKey(username))
                    UsernameAccounts[username] = account;
                else
                    UsernameAccounts.Add(username, account);

                if (AccountUsernames.ContainsKey(account))
                    AccountUsernames[account] = username;
                else
                    AccountUsernames.Add(account, username);
            }
            else
                CFDGlobal.LogLine("UserResponse:UserStatus:" + response.UserStatus.Obj);
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
                RejectedExecutionReports.TryAdd(clOrdID, report);
            }
        }

        public void OnMessage(RequestForPositionsAck response, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:RequestForPositionsAck: " + GetMessageString(response));

            var guid = response.PosReqID.Obj;

            if (RequestForPositionsAcks.ContainsKey(guid))
            {
                throw new Exception("existed guid for RequestForPositionsAck");
            }

            RequestForPositionsAcks.TryAdd(guid, response);
        }

        public void OnMessage(PositionReport report, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:PositionReport: " + GetMessageString(report));
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
            if (posReqId == "Unsolicited")
            {
                if (report.Any(o => o.Key == Tags.ClOrdID)) //after order filled
                {
                    //Text=Position DELETE by MarketOrder
                    //Text=Position DELETE by TakeProfitOrder
                    //Text=Position DELETE by StopLossOrder

                    var clOrdID = report.GetString(Tags.ClOrdID);

                    if (report.Text.Obj == "Position DELETE by StopLossOrder" || report.Text.Obj == "Position DELETE by TakeProfitOrder") //by stop/take
                    {
                        try
                        {
                            var account = report.Account.Obj;
                            if (AccountUsernames.ContainsKey(account))
                            {
                                var username = AccountUsernames[account];
                                using (var db = CFDEntities.Create())
                                {
                                    var user = db.Users.FirstOrDefault(o => o.AyondoUsername == username);
                                    if (user != null && user.Phone != null)
                                    {
                                        var secId = Convert.ToInt32(report.SecurityID.Obj);
                                        var sec = db.AyondoSecurities.FirstOrDefault(o => o.Id == secId);
                                        var name = sec != null && sec.CName != null ? sec.CName : report.Symbol.Obj;
                                        var stopTake = report.Text.Obj == "Position DELETE by StopLossOrder" ? "止损" : "止盈";
                                        var price = report.SettlPrice;
                                        var pl = report.GetDecimal(TAG_MDS_PL);
                                        var sendSms = YunPianMessenger.SendSms("【MyHero运营】运营监控，您买的" + name + "已被" + stopTake + "在" + price + "，收益为" + pl.ToString("0.00") 
                                            + "，回T退订", user.Phone);
                                        CFDGlobal.LogInformation(sendSms);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            CFDGlobal.LogException(e);
                        }
                    }
                    else //by market order
                    {
                        if (OrderPositionReports.ContainsKey(clOrdID))
                            OrderPositionReports[clOrdID].Add(report);
                        else
                            OrderPositionReports.TryAdd(clOrdID, new List<PositionReport>() {report});
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
                }
            }
            else //after position report request
            {
                if (PositionReports.ContainsKey(posReqId))
                    PositionReports[posReqId].Add(report);
                else
                    PositionReports.TryAdd(posReqId, new List<PositionReport> {report});
            }
        }

        public void OnMessage(BusinessMessageReject reject, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:BusinessMessageReject: " + GetMessageString(reject, true, true));

            var guid = reject.BusinessRejectRefID.Obj;

            if (BusinessMessageRejects.ContainsKey(guid))
            {
                CFDGlobal.LogInformation("existed guid for BusinessMessageRejects");
            }
            else
                BusinessMessageRejects.TryAdd(guid, reject);
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

                    if (action == (char) 0)
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
                    else if (action == 'q' || action == 'Q')
                        break;
                }
                catch (System.Exception e)
                {
                    Console.WriteLine("Message Not Sent: " + e.Message);
                    Console.WriteLine("StackTrace: " + e.StackTrace);
                }
            }
            Console.WriteLine("Program shutdown.");
        }

        private char QueryAction()
        {
            HashSet<string> validActions = new HashSet<string>("1,2,3,4,5,6,7,8,q,Q,g,x".Split(','));

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
            m.SetField(new StringField(DD.FieldsByName["MDS_SendColRep"].Tag) {Obj = "N"});
            m.SetField(new StringField(DD.FieldsByName["MDS_SendNoPos"].Tag) {Obj = "0"});
            SendMessage(m);
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
            m.PosReqID = new PosReqID("pos_req:" + _account);
            m.PosReqType = new PosReqType(PosReqType.POSITIONS);
            m.ClearingBusinessDate = new ClearingBusinessDate("0-0-0");
            m.TransactTime = new TransactTime(DateTime.UtcNow);
            m.Account = new Account(_account);
            m.AccountType = new AccountType(AccountType.ACCOUNT_IS_CARRIED_ON_CUSTOMER_SIDE_OF_BOOKS);
            SendMessage(m);
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
            m.SetField(new StringField(DD.FieldsByName["MDS_RequestID"].Tag) {Obj = "balance:" + _account});
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
                newOrderSingle.SetField(new DecimalField(DD.FieldsByName["TakePx"].Tag) {Obj = Convert.ToDecimal(queryTakePx)});

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
                m.SetField(new DecimalField(DD.FieldsByName["TakePx"].Tag) {Obj = Convert.ToDecimal(queryTakePx)});

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
                Session.Send(m);
            else
            {
                //// This probably won't ever happen.
                //Console.WriteLine("Can't send message: session not created.");

                throw new Exception("fix session is null. fix not logged on yet.");
            }
        }

        private string GetMessageString(Message message, bool showHeader = false, bool showTrailer = false)
        {
            if (DD == null)
                return message.ToString();

            var sb = new StringBuilder();
            if (showHeader)
            {
                //sb.AppendLine("--------------------fix message-------------------");
                foreach (KeyValuePair<int, IField> pair in message.Header)
                {
                    var field = DD.FieldsByTag[pair.Key];
                    var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                    sb.AppendLine(field.Name + "=" + value);
                }
                sb.AppendLine("------");
            }

            var groupTags = message.GetGroupTags();

            foreach (KeyValuePair<int, IField> pair in message)
            {
                var field = DD.FieldsByTag[pair.Key];

                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                sb.AppendLine(field.Name + "=" + value);

                if (groupTags.Contains(pair.Key))
                {
                    var @group = message.GetGroup(1, pair.Key);
                    foreach (var item in @group)
                    {
                        var subField = DD.FieldsByTag[item.Key];

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
                    var field = DD.FieldsByTag[pair.Key];
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