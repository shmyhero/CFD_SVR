using QuickFix;
using QuickFix.Transport;

namespace CFD_JOBS.Ayondo
{
    internal class AyondoQuoteWorker
    {
        public static void Run()
        {
            SessionSettings settings = new SessionSettings("Ayondo\\AyondoQuoteFeed.cfg");
            AyondoQuoteApp myApp = new AyondoQuoteApp();
            IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
            ILogFactory logFactory = new FileLogFactory(settings);
            SocketInitiator initiator = new SocketInitiator(myApp, storeFactory, settings, logFactory);

            initiator.Start();
            while (true)
            {
                //System.Console.WriteLine("o hai");
                System.Threading.Thread.Sleep(1000);
            }
            initiator.Stop();
        }
    }
}