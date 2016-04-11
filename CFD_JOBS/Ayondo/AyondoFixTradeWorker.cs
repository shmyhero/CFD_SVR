using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using CFD_COMMON;
using QuickFix;
using QuickFix.Transport;

namespace CFD_JOBS.Ayondo
{
    internal class AyondoFixTradeWorker
    {
        public static void Run()
        {
            //Start FIX
            SessionSettings settings = new SessionSettings(CFDGlobal.GetConfigurationSetting("ayondoFixTradeCfgFilePath"));
            AyondoFixTradeApp myApp = new AyondoFixTradeApp();
            IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
            ILogFactory logFactory = new FileLogFactory(settings);
            SocketInitiator initiator = new SocketInitiator(myApp, storeFactory, settings, logFactory);

            initiator.Start();

            //CFDGlobal.LogLine("job main worker id " + Thread.CurrentThread.ManagedThreadId.ToString());

            //Start WCF Service
            Uri baseAddress = new Uri("http://localhost:14001/ayondo");
            var host = new ServiceHost(typeof(AyondoTradeService), baseAddress);

            // Enable metadata publishing.
            ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            host.Description.Behaviors.Add(smb);

            host.Open();
            //host.Close();


            myApp.Run();
            //while (true)
            //{
            //    //System.Console.WriteLine("o hai");
            //    System.Threading.Thread.Sleep(1000);
            //}

            initiator.Stop();
        }
    }
}