using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON;
using QuickFix;
using QuickFix.DataDictionary;
using QuickFix.Fields;
using QuickFix.FIX44;
using Message = QuickFix.Message;

namespace CFD_JOBS.Ayondo
{
    class AyondoFixPositionApp : MessageCracker, IApplication
    {
        public Session Session { get; set; }
        private DataDictionary DataDic;

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
            //CFDGlobal.LogLine(message.ToString());
            CFDGlobal.LogLine(GetMessageString(message));
            Crack(message, sessionID);
        }

        public void OnMessage(QuickFix.FIX44.UserResponse response, SessionID sessionID)
        {
            CFDGlobal.LogLine("OnMessage:UserResponse ");
            CFDGlobal.LogLine(GetMessageString(response));

            var requestForPositions = new QuickFix.FIX44.RequestForPositions();
            requestForPositions.PosReqID = new PosReqID("positionrq123");
            requestForPositions.PosReqType = new PosReqType(PosReqType.POSITIONS);
            requestForPositions.ClearingBusinessDate = new ClearingBusinessDate("0-0-0");
            requestForPositions.TransactTime = new TransactTime(DateTime.Now);
            requestForPositions.Account = new Account(response.GetString(Tags.Account));
            requestForPositions.AccountType = new AccountType(AccountType.ACCOUNT_IS_CARRIED_ON_CUSTOMER_SIDE_OF_BOOKS);

            Session.Send(requestForPositions);
        }

        public void OnMessage(QuickFix.FIX44.RequestForPositionsAck report, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:RequestForPositionsAck ");
            CFDGlobal.LogLine(GetMessageString(report));

            //var rqForPositionsAck = new QuickFix.FIX44.RequestForPositionsAck();
            //rqForPositionsAck.PosReqID = new PosReqID("positionrq");
            //Session.Send(rqForPositionsAck);
        }

        public void OnMessage(QuickFix.FIX44.PositionReport report, SessionID session)
        {
            CFDGlobal.LogLine("OnMessage:PositionReport ");
            CFDGlobal.LogLine(GetMessageString(report));
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
            DataDic = Session.ApplicationDataDictionary;
            var userRequest = new UserRequest();
            userRequest.UserRequestID = new UserRequestID("login123");
            userRequest.UserRequestType = new UserRequestType(UserRequestType.LOGONUSER);
            userRequest.Username = new Username("ayondodemo01");
            userRequest.Password = new Password("demo2016!");
            Session.Send(userRequest);
        }

        private string GetMessageString(Message message)
        {
            if (DataDic == null)
                return message.ToString();

            var sb = new StringBuilder();
            sb.AppendLine("--------------------fix message-------------------");
            foreach (KeyValuePair<int, IField> pair in message.Header)
            {
                var field = DataDic.FieldsByTag[pair.Key];
                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                sb.AppendLine(field.Name + "=" + value);
            }
            sb.AppendLine("");

            foreach (KeyValuePair<int, IField> pair in message)
            {
                var field = DataDic.FieldsByTag[pair.Key];
                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                sb.AppendLine(field.Name + "=" + value);
            }
            sb.AppendLine("");

            foreach (KeyValuePair<int, IField> pair in message.Trailer)
            {
                var field = DataDic.FieldsByTag[pair.Key];
                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
                sb.AppendLine(field.Name + "=" + value);
            }
            return sb.ToString();
        }
    }
}
