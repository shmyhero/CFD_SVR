using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Hosting;
using CFD_COMMON;
using CFD_JOBS.Ayondo;
using QuickFix;
using QuickFix.Transport;
using ServiceStack.Common.Extensions;

namespace AyondoTrade
{
    public class FIXApp
    {
        // Singleton instance
        private static readonly Lazy<AyondoFixTradeApp> _instance = new Lazy<AyondoFixTradeApp>(delegate
        {
            var fixApp = new AyondoFixTradeApp();

            CFDGlobal.LogLine("Starting FIX initiator...");

            var path = CFDGlobal.GetConfigurationSetting("ayondoFixTradeCfgFilePath");
            var serverPath = HostingEnvironment.MapPath("~/"+path);

            SessionSettings settings = new SessionSettings(serverPath);

            //var sessionIds = settings.GetSessions();
            var dictionary = settings.Get(new SessionID("FIX.4.4", "THCN_Trade", "TXIOBridge"));
            dictionary.SetString("DataDictionary", HostingEnvironment.MapPath("~/Fix44.xml"));
            dictionary.SetString("FileStorePath", HostingEnvironment.MapPath("~/fixfiles"));
            //settings.Set(new SessionID("FIX.4.4", "THCN_Trade", "TXIOBridge"), new Dictionary("DataDictionary",));

            IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
            //ILogFactory logFactory = new FileLogFactory(settings);
            SocketInitiator initiator = new SocketInitiator(fixApp, storeFactory, settings, null);

            initiator.Start();

            CFDGlobal.LogLine("FIX initiator started.");

            return fixApp;
        });

        public static AyondoFixTradeApp Instance
        {
            get { return _instance.Value; }
        }
    }
}