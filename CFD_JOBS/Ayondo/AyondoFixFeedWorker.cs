using System.Collections.Generic;
using CFD_COMMON;
using CFD_COMMON.Models;
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

                    IList<ProdDef> list = new List<ProdDef>();

                    while (!myApp.ProdDefs.IsEmpty)
                    {
                        ProdDef obj;
                        var tryDequeue = myApp.ProdDefs.TryDequeue(out obj);
                        list.Add(obj);
                    }

                    CFDGlobal.LogLine("Saving " + list.Count + " ProdDefs to Redis...");
                    redisProdDefClient.StoreAll(list);
                }
            }
            //initiator.Stop();
        }
    }
}