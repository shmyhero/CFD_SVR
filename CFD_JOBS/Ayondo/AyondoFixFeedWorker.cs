using System.Collections.Generic;
using System.Linq;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using QuickFix;
using QuickFix.Transport;

namespace CFD_JOBS.Ayondo
{
    internal class AyondoFixFeedWorker
    {
        public static void Run()
        {
            SessionSettings settings = new SessionSettings(CFDGlobal.GetConfigurationSetting("ayondoFixFeedCfgFilePath"));
            AyondoFixFeedApp myApp = new AyondoFixFeedApp();
            IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
            //ILogFactory logFactory = new FileLogFactory(settings);
            SocketInitiator initiator = new SocketInitiator(myApp, storeFactory, settings
                //,logFactory
                );

            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();
            var redisProdDefClient = basicRedisClientManager.GetClient().As<ProdDef>();

            initiator.Start();
            while (true)
            {
                //System.Console.WriteLine("o hai");
                System.Threading.Thread.Sleep(1000);

                //save ProdDefs
                if (!myApp.ProdDefs.IsEmpty)
                {
                    //CFDGlobal.LogLine("Pending ProdDefs detected. Loading from queue...");

                    //new prod list from Ayondo MDS2
                    IList<ProdDef> listNew = new List<ProdDef>();

                    while (!myApp.ProdDefs.IsEmpty)
                    {
                        ProdDef obj;
                        var tryDequeue = myApp.ProdDefs.TryDequeue(out obj);
                        listNew.Add(obj);
                    }

                    CFDGlobal.LogLine("Saving " + listNew.Count + " ProdDefs to Redis...");

                    //current redis list
                    var listOld = redisProdDefClient.GetAll();

                    IList<ProdDef> listToSave = new List<ProdDef>();

                    foreach (var newProdDef in listNew)
                    {
                        var old = listOld.FirstOrDefault(o => o.Id == newProdDef.Id);

                        if (old != null) //updating prod def in redis
                        {
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

                            //update state
                            if (old.QuoteType != enmQuoteType.Closed && newProdDef.QuoteType == enmQuoteType.Closed) //xxx -> close
                            {
                                CFDGlobal.LogLine("PROD CLOSED " + newProdDef.Id + " time: " + newProdDef.Time);

                                //close time
                                old.LastClose = newProdDef.Time;
                            }
                            else if (old.QuoteType != enmQuoteType.Open && newProdDef.QuoteType == enmQuoteType.Open) //xxx -> open
                            {
                                CFDGlobal.LogLine("PROD OPENED " + newProdDef.Id + " time: " + newProdDef.Time + " offer: " + newProdDef.Offer + " bid: " + newProdDef.Bid);

                                //open time
                                old.LastOpen = newProdDef.Time;

                                //open prices
                                old.OpenAsk = newProdDef.Offer;
                                old.OpenBid = newProdDef.Bid;
                            }

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
            //initiator.Stop();
        }
    }
}