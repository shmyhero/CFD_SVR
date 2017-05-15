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
using ServiceStack.Redis.Generic;

namespace CFD_JOBS.Ayondo
{
    internal class AyondoFixFeedWorker
    {
        private static Timer _timerQuotes;
        private static Timer _timerProdDefs;
        private static Timer _timerProdDefRequest;
        private static Timer _timerTicks;
        private static Timer _timerKLine5m;

        private static AyondoFixFeedApp myApp;

        private static readonly TimeSpan _intervalProdDefs = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan _intervalProdDefRequest = TimeSpan.FromMinutes(60);
        private static readonly TimeSpan _intervalQuotes = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan _intervalTicks = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan _intervalKLine = TimeSpan.FromSeconds(10);

        private static bool _isLive;

        public static void Run(bool isLive = false)
        {
            _isLive = isLive;

            SessionSettings settings = new SessionSettings(CFDGlobal.GetConfigurationSetting(
                _isLive ? "ayondoFixFeedCfgFilePath_Live" : "ayondoFixFeedCfgFilePath"));
            myApp =
                new AyondoFixFeedApp(
                    CFDGlobal.GetConfigurationSetting(_isLive ? "ayondoFixFeedUsername_Live" : "ayondoFixFeedUsername"),
                    CFDGlobal.GetConfigurationSetting(_isLive ? "ayondoFixFeedPassword_Live" : "ayondoFixFeedPassword"));
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
            _timerProdDefRequest = new Timer(SendProdDefRequest, null, _intervalProdDefRequest, TimeSpan.FromMilliseconds(-1));
            _timerQuotes = new Timer(SaveQuotes, null, _intervalQuotes, TimeSpan.FromMilliseconds(-1));
            _timerTicks = new Timer(SaveTicks, null, _intervalTicks, TimeSpan.FromMilliseconds(-1));
            _timerKLine5m = new Timer(SaveKLine, null, _intervalKLine, TimeSpan.FromMilliseconds(-1));

            while (true)
            {
                //System.Console.WriteLine("o hai");
                System.Threading.Thread.Sleep(1000);
            }

            //initiator.Stop();
        }

