using System;
using System.Collections.ObjectModel;
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
            var host = new ServiceHost(typeof(AyondoTradeService));

            //var hostname = "localhost";
            var hostname = "192.168.20.143";
            //var hostname = "cfd-job.chinacloudapp.cn";

            //tcp at 14001
            host.AddServiceEndpoint(typeof(IAyondoTradeService), new NetTcpBinding(SecurityMode.None), new Uri("net.tcp://"+hostname+":14001/ayondo"));
            //http at 14002
            host.AddServiceEndpoint(typeof(IAyondoTradeService), new BasicHttpBinding(BasicHttpSecurityMode.None), new Uri("http://" + hostname + ":14002/ayondo"));

            // Enable metadata publishing.
            ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            smb.HttpGetUrl = new Uri("http://"+hostname+":14002/ayondo");
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            host.Description.Behaviors.Add(smb);
            
            ////auth
            //var collection = new ReadOnlyCollection<IAuthorizationPolicy>(new IAuthorizationPolicy[] { new MyPolicy() });
            //ServiceAuthorizationBehavior sa = host.Description.Behaviors.Find<ServiceAuthorizationBehavior>();
            //if (sa == null)
            //{
            //    sa = new ServiceAuthorizationBehavior();
            //    host.Description.Behaviors.Add(sa);
            //}
            //sa.ExternalAuthorizationPolicies = collection;

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

    //internal class MyPolicy : IAuthorizationPolicy
    //{
    //    public string Id { get; private set; }
    //    public bool Evaluate(EvaluationContext evaluationContext, ref object state)
    //    {
    //        return true;
    //    }

    //    public ClaimSet Issuer { get; private set; }
    //}
}