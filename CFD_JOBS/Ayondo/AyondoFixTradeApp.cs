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

        public void ToAdmin(Message message, SessionID sessionID)
        {
            CFDGlobal.LogLine("ToAdmin: ");
            CFDGlobal.LogLine(message.ToString());

            if (message.Header.GetString(Tags.MsgType) == MsgType.LOGON)
            {
                CFDGlobal.LogLine(" sending username and password...");
                message.SetField(new Username("thcntrade"));
                message.SetField(new Password("d093gos3j"));
            }
        }

        public void FromAdmin(Message message, SessionID sessionID)
        {
            CFDGlobal.LogLine("FromAdmin: ");
            CFDGlobal.LogLine(message.ToString());
            CFDGlobal.LogLine(GetMessageString(message));
        }

        public void ToApp(Message message, SessionID sessionId)
        {
            CFDGlobal.LogLine("ToApp: ");
            CFDGlobal.LogLine(message.ToString());
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            CFDGlobal.LogLine("FromApp: ");
            CFDGlobal.LogLine(message.ToString());
            //CFDGlobal.LogLine(GetMessageString(message));
            Crack(message, sessionID);
        }

        public void OnMessage(QuickFix.FIX44.UserResponse response, SessionID sessionID)
        {
            CFDGlobal.LogLine("OnMessage:UserResponse ");
            CFDGlobal.LogLine(GetMessageString(response));


            //var orderMassStatusRequest = new OrderMassStatusRequest();
            //orderMassStatusRequest.MassStatusReqID = new MassStatusReqID("ordermass123");
            //orderMassStatusRequest.MassStatusReqType = new MassStatusReqType(7);
            //orderMassStatusRequest.Account = new Account(response.GetString(Tags.Account));
            //Session.Send(orderMassStatusRequest);


            //var requestForPositions = new RequestForPositions();
            //requestForPositions.PosReqID = new PosReqID("posreq123");
            //requestForPositions.PosReqType = new PosReqType(PosReqType.POSITIONS);
            //requestForPositions.ClearingBusinessDate = new ClearingBusinessDate("0-0-0");
            //requestForPositions.TransactTime = new TransactTime(DateTime.Now);
            //requestForPositions.Account = new Account(response.GetString(Tags.Account));
            //requestForPositions.AccountType = new AccountType(AccountType.ACCOUNT_IS_CARRIED_ON_CUSTOMER_SIDE_OF_BOOKS);
            //Session.Send(requestForPositions);


            var newOrderSingle = new QuickFix.FIX44.NewOrderSingle();
            newOrderSingle.ClOrdID = new ClOrdID("newOrderSingle123");
            newOrderSingle.TransactTime = new TransactTime(DateTime.UtcNow);
            newOrderSingle.Symbol = new Symbol("12956");
            newOrderSingle.SecurityID = new SecurityID("12956");
            newOrderSingle.SecurityIDSource = new SecurityIDSource("G");
            newOrderSingle.Side = new Side(Side.BUY);
            newOrderSingle.Price = new Price(123.123m);
            newOrderSingle.OrderQty = new OrderQty((decimal) 1);
            newOrderSingle.OrdType = new OrdType('1');

            //newOrderSingle.TargetStrategy = new TargetStrategy(5001);
            //newOrderSingle.TargetStrategyParameters = new TargetStrategyParameters("138604815797");
            newOrderSingle.TargetStrategy = new TargetStrategy(5000);

            newOrderSingle.Account = new Account(response.GetString(Tags.Account));
            Session.Send(newOrderSingle);
        }

        public void OnMessage(QuickFix.FIX44.CollateralReport report, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:CollateralReport ");
            CFDGlobal.LogLine(GetMessageString(report));
        }

        public void OnMessage(QuickFix.FIX44.ExecutionReport report, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:ExecutionReport ");
            CFDGlobal.LogLine(GetMessageString(report));
        }

        public void OnMessage(QuickFix.FIX44.RequestForPositionsAck requestAck, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:RequestForPositionsAck ");
            CFDGlobal.LogLine(GetMessageString(requestAck));
        }

        public void OnMessage(QuickFix.FIX44.PositionReport report, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:PositionReport ");
            CFDGlobal.LogLine(GetMessageString(report));
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

            var userRequest = new UserRequest();
            userRequest.UserRequestID = new UserRequestID("login123");
            userRequest.UserRequestType = new UserRequestType(UserRequestType.LOGONUSER);
            userRequest.Username = new Username("ayondodemo01");
            userRequest.Password = new Password("demo2016!");
            Session.Send(userRequest);
        }

        private string GetMessageString(Message message)
        {
            if (DD == null)
                return message.ToString();

            var sb = new StringBuilder();
            sb.AppendLine("--------------------fix message-------------------");
            foreach (KeyValuePair<int, IField> pair in message.Header)
            {
                var field = DD.FieldsByTag[pair.Key];
                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                sb.AppendLine(field.Name + "=" + value);
            }
            sb.AppendLine("");

            foreach (KeyValuePair<int, IField> pair in message)
            {
                var field = DD.FieldsByTag[pair.Key];
                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                sb.AppendLine(field.Name + "=" + value);
            }
            sb.AppendLine("");

            foreach (KeyValuePair<int, IField> pair in message.Trailer)
            {
                var field = DD.FieldsByTag[pair.Key];
                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                sb.AppendLine(field.Name + "=" + value);
            }
            return sb.ToString();
        }
    }
}