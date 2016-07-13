using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Utils;
using QuickFix;
using QuickFix.Transport;

namespace CFD_JOBS.Ayondo
{
    internal class AyondoFixFeedWorker
    {
        private static Timer _timerQuotes;
        private static Timer _timerProdDefs;
        private static AyondoFixFeedApp myApp;

        private static readonly TimeSpan _intervalProdDefs = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan _intervalQuotes = TimeSpan.FromMilliseconds(500);

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

            while (true)
            {
                //System.Console.WriteLine("o hai");
                System.Threading.Thread.Sleep(1000);
            }

            //initiator.Stop();
        }

        private static void SaveQuotes(object state)
        {
            while (true)
            {
                try
                {
                    //new prod list from Ayondo MDS2
                    IList<Quote> quotes = new List<Quote>();
                    while (!myApp.Quotes.IsEmpty)
                    {
                        Quote obj;
                        var tryDequeue = myApp.Quotes.TryDequeue(out obj);
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
                    while (!myApp.ProdDefs.IsEmpty)
                    {
                        ProdDef obj;
                        var tryDequeue = myApp.ProdDefs.TryDequeue(out obj);
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