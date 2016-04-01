using System;
using System.Collections.Generic;
using System.Text;
using CFD_COMMON;
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

        #region IApplication members

        public void ToAdmin(Message message, SessionID sessionID)
        {
            //CFDGlobal.LogLine("ToAdmin: ");
            //CFDGlobal.LogLine(message.ToString());

            if (message.Header.GetString(Tags.MsgType) == MsgType.LOGON)
            {
                //CFDGlobal.LogLine(" sending username and password...");
                message.SetField(new Username("thcntrade"));
                message.SetField(new Password("d093gos3j"));
            }
        }

        public void FromAdmin(Message message, SessionID sessionID)
        {
            if (message.Header.GetString(Tags.MsgType) != MsgType.HEARTBEAT)
            {
                CFDGlobal.LogLine("FromAdmin: ");
                //CFDGlobal.LogLine(message.ToString());
                CFDGlobal.LogLine(GetMessageString(message));
            }
        }

        public void ToApp(Message message, SessionID sessionId)
        {
            CFDGlobal.LogLine("ToApp: ");
            CFDGlobal.LogLine(message.ToString());
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            //CFDGlobal.LogLine("FromApp: ");
            //CFDGlobal.LogLine(message.ToString());
            //CFDGlobal.LogLine(GetMessageString(message));

            var msgType = message.Header.GetString(Tags.MsgType);
            if (msgType == "MDS6")
            {
                CFDGlobal.LogLine("MDS6:BalanceResponse");
                CFDGlobal.LogLine(GetMessageString(message));
            }
            else
                Crack(message, sessionID);
        }

        public void OnCreate(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnCreate: " + sessionID);
        }

        public void OnLogout(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnLogout: " + sessionID);
        }

        public void OnLogon(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnLogon: " + sessionID);

            Session = Session.LookupSession(sessionID);
            DD = Session.ApplicationDataDictionary;
        }

        #endregion

        #region OnMessage methods

        public void OnMessage(News news, SessionID sessionID)
        {
            CFDGlobal.LogLine("OnMessage:News ");
            CFDGlobal.LogLine(GetMessageString(news));
        }

        public void OnMessage(UserResponse response, SessionID sessionID)
        {
            CFDGlobal.LogLine("OnMessage:UserResponse ");
            CFDGlobal.LogLine(GetMessageString(response));
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

            CFDGlobal.LogLine("OnMessage:CollateralReport ");
            CFDGlobal.LogLine(GetMessageString(report));
        }

        public void OnMessage(ExecutionReport report, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:ExecutionReport ");
            CFDGlobal.LogLine(GetMessageString(report));
        }

        public void OnMessage(RequestForPositionsAck requestAck, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:RequestForPositionsAck ");
            CFDGlobal.LogLine(GetMessageString(requestAck));
        }

        public void OnMessage(PositionReport report, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:PositionReport ");
            CFDGlobal.LogLine(GetMessageString(report));
            //var groupTags = report.GetGroupTags();
            //var noPositionsGroup = new PositionReport.NoPositionsGroup();
            //var @group2 = report.GetGroup(1, noPositionsGroup);
        }

        public void OnMessage(BusinessMessageReject reject, SessionID session)
        {
            CFDGlobal.LogLine(":OnMessage:BusinessMessageReject");
            CFDGlobal.LogLine(GetMessageString(reject));
        }

        #endregion

        public void Run()
        {
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

        private void QueryLogIn()
        {
            var m = new UserRequest();
            m.UserRequestID = new UserRequestID("login:" + "ayondodemo01");
            m.UserRequestType = new UserRequestType(UserRequestType.LOGONUSER);
            m.Username = new Username("ayondodemo01");
            m.Password = new Password("demo2016!");
            m.SetField(new StringField(DD.FieldsByName["MDS_SendColRep"].Tag) { Obj = "N" });
            m.SetField(new StringField(DD.FieldsByName["MDS_SendNoPos"].Tag) { Obj = "0" });
            SendMessage(m);
        }

        private void QueryLogOut()
        {
            var m = new UserRequest();
            m.UserRequestID = new UserRequestID("logout:" + "ayondodemo01");
            m.UserRequestType = new UserRequestType(UserRequestType.LOGOFFUSER);
            m.Username = new Username("ayondodemo01");
            m.Password = new Password("demo2016!");
            m.SetField(new StringField(DD.FieldsByName["MDS_SendColRep"].Tag) { Obj = "N" });
            m.SetField(new StringField(DD.FieldsByName["MDS_SendNoPos"].Tag) { Obj = "0" });
            m.SetField(new Account("136824778776"));
            SendMessage(m);
        }

        private void QueryPositionReport()
        {
            var m = new RequestForPositions();
            m.PosReqID = new PosReqID("posreq123");
            m.PosReqType = new PosReqType(PosReqType.POSITIONS);
            m.ClearingBusinessDate = new ClearingBusinessDate("0-0-0");
            m.TransactTime = new TransactTime(DateTime.UtcNow);
            m.Account = new Account("136824778776");
            m.AccountType = new AccountType(AccountType.ACCOUNT_IS_CARRIED_ON_CUSTOMER_SIDE_OF_BOOKS);
            SendMessage(m);
        }

        private void QueryOrderMassStatus()
        {
            var m = new OrderMassStatusRequest();
            m.MassStatusReqID = new MassStatusReqID("orderMass:" + "136824778776");
            m.MassStatusReqType = new MassStatusReqType(MassStatusReqType.STATUS_FOR_ALL_ORDERS);
            m.Account = new Account("136824778776");
            SendMessage(m);
        }

        private void QueryBalance()
        {
            var m = new Message();
            m.Header.SetField(new MsgType("MDS5"));
            m.SetField(new StringField(DD.FieldsByName["MDS_RequestID"].Tag) {Obj = "balance:" + "136824778776"});
            m.SetField(new Account("136824778776"));
            SendMessage(m);
        }

        private void QueryEnterOrder()
        {
            Console.WriteLine("\nNewOrderSingle");

            QuickFix.FIX44.NewOrderSingle m = QueryNewOrderSingle44();

            if (m != null && QueryConfirm("Send order"))
            {
                m.Header.GetField(Tags.BeginString);

                SendMessage(m);
            }
        }

        private void QueryReplaceOrder()
        {
            Console.WriteLine("\nCancelReplaceRequest");

            QuickFix.FIX44.OrderCancelReplaceRequest m = QueryCancelReplaceRequest44();

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

        private void SendMessage(Message m)
        {
            if (Session != null)
                Session.Send(m);
            else
            {
                // This probably won't ever happen.
                Console.WriteLine("Can't send message: session not created.");
            }
        }

        private char QueryAction()
        {
            HashSet<string> validActions = new HashSet<string>("1,2,3,4,5,6,7,q,Q,g,x".Split(','));

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
                          + "Q) Quit\n"
                          + "Action: "
                );
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
            newOrderSingle.ClOrdID = new ClOrdID("newOrderSingle:" + "136824778776"); //QueryClOrdID();
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
            newOrderSingle.OrdType = new OrdType('1');

            var queryStopPx = QueryStopPx();
            if (!string.IsNullOrEmpty(queryStopPx))
                newOrderSingle.StopPx = new StopPx(Convert.ToDecimal(queryStopPx));

            var queryTakePx = QueryTakePx();
            if (!string.IsNullOrEmpty(queryTakePx))
                newOrderSingle.SetField(new DecimalField(DD.FieldsByName["TakePx"].Tag) {Obj = Convert.ToDecimal(queryTakePx)});

            //if (!string.IsNullOrEmpty(queryStopPx) || !string.IsNullOrEmpty(queryTakePx))
            //{
            //    var queryPrice = QueryPrice();
            //    if(!string.IsNullOrEmpty(queryPrice))
            //    newOrderSingle.Price =new Price(Convert.ToDecimal( queryPrice));
            //}

            newOrderSingle.TargetStrategy = QueryTargetStrategy();
            if (newOrderSingle.TargetStrategy.Obj == 5001)
            {
                newOrderSingle.TargetStrategyParameters = QueryTargetStrategyParameters();
            }

            newOrderSingle.Account = new Account("136824778776");

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
            m.TransactTime=new TransactTime(DateTime.UtcNow);
            var symbol = QuerySymbol();
            m.Symbol=new Symbol(symbol);
            m.SecurityID=new SecurityID(symbol);
            m.SecurityIDSource=new SecurityIDSource("G");

            m.Side = QuerySide();

            //var price = QueryPrice();
            //if(!string.IsNullOrEmpty(price))
            //m.Price =new Price(Convert.ToDecimal(price));

            var queryStopPx = QueryStopPx();
            if (!string.IsNullOrEmpty(queryStopPx))
                m.StopPx = new StopPx(Convert.ToDecimal(queryStopPx));

            var queryTakePx = QueryTakePx();
            if (!string.IsNullOrEmpty(queryTakePx))
                m.SetField(new DecimalField(DD.FieldsByName["TakePx"].Tag) { Obj = Convert.ToDecimal(queryTakePx) });

            m.Account = new Account("136824778776");


            m.OrdType = new OrdType('1');
            m.OrigClOrdID = QueryOrigClOrdID();

            return m;
        }

        private string GetMessageString(Message message)
        {
            if (DD == null)
                return message.ToString();

            var sb = new StringBuilder();
            //sb.AppendLine("--------------------fix message-------------------");
            //foreach (KeyValuePair<int, IField> pair in message.Header)
            //{
            //    var field = DD.FieldsByTag[pair.Key];
            //    var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
            //    sb.AppendLine(field.Name + "=" + value);
            //}
            //sb.AppendLine("");

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
            //sb.AppendLine("");

            //foreach (KeyValuePair<int, IField> pair in message.Trailer)
            //{
            //    var field = DD.FieldsByTag[pair.Key];
            //    var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
            //    sb.AppendLine(field.Name + "=" + value);
            //}
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

        #endregion
    }
}