using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using QuickFix;
using QuickFix.DataDictionary;
using QuickFix.Fields;

namespace CFD_JOBS.Ayondo
{
    public class AyondoFixFeedApp : MessageCracker, IApplication
    {
        //public bool IsReceivingMessages { get; set; }

        public Session Session { get; set; }
        private DataDictionary DD;

        private DateTime BeginTimeForMsgCount = DateTime.MinValue;
        private int MsgCount = 0;
        private int MsgTotalCount = 0;
        private IList<Quote> quotes = new List<Quote>();

        //private IList<int> _activeProdIds = new List<int>(); 

        public ConcurrentQueue<ProdDef> ProdDefs = new ConcurrentQueue<ProdDef>();

        //private IRedisTypedClient<Quote> redisQuoteClient;
        //private IRedisTypedClient<ProdDef> redisProdDefClient;

//        public IRedisTypedClient<> 

        //public AyondoFixFeedApp()
        //{
        //var basicRedisClientManager = CFDGlobal.GetNewBasicRedisClientManager();

        //redisQuoteClient = CFDGlobal.BasicRedisClientManager.GetClient().As<Quote>();
        //redisProdDefClient = basicRedisClientManager.GetClient().As<ProdDef>();
        //}

        public void ToAdmin(Message message, SessionID sessionID)
        {
            CFDGlobal.LogLine("ToAdmin: ");

            if (message.Header.GetString(Tags.MsgType) == MsgType.LOGON)
            {
                CFDGlobal.LogLine(" sending username and password...");

                //demo
                message.SetField(new Username("thcnprices"));
                message.SetField(new Password("sl6map3go"));

                ////demo UAT
                //message.SetField(new Username("thcnuatprices"));
                //message.SetField(new Password("slktrp2"));
            }

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

            ////-------clear inactive products-------------
            //if (_activeProdIds.Count==121)
            //{
            //    var redisProdDefClient = CFDGlobal.BasicRedisClientManager.GetClient().As<ProdDef>();
            //    var allIds = redisProdDefClient.GetAll().Select(o => o.Id).ToList();
            //    var removeIds = allIds.Where(o => !_activeProdIds.Contains(o)).ToList();
            //    redisProdDefClient.DeleteByIds(removeIds);

            //     allIds = redisQuoteClient.GetAll().Select(o => o.Id).ToList();
            //     removeIds = allIds.Where(o => !_activeProdIds.Contains(o)).ToList();
            //     redisQuoteClient.DeleteByIds(removeIds);

            //    var redis = CFDGlobal.BasicRedisClientManager.GetClient();
            //    var keys = redis.SearchKeys("tick:*");
            //    foreach (var key in keys)
            //    {
            //        var secId = Convert.ToInt32(key.Replace("tick:", ""));
            //        if (!_activeProdIds.Contains(secId))
            //            redis.RemoveEntry(key);
            //    }
            //}//---------------------------------------------

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
                    QuoteType = (enmQuoteType) message.GetInt(Tags.QuoteType),
                    Name = message.GetString(Tags.Symbol),
                    Symbol = message.GetString(DD.FieldsByName["MDS_BBC"].Tag),
                    AssetClass = message.GetString(DD.FieldsByName["MDS_ASSETCLASS"].Tag),

                    //some security MDS2 dont have Bid/Offer...
                    Bid = message.Any(o => o.Key == Tags.BidPx) ? message.GetDecimal(Tags.BidPx) : (decimal?) null,
                    Offer = message.Any(o => o.Key == Tags.OfferPx) ? message.GetDecimal(Tags.OfferPx) : (decimal?) null,
                    //some security MDS2 dont have MDS_CLOSEBID/MDS_CLOSEASK...
                    CloseBid = message.Any(o => o.Key == DD.FieldsByName["MDS_CLOSEBID"].Tag) ? message.GetDecimal(DD.FieldsByName["MDS_CLOSEBID"].Tag) : (decimal?) null,
                    CloseAsk = message.Any(o => o.Key == DD.FieldsByName["MDS_CLOSEASK"].Tag) ? message.GetDecimal(DD.FieldsByName["MDS_CLOSEASK"].Tag) : (decimal?) null,

                    //
                    Shortable = Convert.ToBoolean(message.GetString(DD.FieldsByName["MDS_SHORTABLE"].Tag)),
                    MinSizeShort = message.GetDecimal(DD.FieldsByName["MDS_MinSizeShort"].Tag),
                    MaxSizeShort = message.GetDecimal(DD.FieldsByName["MDS_MaxSizeShort"].Tag),
                    MinSizeLong = message.GetDecimal(DD.FieldsByName["MDS_MinSizeLong"].Tag),
                    MaxSizeLong = message.GetDecimal(DD.FieldsByName["MDS_MaxSizeLong"].Tag),
                    MaxLeverage = message.GetDecimal(DD.FieldsByName["MDS_EFFLEVERAGE"].Tag),
                    PLUnits = message.GetDecimal(DD.FieldsByName["MDS_PLUNITS"].Tag),
                    LotSize = message.GetDecimal(DD.FieldsByName["MDS_LOTSIZE"].Tag),
                    Ccy2 = message.GetString(DD.FieldsByName["MDS_CCY2"].Tag),

                    Prec = message.GetInt(DD.FieldsByName["MDS_PREC"].Tag),
                    SMD = message.GetDecimal(DD.FieldsByName["MDS_SMD"].Tag),
                    GSMD = message.GetDecimal(DD.FieldsByName["MDS_GSMD"].Tag),
                };

                //CFDGlobal.LogLine("MDS2 Received: Id: " + prodDef.Id + " QuoteType: " + prodDef.QuoteType);
                ProdDefs.Enqueue(prodDef);
                //redisProdDefClient.Store(prodDef);

                //if(!_activeProdIds.Contains(prodDef.Id))
                //    _activeProdIds.Add(prodDef.Id);
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
            //CFDGlobal.LogLine(GetMessageString(quote));

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
                Time = quote.Header.GetDateTime(Tags.SendingTime)
            });

            //do save Every Second
            var now = DateTime.Now;
            if (now - BeginTimeForMsgCount > TimeSpan.FromSeconds(1))
            {
                CFDGlobal.LogLine("Count: " + MsgCount + "/" + MsgTotalCount
                                  + " Time: " + quotes.Min(o => o.Time).ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND)
                                  + " ~ " + quotes.Max(o => o.Time).ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND)
                                  + ". Saving to redis...");

                try
                {
                    using (var redisClient = CFDGlobal.BasicRedisClientManager.GetClient())
                    {
                        var redisQuoteClient = redisClient.As<Quote>();
                        redisQuoteClient.StoreAll(quotes);
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

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