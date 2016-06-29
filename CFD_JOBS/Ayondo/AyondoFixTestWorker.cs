using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Utils;
using QuickFix;
using QuickFix.Transport;

namespace CFD_JOBS.Ayondo
{
    class AyondoFixTestWorker
    {
        public static void Run()
        {
            var settings = new SessionSettings("AyondoTest.cfg");
            var myApp = new AyondoFixTestApp();
            var storeFactory = new MemoryStoreFactory();
            var logFactory = new FileLogFactory(settings);
            var initiator = new SocketInitiator(myApp, storeFactory, settings, logFactory);

            initiator.Start();

            while (true)
            {
                Thread.Sleep(1000);
            }

            //initiator.Stop();
        }
    }
}