        private static void SaveKLine(object state)
        {
            while (true)
            {
                try
                {
                    IList<Quote> newQuotes = new List<Quote>();
                    while (!myApp.QueueQuotes3.IsEmpty)
                    {
                        Quote obj;
                        var tryDequeue = myApp.QueueQuotes3.TryDequeue(out obj);
                        newQuotes.Add(obj);
                    }

                    using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(_isLive).GetClient())
                    {
                        var redisProdDefClient = redisClient.As<ProdDef>();
                        var redisKLineClient = redisClient.As<KLine>();
                        var redisQuoteClient = redisClient.As<Quote>();

                        var prodDefs = redisProdDefClient.GetAll();
                        var allQuotes = redisQuoteClient.GetAll();

                        if (allQuotes.Count > 0)
                        {
                            var dtAyondoNow = allQuotes.Max(o => o.Time); //the time of the last message received from Ayondo
                            var klineAyondoNow1M = DateTimes.GetStartTime(dtAyondoNow, 1);
                            var klineAyondoNow5M = DateTimes.GetStartTime(dtAyondoNow, 5);
                            var klineAyondoNow15M = DateTimes.GetStartTime(dtAyondoNow, 15);
                            var klineAyondoNow60M = DateTimes.GetStartTime(dtAyondoNow, 60);

                            var dtNow = DateTime.UtcNow;
                            var oneMinuteAgo = dtNow.AddMinutes(-1);

                            var openOrRecentlyClosedProdDefs = prodDefs.Where(o =>
                                (o.QuoteType == enmQuoteType.Open || o.QuoteType == enmQuoteType.PhoneOnly) //is open
                                || (o.QuoteType == enmQuoteType.Closed && o.LastClose > oneMinuteAgo) //recently closed
                                )
                                .ToList();

                            foreach (var prodDef in openOrRecentlyClosedProdDefs)
                            {
                                var quotesByProd = newQuotes.Where(o => o.Id == prodDef.Id).ToList();

                                if (prodDef.QuoteType == enmQuoteType.Closed) //recently closed
                                    quotesByProd = quotesByProd.Where(o => o.Time <= prodDef.LastClose.Value).ToList();

                                UpdateKLine(quotesByProd, redisKLineClient, prodDef, klineAyondoNow5M, KLineSize.FiveMinutes);
                                UpdateKLine(quotesByProd, redisKLineClient, prodDef, klineAyondoNow5M, KLineSize.Day);
                                UpdateKLine(quotesByProd, redisKLineClient, prodDef, klineAyondoNow1M, KLineSize.OneMinute);
                                UpdateKLine(quotesByProd, redisKLineClient, prodDef, klineAyondoNow15M, KLineSize.FifteenMinutes);
                                UpdateKLine(quotesByProd, redisKLineClient, prodDef, klineAyondoNow60M, KLineSize.SixtyMinutes);
                            }

                            CFDGlobal.LogLine("\t\t\t\tkline updated");
                        }
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(_intervalKLine);
            }
        }

        private static void UpdateKLine(List<Quote> quotes, IRedisTypedClient<KLine> redisKLineClient, ProdDef prodDef, DateTime klineAyondoNow, KLineSize kLineSize)
        {
            if (kLineSize == KLineSize.Day && prodDef.LastOpen == null)
            {
                CFDGlobal.LogLine("no LastOpen time for "+prodDef.Id);
                return;
            }

            var list = redisKLineClient.Lists[KLines.GetKLineListNamePrefix(kLineSize) + prodDef.Id];

            if (quotes.Count == 0) //no quotes received, then should just fill the non-changing quotes
            {
                if (kLineSize != KLineSize.Day) //no need to fill the non-changing quotes for day kline)
                {
                    //var list = redisKLineClient.Lists[KLines.GetKLineListNamePrefix(kLineSize) + prodDef.Id];

                    if (list.Count != 0)
                    {
                        var last = list[list.Count - 1];

                        var klineTime = klineAyondoNow;

                        if (prodDef.QuoteType == enmQuoteType.Closed)
                            klineTime = KLines.GetKLineTime(prodDef.LastClose.Value, kLineSize);

                        if (klineTime > last.Time)
                        {
                            //fill the non-changing quotes
                            list.Add(new KLine()
                            {
                                Time = klineTime,
                                Open = last.Close,
                                Close = last.Close,
                                High = last.Close,
                                Low = last.Close,
                            });
                        }
                    }
                }
            }
            else
            {
                var orderedQuotes = quotes.OrderBy(o => o.Time).ToList();

                var firstQuote = orderedQuotes.First();
                var lastQuote = orderedQuotes.Last();

                var klineTime1 = KLines.GetKLineTime(firstQuote.Time, kLineSize);
                var klineTime2 = KLines.GetKLineTime(lastQuote.Time, kLineSize);

                //var list = redisKLineClient.Lists[KLines.GetKLineListNamePrefix(kLineSize) + prodDef.Id];

                if (klineTime1 != klineTime2 && kLineSize != KLineSize.Day)
                {
                    var list1 = orderedQuotes.Where(o => o.Time < klineTime2).ToList();
                    var list2 = orderedQuotes.Where(o => o.Time >= klineTime2).ToList();

                    var k1 = new KLine()
                    {
                        Time = klineTime1,
                        Open = Quotes.GetLastPrice(list1.First()),
                        Close = Quotes.GetLastPrice(list1.Last()),
                        High = list1.Max(o => Quotes.GetLastPrice(o)),
                        Low = list1.Min(o => Quotes.GetLastPrice(o)),
                    };
                    var k2 = new KLine()
                    {
                        Time = klineTime2,
                        Open = Quotes.GetLastPrice(list2.First()),
                        Close = Quotes.GetLastPrice(list2.Last()),
                        High = list2.Max(o => Quotes.GetLastPrice(o)),
                        Low = list2.Min(o => Quotes.GetLastPrice(o)),
                    };

                    if (list.Count == 0)
                    {
                        list.Add(k1);
                        list.Add(k2);
                    }
                    else
                    {
                        var last = list[list.Count - 1];

                        if (last.Time < klineTime1)
                        {
                            list.Add(k1);
                            list.Add(k2);
                        }
                        else if (last.Time == klineTime1)
                        {
                            list[list.Count - 1] = new KLine()
                            {
                                Time = last.Time,
                                Open = last.Open,
                                Close = k1.Close,
                                High = Math.Max(last.High, k1.High),
                                Low = Math.Min(last.Low, k1.Low),
                            };

                            list.Add(k2);
                        }
                        else
                        {
                            //should not be here
                        }
                    }
                }
                else
                {
                    //for day kline, quote's kline time should be the date of the LastOpen time
                    if (kLineSize == KLineSize.Day)
                        klineTime1 = DateTimes.UtcToChinaTime(prodDef.LastOpen.Value).Date;

                    var k = new KLine()
                    {
                        Time = klineTime1,
                        Open = Quotes.GetLastPrice(firstQuote),
                        Close = Quotes.GetLastPrice(lastQuote),
                        High = orderedQuotes.Max(o => Quotes.GetLastPrice(o)),
                        Low = orderedQuotes.Min(o => Quotes.GetLastPrice(o)),
                    };

                    if (list.Count == 0)
                    {
                        list.Add(k);
                    }
                    else
                    {
                        var last = list[list.Count - 1];

                        if (last.Time < k.Time)
                        {
                            list.Add(k);
                        }
                        else if (last.Time == k.Time)
                        {
                            list[list.Count - 1] = new KLine()
                            {
                                Time = last.Time,
                                Open = last.Open,
                                Close = k.Close,
                                High = Math.Max(last.High, k.High),
                                Low = Math.Min(last.Low, k.Low),
                            };
                        }
                        else
                        {
                            //should not be here
                        }
                    }
                }
            }

            //clear history/prevent data increasing for good
            var clearWhenSize = KLines.GetClearWhenSize(kLineSize);
            var clearToSize = KLines.GetClearToSize(kLineSize);
            if (list.Count > clearWhenSize) //data count at most possible size (in x days )
            {
                CFDGlobal.LogLine("KLine " + kLineSize + " " + prodDef.Id + " Clearing data from " + list.Count + " to " + clearToSize);
                var klines = list.GetAll();
                var newKLines = klines.Skip(klines.Count - clearToSize);
                list.RemoveAll();
                list.AddRange(newKLines);
            }
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

                        using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(_isLive).GetClient())
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

                        if (!_isLive //demo
                            && entitiesToSaveToDB.Count > 0
                            )
                        {
                            dtBeginSave = DateTime.Now;
                            using (var dbHistory = CFDHistoryEntities.Create())
                            {
                                //using (var transactionScope = new TransactionScope())
                                //{
                                    dbHistory.BulkInsert(distinctQuotes.Select(o => new QuoteHistory
                                    {
                                        SecurityId = o.Id,
                                        Time = o.Time,
                                        Bid = o.Bid,
                                        Ask = o.Offer,
                                    }));
                                    dbHistory.SaveChanges();
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

                        using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(_isLive).GetClient())
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

                        using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(_isLive).GetClient())
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
                                        old.PreClose = Quotes.GetClosePrice(newProdDef) ??
                                        //when close ask/bid is null, get from ask/bid
                                                       Quotes.GetLastPrice(newProdDef);

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

        private static void SendProdDefRequest(object state)
        {
            while (true)
            {
                try
                {
                    CFDGlobal.LogLine("sending mds1 request...");
                    myApp.SendMDS1Request();
                }
                catch (Exception e)
                {
                    CFDGlobal.LogLine("sending mds1 request failed");
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(_intervalProdDefRequest);
            }
        }
    }
}