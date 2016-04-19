using System;
using System.Threading;
using System.Web.Hosting;
using CFD_COMMON;
using CFD_JOBS.Ayondo;
using QuickFix;
using QuickFix.Transport;

namespace AyondoTrade
{
    public class Global
    {
        // Singleton instance
        private static readonly Lazy<AyondoFixTradeApp> _instance = new Lazy<AyondoFixTradeApp>(delegate
        {
            var application = new AyondoFixTradeApp();

            CFDGlobal.LogLine("Starting FIX initiator...");

            var path = CFDGlobal.GetConfigurationSetting("ayondoFixTradeCfgFilePath");
            var serverPath = HostingEnvironment.MapPath("~/" + path);

            SessionSettings settings = new SessionSettings(serverPath);

            //var sessionIds = settings.GetSessions();
            var dictionary = settings.Get(new SessionID("FIX.4.4", "THCN_Trade", "TXIOBridge"));
            dictionary.SetString("DataDictionary", HostingEnvironment.MapPath("~/Fix44.xml"));
            dictionary.SetString("FileStorePath", HostingEnvironment.MapPath("~/fixfiles"));
            //settings.Set(new SessionID("FIX.4.4", "THCN_Trade", "TXIOBridge"), new Dictionary("DataDictionary",));

            IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
            //ILogFactory logFactory = new FileLogFactory(settings);
            SocketInitiator initiator = new SocketInitiator(application, storeFactory, settings, null);

            initiator.Start();

            CFDGlobal.LogLine("FIX initiator started. Waiting for FIX session...");

            var dt = DateTime.UtcNow;

            while (application.Session == null)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));

                if (DateTime.UtcNow - dt >= TimeSpan.FromMinutes(1))
                {
                    CFDGlobal.LogWarning("FIX session establish timeout.");
                    break;
                }
            }

            if (application.Session != null)
                CFDGlobal.LogLine("FIX session established.");

            return application;
        });

        public static AyondoFixTradeApp FixApp
        {
            get { return _instance.Value; }
        }
    }
}