using System;
using CFD_COMMON;
using CFD_JOBS.Ayondo;
using Microsoft.WindowsAzure.ServiceRuntime;
using QuickFix;
using QuickFix.Transport;

namespace AyondoTrade
{
    public class WebRole : RoleEntryPoint
    {
       public override bool OnStart()
        {
            //try
            //{
            //    CFDGlobal.LogLine("Starting FIX initiator...");

            //    SessionSettings settings = new SessionSettings(CFDGlobal.GetConfigurationSetting("ayondoFixTradeCfgFilePath"));

            //    IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
            //    //ILogFactory logFactory = new FileLogFactory(settings);
            //    SocketInitiator initiator = new SocketInitiator(FIXApp, storeFactory, settings, null);

            //    initiator.Start();

            //    CFDGlobal.LogLine("FIX initiator started.");
            //}
            //catch (Exception e)
            //{
            //}

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            return base.OnStart();
        }

        public override void Run()
        {
            base.Run();
        }

        public override void OnStop()
        {
            base.OnStop();
        }
    }
}