using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CFD_COMMON;
using CFD_COMMON.Models;
using CFD_COMMON.Models.Cached;
using QuickFix;
using QuickFix.DataDictionary;
using QuickFix.Fields;
using ServiceStack.Redis.Generic;

namespace CFD_JOBS.Ayondo
{
    public class AyondoFixFeedApp : MessageCracker, IApplication
    {
        public Session Session { get; set; }
        private DataDictionary DD;

        private DateTime BeginTimeForMsgCount = DateTime.MinValue;
        private int MsgCount = 0;
        private int MsgTotalCount = 0;
        private IList<Quote> quotes = new List<Quote>();

        public ConcurrentQueue<ProdDef> ProdDefs = new ConcurrentQueue<ProdDef>();

        private IRedisTypedClient<Quote> redisQuoteClient;
        //private IRedisTypedClient<ProdDef> redisProdDefClient;

//        public IRedisTypedClient<> 

        public AyondoFixFeedApp()
        {
            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();

            redisQuoteClient = basicRedisClientManager.GetClient().As<Quote>();
            //redisProdDefClient = basicRedisClientManager.GetClient().As<ProdDef>();
        }

        public void ToAdmin(Message message, SessionID sessionID)
        {
            CFDGlobal.LogLine("ToAdmin: sending username and password...");

            message.SetField(new Username("thcnprices"));
            message.SetField(new Password("sl6map3go"));

            //message.SetField(new Username("thcntrade"));
            //message.SetField(new Password("d093gos3j"));

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

            CFDGlobal.LogLine(message.ToString());
            //message.SetField(new MsgType("MDS1"));
            //message.SetField(new UserRequestID("test1111"));
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            //CFDGlobal.LogLine("FromApp: cracking message...");

            string msgType = message.Header.GetString(Tags.MsgType);

            if (msgType == MsgType.QUOTE)
            {
                Crack(message, sessionID);
            }
            else if (msgType == "MDS2")
            {
                //CFDGlobal.LogLine(message.ToString());
                //CFDGlobal.LogLine(GetMessageString(message));

                //var name=
                var prodDef = new ProdDef()
                {
                    Id = Convert.ToInt32(message.GetString(Tags.SecurityID)),
                    Time = message.Header.GetDateTime(Tags.SendingTime),
                    QuoteType = message.GetInt(Tags.QuoteType),
                    Name = message.GetString(Tags.Symbol),
                    Symbol = message.GetString(DD.FieldsByName["MDS_BBC"].Tag)
                };

                //CFDGlobal.LogLine("MDS2 Received: Id: " + prodDef.Id + " QuoteType: " + prodDef.QuoteType);
                ProdDefs.Enqueue(prodDef);
                //redisProdDefClient.Store(prodDef);
            }
            else
            {
                CFDGlobal.LogLine("Unknown MsgType: " + message.ToString());
            }
        }

        private string GetMessageString(Message message)
        {
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

        public void OnMessage(QuickFix.FIX44.Quote quote, SessionID sessionID)
        {
            //basic log
//            CFDGlobal.LogLine(quote.ToString());

//            //detail log
            //            CFDGlobal.LogLine(GetMessageString);

            //if (quote.SecurityID.getValue() == "20867")
            //    CFDGlobal.LogLine("20867 " + quote.BidPx.getValue() + " " + quote.OfferPx.getValue());

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

                redisQuoteClient.StoreAll(quotes);

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

            Session = Session.LookupSession(sessionID);
            DD = Session.ApplicationDataDictionary;

            //Product Definition Request
            var order = new Message();
            order.Header.SetField(new MsgType("MDS1"));
            order.SetField(new UserRequestID("ProdDef"));
            Session.Send(order);
            //}
        }
    }
}