using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Transactions;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using EntityFramework.BulkInsert.Extensions;
using QuickFix;
using QuickFix.Transport;

namespace CFD_JOBS.Ayondo
{
    internal class AyondoFixFeedWorker
    {
        private static Timer _timerQuotes;
        private static Timer _timerProdDefs;
        private static Timer _timerTicks;

        private static AyondoFixFeedApp myApp;

        private static readonly TimeSpan _intervalProdDefs = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan _intervalQuotes = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan _intervalTicks = TimeSpan.FromMilliseconds(1000);

        public static void Run()
        {
            SessionSettings settings = new SessionSettings(CFDGlobal.GetConfigurationSetting("ayondoFixFeedCfgFilePath"));
            myApp = new AyondoFixFeedApp();
            IMessageStoreFactory storeFactory = new MemoryStoreFactory(); //new FileStoreFactory(settings);
            //ILogFactory logFactory = new FileLogFactory(settings);
            SocketInitiator initiator = new SocketInitiator(myApp, storeFactory, settings,
                null //logFactory
                );

            //var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();
            //var redisProdDefClient = redisClient.As<ProdDef>();
            //var redisTickClient = redisClient.As<Tick>();

            initiator.Start();

            _timerProdDefs = new Timer(SaveProdDefs, null, _intervalProdDefs, TimeSpan.FromMilliseconds(-1));
            _timerQuotes = new Timer(SaveQuotes, null, _intervalQuotes, TimeSpan.FromMilliseconds(-1));
            _timerTicks = new Timer(SaveTicks, null, _intervalTicks, TimeSpan.FromMilliseconds(-1));

            while (true)
            {
                //System.Console.WriteLine("o hai");
                System.Threading.Thread.Sleep(1000);
            }

            //initiator.Stop();
        }

