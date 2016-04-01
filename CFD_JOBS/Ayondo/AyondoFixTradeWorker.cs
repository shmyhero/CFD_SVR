using CFD_COMMON;
using QuickFix;
using QuickFix.Transport;

namespace CFD_JOBS.Ayondo
{
    internal class AyondoFixTradeWorker
    {
        public static void Run()
        {
            SessionSettings settings = new SessionSettings(CFDGlobal.GetConfigurationSetting("ayondoFixTradeCfgFilePath"));
            AyondoFixTradeApp myApp = new AyondoFixTradeApp();
            IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
            ILogFactory logFactory = new FileLogFactory(settings);
            SocketInitiator initiator = new SocketInitiator(myApp, storeFactory, settings, logFactory);

            initiator.Start();

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