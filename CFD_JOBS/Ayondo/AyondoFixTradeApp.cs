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
            //CFDGlobal.LogLine("FromAdmin: ");
            //CFDGlobal.LogLine(message.ToString());
            //CFDGlobal.LogLine(GetMessageString(message));
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
            //CFDGlobal.LogLine("OnLogon: " + sessionID);

            Session = Session.LookupSession(sessionID);
            DD = Session.ApplicationDataDictionary;

            var userRequest = new UserRequest();
            userRequest.UserRequestID = new UserRequestID("login:" + "ayondodemo01");
            userRequest.UserRequestType = new UserRequestType(UserRequestType.LOGONUSER);
            userRequest.Username = new Username("ayondodemo01");
            userRequest.Password = new Password("demo2016!");
            Session.Send(userRequest);
        }

        #endregion

        #region OnMessage methods

        public void OnMessage(UserResponse response, SessionID sessionID)
        {
            CFDGlobal.LogLine("OnMessage:UserResponse ");
            CFDGlobal.LogLine(GetMessageString(response));
        }

        public void OnMessage(CollateralReport report, SessionID session)
        {
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
                        continue;
                    else if (action == '1')
                        QueryEnterOrder();
                    else if (action == '2')
                        QueryReplaceOrder();
                    else if (action == '3')
                        QueryPositionReport();
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

            //QuickFix.FIX44.OrderCancelReplaceRequest m = QueryCancelReplaceRequest44();

            //if (m != null && QueryConfirm("Send replace"))
            //    SendMessage(m);
        }

        private bool QueryConfirm(string query)
        {
            Console.WriteLine();
            Console.WriteLine(query + "?: ");
            string line = Console.ReadLine().Trim();
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
            // Commands 'g' and 'x' are intentionally hidden.
            Console.Write("\n"
                          + "1) Enter Order\n"
                          + "2) Replace Order\n"
                          + "Q) Quit\n"
                          + "Action: "
                );

            HashSet<string> validActions = new HashSet<string>("1,2,3,4,q,Q,g,x".Split(','));

            string cmd = Console.ReadLine().Trim();
            if (cmd.Length != 1 || validActions.Contains(cmd) == false)
                return (char) 0;

            return cmd.ToCharArray()[0];
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
            newOrderSingle.ClOrdID = QueryClOrdID();
            newOrderSingle.TransactTime = new TransactTime(DateTime.UtcNow);

            var secId = QuerySymbol();
            newOrderSingle.Symbol = new Symbol(secId);
            newOrderSingle.SecurityID = new SecurityID(secId);

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

            if (!string.IsNullOrEmpty(queryStopPx) || !string.IsNullOrEmpty(queryTakePx))
            {
                newOrderSingle.Price = QueryPrice();
            }

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

        //private QuickFix.FIX44.OrderCancelReplaceRequest QueryCancelReplaceRequest44()
        //{
        //    QuickFix.FIX44.OrderCancelReplaceRequest ocrr = new QuickFix.FIX44.OrderCancelReplaceRequest(
        //        QueryOrigClOrdID(),
        //        QueryClOrdID(),
        //        QuerySymbol(),
        //        QuerySide(),
        //        new TransactTime(DateTime.Now),
        //        QueryOrdType());

        //    ocrr.Set(new HandlInst('1'));
        //    if (QueryConfirm("New price"))
        //        ocrr.Set(QueryPrice());
        //    if (QueryConfirm("New quantity"))
        //        ocrr.Set(QueryOrderQty());

        //    return ocrr;
        //}

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

            foreach (KeyValuePair<int, IField> pair in message)
            {
                var field = DD.FieldsByTag[pair.Key];
                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                sb.AppendLine(field.Name + "=" + value);
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

        private Price QueryPrice()
        {
            Console.WriteLine();
            Console.Write("Price? ");
            return new Price(Convert.ToDecimal(Console.ReadLine().Trim()));
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