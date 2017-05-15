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
        public AyondoFixFeedApp(string username, string password)
        {
            _username = username;
            _password = password;
        }

        //public bool IsReceivingMessages { get; set; }

        public Session Session { get; set; }
        private DataDictionary DD;

        //private readonly string _username = CFDGlobal.GetConfigurationSetting("ayondoFixFeedUsername");
        //private readonly string _password = CFDGlobal.GetConfigurationSetting("ayondoFixFeedPassword");
        private readonly string _username;
        private readonly string _password;

        //private DateTime BeginTimeForMsgCount = DateTime.MinValue;
        //private int MsgCount = 0;
        //private int MsgTotalCount = 0;
        //private IList<Quote> quotes = new List<Quote>();

        //private IList<int> _activeProdIds = new List<int>(); 

        public ConcurrentQueue<ProdDef> QueueProdDefs = new ConcurrentQueue<ProdDef>();
        public ConcurrentQueue<Quote> QueueQuotes = new ConcurrentQueue<Quote>();
        public ConcurrentQueue<Quote> QueueQuotes2 = new ConcurrentQueue<Quote>();
        public ConcurrentQueue<Quote> QueueQuotes3 = new ConcurrentQueue<Quote>();

        public IDictionary<int, ProdDef> ProdDefs = new Dictionary<int, ProdDef>();

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
                message.SetField(new Username(_username));
                message.SetField(new Password(_password));
                //message.SetField(new Username("thcnprices"));
                //message.SetField(new Password("sl6map3go"));

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
                /*other new fields:
                MDS_SFACTOR: This is the slippage factor for a product.  It is a value used in margin calculations.
                */

                //CFDGlobal.LogLine(message.ToString());
                //CFDGlobal.LogLine(GetMessageString(message));

                var id = Convert.ToInt32(message.GetString(Tags.SecurityID));
                var time = message.Header.GetDateTime(Tags.SendingTime);
                var quoteType = (enmQuoteType) message.GetInt(Tags.QuoteType);
                var name = message.GetString(Tags.Symbol);
                var symbol = message.GetString(DD.FieldsByName["MDS_BBC"].Tag);
                var assetClass = message.GetString(DD.FieldsByName["MDS_ASSETCLASS"].Tag);

                //some security MDS2 dont have Bid/Offer...
                var bid = message.Any(o => o.Key == Tags.BidPx) ? message.GetDecimal(Tags.BidPx) : (decimal?) null;
                var offer = message.Any(o => o.Key == Tags.OfferPx) ? message.GetDecimal(Tags.OfferPx) : (decimal?) null;
                //some security MDS2 dont have MDS_CLOSEBID/MDS_CLOSEASK...
                var closeBid = message.Any(o => o.Key == DD.FieldsByName["MDS_CLOSEBID"].Tag) ? message.GetDecimal(DD.FieldsByName["MDS_CLOSEBID"].Tag) : (decimal?) null;
                var closeAsk = message.Any(o => o.Key == DD.FieldsByName["MDS_CLOSEASK"].Tag) ? message.GetDecimal(DD.FieldsByName["MDS_CLOSEASK"].Tag) : (decimal?) null;

                //
                var shortable = Convert.ToBoolean(message.GetString(DD.FieldsByName["MDS_SHORTABLE"].Tag));
                var minSizeShort = message.GetDecimal(DD.FieldsByName["MDS_MinSizeShort"].Tag);
                var maxSizeShort = message.GetDecimal(DD.FieldsByName["MDS_MaxSizeShort"].Tag);
                var minSizeLong = message.GetDecimal(DD.FieldsByName["MDS_MinSizeLong"].Tag);
                var maxSizeLong = message.GetDecimal(DD.FieldsByName["MDS_MaxSizeLong"].Tag);
                var maxLeverage = message.GetDecimal(DD.FieldsByName["MDS_EFFLEVERAGE"].Tag);
                var plUnits = message.GetDecimal(DD.FieldsByName["MDS_PLUNITS"].Tag);
                var lotSize = message.GetDecimal(DD.FieldsByName["MDS_LOTSIZE"].Tag);
                var ccy2 = message.GetString(DD.FieldsByName["MDS_CCY2"].Tag);
                var prec = message.GetInt(DD.FieldsByName["MDS_PREC"].Tag);
                var smd = message.GetDecimal(DD.FieldsByName["MDS_SMD"].Tag);
                var gsmd = message.GetDecimal(DD.FieldsByName["MDS_GSMD"].Tag);
                var gsms = message.GetDecimal(DD.FieldsByName["MDS_GSMS"].Tag);

                QueueProdDefs.Enqueue(new ProdDef
                {
                    Id = id,
                    Time = time,
                    QuoteType = quoteType,
                    Name = name,
                    Symbol = symbol,
                    AssetClass = assetClass,
                    Bid = bid,
                    Offer = offer,
                    CloseBid = closeBid,
                    CloseAsk = closeAsk,
                    Shortable = shortable,
                    MinSizeShort = minSizeShort,
                    MaxSizeShort = maxSizeShort,
                    MinSizeLong = minSizeLong,
                    MaxSizeLong = maxSizeLong,
                    MaxLeverage = maxLeverage,
                    PLUnits = plUnits,
                    LotSize = lotSize,
                    Ccy2 = ccy2,
                    Prec = prec,
                    SMD = smd,
                    GSMD = gsmd,
                    GSMS = gsms,
                });

                ProdDefs[id] = new ProdDef
                {
                    Id = id,
                    Time = time,
                    QuoteType = quoteType,
                    Name = name,
                    Symbol = symbol,
                    AssetClass = assetClass,
                    Bid = bid,
                    Offer = offer,
                    CloseBid = closeBid,
                    CloseAsk = closeAsk,
                    Shortable = shortable,
                    MinSizeShort = minSizeShort,
                    MaxSizeShort = maxSizeShort,
                    MinSizeLong = minSizeLong,
                    MaxSizeLong = maxSizeLong,
                    MaxLeverage = maxLeverage,
                    PLUnits = plUnits,
                    LotSize = lotSize,
                    Ccy2 = ccy2,
                    Prec = prec,
                    SMD = smd,
                    GSMD = gsmd,
                    GSMS = gsms,
                };
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

            ////count and add to list for saving
            //MsgCount++;
            //MsgTotalCount++;
            //quotes.Add(new Quote()
            //{
            //    Bid = quote.BidPx.getValue(),
            //    Id = Convert.ToInt32(quote.SecurityID.getValue()),
            //    Offer = quote.OfferPx.getValue(),
            //    Time = quote.Header.GetDateTime(Tags.SendingTime)
            //});

            ////do save Every Second
            //var now = DateTime.Now;
            //if (now - BeginTimeForMsgCount > TimeSpan.FromMilliseconds(500))
            //{
            //    var dtBeginSave = DateTime.Now;
            //    try
            //    {
            //        using (var redisClient = CFDGlobal.PooledRedisClientsManager.GetClient())
            //        {
            //            var redisQuoteClient = redisClient.As<Quote>();
            //            redisQuoteClient.StoreAll(quotes);
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        CFDGlobal.LogException(e);
            //    }

            //    CFDGlobal.LogLine("Count: " + MsgCount + "(" + quotes.Select(o => o.Id).Distinct().Count() + ")" + "/" + MsgTotalCount
            //                      + " Time: " + quotes.Min(o => o.Time).ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND)
            //                      + " ~ " + quotes.Max(o => o.Time).ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND)
            //                      + ". Save to redis " + (DateTime.Now - dtBeginSave).TotalMilliseconds);

            //    //reset vars
            //    BeginTimeForMsgCount = now;
            //    MsgCount = 0;
            //    quotes = new List<Quote>();
            //}

            var bid = quote.BidPx.getValue();
            var secId = Convert.ToInt32(quote.SecurityID.getValue());
            var offer = quote.OfferPx.getValue();
            var time = quote.Header.GetDateTime(Tags.SendingTime);

            QueueQuotes.Enqueue(new Quote
            {
                Bid = bid,
                Id = secId,
                Offer = offer,
                Time = time
            });

            QueueQuotes2.Enqueue(new Quote
            {
                Bid = bid,
                Id = secId,
                Offer = offer,
                Time = time
            });

            QueueQuotes3.Enqueue(new Quote
            {
                Bid = bid,
                Id = secId,
                Offer = offer,
                Time = time
            });
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

            SendMDS1Request();
        }

        public void SendMDS1Request()
        {
            if (Session != null)
            {
                try
                {
                    //Product Definition Request
                    var order = new Message();
                    order.Header.SetField(new MsgType("MDS1"));
                    order.SetField(new UserRequestID("ProdDef"));
                    Session.Send(order);
                    //}
                }
                catch (Exception e)
                {
                    CFDGlobal.LogLine("send mds1 request failed");
                    CFDGlobal.LogException(e);
                }
            }
            else
            {
                //// This probably won't ever happen.
                //Console.WriteLine("Can't send message: session not created.");

                throw new Exception("fix session is null.");
            }
        }
    }
}