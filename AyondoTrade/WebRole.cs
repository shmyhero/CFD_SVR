using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using CFD_COMMON;
using CFD_JOBS.Ayondo;
using Microsoft.Web.Administration;
using Microsoft.WindowsAzure.ServiceRuntime;
using QuickFix;
using QuickFix.Transport;

namespace AyondoTrade
{
    public class WebRole : RoleEntryPoint
    {
       public override bool OnStart()
        {
           //!!!!!!!!!!!!!!!!
           //cannot do veriable initiating here
           //this roleEntryPoint is called by WAIISHOST.exe whereas the WCF service is called by another process (w3wp.exe if hosted in IIS)
           //any initiating here is in vain
           //!!!!!!!!!!!!!!!!

           Trace.TraceInformation("Role OnStart");

           //---------------try to set IIS AutoStart-------------------------
           //ServicePointManager.DefaultConnectionLimit = 12;
           if (!RoleEnvironment.IsEmulated)
           {
               using (ServerManager serverManager = new ServerManager())
               {
                   foreach (var app in serverManager.Sites.SelectMany(x => x.Applications))
                   {
                       app["preloadEnabled"] = true;
                   }
                   foreach (var appPool in serverManager.ApplicationPools)
                   {
                       appPool.AutoStart = true;
                       appPool["startMode"] = "AlwaysRunning";
                       appPool.ProcessModel.IdleTimeout = TimeSpan.Zero;
                       appPool.Recycling.PeriodicRestart.Time = TimeSpan.Zero;
                   }
                   serverManager.CommitChanges();
               }
           }
           //------------------------------------------------------------------

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            return base.OnStart();
        }

        public override void Run()
       {
           Trace.TraceInformation("Role Run");

            base.Run();
        }

        public override void OnStop()
        {
            Trace.TraceInformation("Role OnStop");

            base.OnStop();
        }
    }
}