        private static void SaveTicks(object state)
        {
            while (true)
            {
                try
                {
                    IList<Quote> quotes = new List<Quote>();
                    while (!myApp.QueueQuotes2.IsEmpty)
                    {
                        Quote obj;
                        var tryDequeue = myApp.QueueQuotes2.TryDequeue(out obj);
                        quotes.Add(obj);
                    }

                    if (quotes.Count > 0)
                    {
                        var distinctQuotes = quotes.GroupBy(o => o.Id).Select(o => o.OrderByDescending(p => p.Time).First()).ToList();

                        var dtBeginSave = DateTime.Now;
                        var count = 0;
                        var entitiesToSaveToDB=new List<QuoteHistory>();

                        using (var redisClient = CFDGlobal.PooledRedisClientsManager.GetClient())
                        {
                            var redisTickClient = redisClient.As<Tick>();

                            foreach (var quote in distinctQuotes)
                            {
                                if (!myApp.ProdDefs.ContainsKey(quote.Id)) //no product definition
                                {
                                    CFDGlobal.LogLine("no prodDef. tick ignored " + quote.Id);
                                    continue;
                                }

                                var prodDef = myApp.ProdDefs[quote.Id];
                                if (prodDef.QuoteType != enmQuoteType.Open && prodDef.QuoteType != enmQuoteType.PhoneOnly) //not open not phoneOnly
                                {
                                    CFDGlobal.LogLine("prod not opening. tick ignored. " + prodDef.Id + " " + prodDef.Name);
                                    continue;
                                }

                                var list = redisTickClient.Lists[Ticks.GetTickListNamePrefix(TickSize.Raw) + quote.Id];

                                var tick = new Tick {P = Quotes.GetLastPrice(quote), Time = quote.Time};

                                list.Add(tick);
                                count++;

                                //add to list to save to db later
                                entitiesToSaveToDB.Add(new QuoteHistory
                                {
                                    SecurityId = quote.Id,
                                    Time = quote.Time,
                                    Bid = quote.Bid,
                                    Ask = quote.Offer,
                                });

                                //clear history/prevent data increasing for good
                                var clearWhenSize = Ticks.GetClearWhenSize(TickSize.Raw);
                                var clearToSize = Ticks.GetClearToSize(TickSize.Raw);
                                if (list.Count > clearWhenSize) //data count at most possible size (in x days )
                                {
                                    CFDGlobal.LogLine("Raw Ticks " + quote.Id + " Clearing data from " + list.Count + " to " + clearToSize);
                                    var ticks = list.GetAll();
                                    var newTicks = ticks.Skip(ticks.Count - clearToSize);
                                    list.RemoveAll();
                                    list.AddRange(newTicks);
                                }
                            }
                        }

                        CFDGlobal.LogLine("\t\tSaved " + count + "/" + distinctQuotes.Count + "/" + quotes.Count + " ticks to Redis " + (DateTime.Now - dtBeginSave).TotalMilliseconds);

                        if (entitiesToSaveToDB.Count > 0)
                        {
                            dtBeginSave = DateTime.Now;
                            using (var db = CFDEntities.Create())
                            {
                                //using (var transactionScope = new TransactionScope())
                                //{
                                    db.BulkInsert(distinctQuotes.Select(o => new QuoteHistory
                                    {
                                        SecurityId = o.Id,
                                        Time = o.Time,
                                        Bid = o.Bid,
                                        Ask = o.Offer,
                                    }));
                                    db.SaveChanges();
                                //    transactionScope.Complete();
                                //}
                            }

                            CFDGlobal.LogLine("\t\tSaved " + entitiesToSaveToDB.Count + " ticks to DB " + (DateTime.Now - dtBeginSave).TotalMilliseconds);
                        }
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(_intervalTicks);
            }
        }

        private static void SaveQuotes(object state)
        {
            while (true)
            {
                try
                {
                    //new prod list from Ayondo MDS2
                    IList<Quote> quotes = new List<Quote>();
                    while (!myApp.QueueQuotes.IsEmpty)
                    {
                        Quote obj;
                        var tryDequeue = myApp.QueueQuotes.TryDequeue(out obj);
                        quotes.Add(obj);
                    }

                    if (quotes.Count > 0)
                    {
                        var distinctQuotes = quotes.GroupBy(o => o.Id).Select(o => o.OrderByDescending(p => p.Time).First()).ToList();

                        var dtBeginSave = DateTime.Now;

                        using (var redisClient = CFDGlobal.PooledRedisClientsManager.GetClient())
                        {
                            var redisQuoteClient = redisClient.As<Quote>();
                            redisQuoteClient.StoreAll(distinctQuotes);
                        }

                        CFDGlobal.LogLine("Count: " + distinctQuotes.Count + "/" + quotes.Count + " (distinct/raw) "
                                          + " Time: " + quotes.Min(o => o.Time).ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND)
                                          + " ~ " + quotes.Max(o => o.Time).ToString(CFDGlobal.DATETIME_MASK_MILLI_SECOND)
                                          + ". Saved to redis " + (DateTime.Now - dtBeginSave).TotalMilliseconds);
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(_intervalQuotes);
            }
        }

        private static void SaveProdDefs(object state)
        {
            while (true)
            {
                try
                {
                    //new prod list from Ayondo MDS2
                    IList<ProdDef> listNew = new List<ProdDef>();
                    while (!myApp.QueueProdDefs.IsEmpty)
                    {
                        ProdDef obj;
                        var tryDequeue = myApp.QueueProdDefs.TryDequeue(out obj);
                        listNew.Add(obj);
                    }

                    if (listNew.Count > 0)
                    {
                        CFDGlobal.LogLine("Saving " + listNew.Count + " ProdDefs to Redis...");

                        using (var redisClient = CFDGlobal.PooledRedisClientsManager.GetClient())
                        {
                            var redisProdDefClient = redisClient.As<ProdDef>();

                            //current redis list
                            var listOld = redisProdDefClient.GetAll();

                            IList<ProdDef> listToSave = new List<ProdDef>();
                            //var listToSaveAsQuote = new List<ProdDef>();

                            foreach (var newProdDef in listNew)
                            {
                                var old = listOld.FirstOrDefault(o => o.Id == newProdDef.Id);

                                if (old != null) //updating prod def in redis
                                {
                                    //update open/close time/price depending on state change
                                    if (old.QuoteType != enmQuoteType.Closed && newProdDef.QuoteType == enmQuoteType.Closed) //xxx -> close
                                    {
                                        CFDGlobal.LogLine("PROD CLOSED " + newProdDef.Id + " time: " + newProdDef.Time + " offer: " + newProdDef.Offer + " bid: " + newProdDef.Bid);

                                        //close time
                                        old.LastClose = newProdDef.Time;

                                        ////prod def will be treated as a new QUOTE when stock open/close
                                        //listToSaveAsQuote.Add(newProdDef);
                                    }
                                    else if (old.QuoteType != enmQuoteType.Open && old.QuoteType != enmQuoteType.PhoneOnly &&
                                             (newProdDef.QuoteType == enmQuoteType.Open || newProdDef.QuoteType == enmQuoteType.PhoneOnly)) //xxx -> open/phone
                                    {
                                        CFDGlobal.LogLine("PROD OPENED " + newProdDef.Id + " time: " + newProdDef.Time + " offer: " + newProdDef.Offer + " bid: " +
                                                          newProdDef.Bid);

                                        //open time
                                        old.LastOpen = newProdDef.Time;

                                        //open prices
                                        old.OpenAsk = newProdDef.Offer;
                                        old.OpenBid = newProdDef.Bid;

                                        //preclose
                                        old.PreClose = Quotes.GetClosePrice(newProdDef);

                                        ////prod def will be treated as a new QUOTE when stock open/close
                                        //listToSaveAsQuote.Add(newProdDef);
                                    }

                                    //update fields
                                    old.Time = newProdDef.Time;
                                    old.QuoteType = newProdDef.QuoteType;
                                    old.Name = newProdDef.Name;
                                    old.Symbol = newProdDef.Symbol;
                                    old.AssetClass = newProdDef.AssetClass;
                                    old.Bid = newProdDef.Bid;
                                    old.Offer = newProdDef.Offer;
                                    old.CloseBid = newProdDef.CloseBid;
                                    old.CloseAsk = newProdDef.CloseAsk;
                                    old.Shortable = newProdDef.Shortable;
                                    old.MinSizeShort = newProdDef.MinSizeShort;
                                    old.MaxSizeShort = newProdDef.MaxSizeShort;
                                    old.MinSizeLong = newProdDef.MinSizeLong;
                                    old.MaxSizeLong = newProdDef.MaxSizeLong;
                                    old.MaxLeverage = newProdDef.MaxLeverage;
                                    old.PLUnits = newProdDef.PLUnits;
                                    old.LotSize = newProdDef.LotSize;
                                    old.Ccy2 = newProdDef.Ccy2;
                                    old.Prec = newProdDef.Prec;
                                    old.SMD = newProdDef.SMD;
                                    old.GSMD = newProdDef.GSMD;
                                    old.GSMS = newProdDef.GSMS;

                                    listToSave.Add(old);
                                }
                                else //appending new prod def into redis
                                {
                                    listToSave.Add(newProdDef);
                                }
                            }

                            redisProdDefClient.StoreAll(listToSave);
                        }
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(_intervalProdDefs);
            }
        }
    }
}