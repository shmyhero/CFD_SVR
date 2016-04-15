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
           //!!!
           //cannot do veriable initiating here
           //this roleEntryPoint is called by WAIISHOST.exe whereas the WCF service is called by another process (w3wp.exe if hosted in IIS)
           //any initiating here is in vain

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