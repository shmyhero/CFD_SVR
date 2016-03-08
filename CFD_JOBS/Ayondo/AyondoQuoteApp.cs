using System;
using CFD_COMMON;
using QuickFix;
using QuickFix.DataDictionary;
using QuickFix.Fields;

namespace CFD_JOBS.Ayondo
{
    public class AyondoQuoteApp : MessageCracker, IApplication
    {
        public Session CurrentSession { get; set; }
        public DataDictionary DD { get; set; }

        private DateTime BeginTimeForMsgCount = DateTime.MinValue;
        private int MsgCount = 0;
        private int MsgTotalCount = 0;

        public void ToAdmin(Message message, SessionID sessionID)
        {
            CFDGlobal.LogLine("ToAdmin: sending username and password...");

            message.SetField(new Username("thcnprices"));
            message.SetField(new Password("sl6map3go"));
        }

        public void FromAdmin(Message message, SessionID sessionID)
        {
            CFDGlobal.LogLine("FromAdmin: ");
        }

        public void ToApp(Message message, SessionID sessionId)
        {
            CFDGlobal.LogLine("ToApp: ");
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            //CFDGlobal.LogLine("FromApp: cracking message...");

            Crack(message, sessionID);
        }

        public void OnMessage(QuickFix.FIX44.Quote quote, SessionID sessionID)
        {
//            //detail log
//            var sb=new StringBuilder();
//            sb.AppendLine("--------------------new quote message-------------------");
//            foreach (KeyValuePair<int, IField> pair in quote.Header)
//            {
//                var field = DD.FieldsByTag[pair.Key];
//                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()]+"("+pair.Value+")" : pair.Value.ToString();
//                sb.AppendLine(field.Name + "=" + value);
//            }
//            sb.AppendLine("");
//
//            foreach (KeyValuePair<int, IField> pair in quote)
//            {
//                var field = DD.FieldsByTag[pair.Key];
//                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
//                sb.AppendLine(field.Name + "=" + value);
//            }
//            sb.AppendLine("");
//
//            foreach (KeyValuePair<int, IField> pair in quote.Trailer)
//            {
//                var field = DD.FieldsByTag[pair.Key];
//                var value = field.HasEnums() ? field.EnumDict[pair.Value.ToString()] + "(" + pair.Value + ")" : pair.Value.ToString();
//                sb.AppendLine(field.Name + "=" + value);
//            }
//
//            CFDGlobal.LogLine(sb.ToString());

            //aggregate log
            MsgCount++;
            MsgTotalCount++;

            var now = DateTime.Now;
            if (now - BeginTimeForMsgCount > TimeSpan.FromSeconds(1))
            {
                CFDGlobal.LogLine(MsgCount + " last second. " + MsgTotalCount + " total.");

                BeginTimeForMsgCount = now;
                MsgCount = 0;
            }


//            CFDGlobal.LogLine(quote.ToString());
        }

        public void OnCreate(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnCreate: ");
        }

        public void OnLogout(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnLogout: ");
        }

        public void OnLogon(SessionID sessionID)
        {
            CFDGlobal.LogLine("OnLogon: ");

            CurrentSession = Session.LookupSession(sessionID);
            DD = CurrentSession.ApplicationDataDictionary;
        }
    }
}