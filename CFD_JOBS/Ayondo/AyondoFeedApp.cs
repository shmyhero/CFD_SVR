using System;
using System.Collections.Generic;
using System.Linq;
using CFD_COMMON;
using CFD_JOBS.Models;
using QuickFix;
using QuickFix.DataDictionary;
using QuickFix.Fields;
using ServiceStack.Redis;
using ServiceStack.Redis.Generic;

namespace CFD_JOBS.Ayondo
{
    public class AyondoFeedApp : MessageCracker, IApplication
    {
        private Session CurrentSession;
        private DataDictionary DD;

        private DateTime BeginTimeForMsgCount = DateTime.MinValue;
        private int MsgCount = 0;
        private int MsgTotalCount = 0;
        private IList<Quote> quotes = new List<Quote>();

        private IRedisTypedClient<Quote> redisClient;

//        public IRedisTypedClient<> 

        public AyondoFeedApp()
        {
            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();

            redisClient = basicRedisClientManager.GetClient().As<Quote>();
        }

        public void ToAdmin(Message message, SessionID sessionID)
        {
            CFDGlobal.LogLine("ToAdmin: sending username and password...");

            message.SetField(new Username("thcnprices"));
            message.SetField(new Password("sl6map3go"));
            //message.SetField(new Username("tradeheroprices"));
            //message.SetField(new Password("4gs9k2osw"));
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
            //basic log
//            CFDGlobal.LogLine(quote.ToString());

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

            //count and add to list for saving
            MsgCount++;
            MsgTotalCount++;
            quotes.Add(new Quote()
            {
                Bid = quote.BidPx.getValue(),
                Id = Convert.ToInt32(quote.SecurityID.getValue()),
                Offer = quote.OfferPx.getValue(),
                Time = quote.Header.GetDateTime(DD.FieldsByName["SendingTime"].Tag)
            });

            //do save Every Second
            var now = DateTime.Now;
            if (now - BeginTimeForMsgCount > TimeSpan.FromSeconds(1))
            {
                CFDGlobal.LogLine("Count: " + MsgCount + "/" + MsgTotalCount
                                  + " Time: " + quotes.Min(o => o.Time).ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND)
                                  + " ~ " + quotes.Max(o => o.Time).ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND)
                                  + ". Saving to redis...");

                redisClient.StoreAll(quotes);

                //reset vars
                BeginTimeForMsgCount = now;
                MsgCount = 0;
                quotes = new List<Quote>();
            }

            //if (quote.QuoteType.getValue() != QuoteType.TRADEABLE)
            //{

            //}
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