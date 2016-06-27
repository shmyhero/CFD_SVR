using System;
using System.Diagnostics;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace AyondoTrade
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        public override void Run()
        {
            Trace.TraceInformation("AyondoTrade is running");

            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 10000;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            //trigger fix connection
            var session = Global.FixApp.Session;

            //create WCF host
            CreateServiceHost();

            bool result = base.OnStart();

            Trace.TraceInformation("AyondoTrade has been started");

            return result;
        }

        private ServiceHost serviceHost;

        private void CreateServiceHost()
        {
            serviceHost = new ServiceHost(typeof (AyondoTradeService));

            NetTcpBinding binding = new NetTcpBinding(SecurityMode.None);
            binding.MaxConnections = 10000;

            RoleInstanceEndpoint externalEndPoint =
                RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["WCFEndpoint"];
            string endpoint = String.Format("net.tcp://{0}/AyondoTrade", externalEndPoint.IPEndpoint);

            serviceHost.AddServiceEndpoint(typeof (IAyondoTradeService), binding, endpoint);

            //enable exception detail in faults
            var serviceDebugBehavior = serviceHost.Description.Behaviors.Find<ServiceDebugBehavior>();
            serviceDebugBehavior.IncludeExceptionDetailInFaults = true;

            serviceHost.Description.Behaviors.Add(new ServiceThrottlingBehavior
            {
                MaxConcurrentSessions = 10000,
                MaxConcurrentCalls = 10000,
                MaxConcurrentInstances = 10000,
            });

            serviceHost.Open();
        }

        public override void OnStop()
        {
            Trace.TraceInformation("AyondoTrade is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("AyondoTrade has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                //Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }
    }
}