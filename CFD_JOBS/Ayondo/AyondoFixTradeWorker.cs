using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using AyondoTrade;
using CFD_COMMON;
using QuickFix;
using QuickFix.FIX44;
using QuickFix.Transport;

namespace CFD_JOBS.Ayondo
{
    //public class MyLogFactory : ILogFactory
    //{
    //    public ILog Create(SessionID sessionID)
    //    {
    //        return new MyLog();
    //    }
    //}
    //public class MyLog : ILog
    //{
    //    IList<string> logs=new List<string>(); 

    //    public void Dispose()
    //    {
    //        //throw new NotImplementedException();
    //    }

    //    public void Clear()
    //    {
    //        //throw new NotImplementedException();
    //    }

    //    public void OnIncoming(string msg)
    //    {
    //        logs.Add(DateTime.UtcNow.ToString("HH:mm:ss.ffffff") + " "+msg);
    //    }

    //    public void OnOutgoing(string msg)
    //    {
    //        //throw new NotImplementedException();
    //    }

    //    public void OnEvent(string s)
    //    {
    //        //throw new NotImplementedException();
    //    }
    //}

    internal class AyondoFixTradeWorker
    {
        public static void Run()
        {
            ////Start FIX
            //SessionSettings settings = new SessionSettings(CFDGlobal.GetConfigurationSetting("ayondoFixTradeCfgFilePath"));
            //AyondoFixTradeApp myApp = new AyondoFixTradeApp();
            //IMessageStoreFactory storeFactory = new MemoryStoreFactory();// FileStoreFactory(settings);
            //ILogFactory logFactory =new FileLogFactory(settings);
            //SocketInitiator initiator = new SocketInitiator(myApp, storeFactory, settings, logFactory);

            //initiator.Start();

            var serviceHost = new ServiceHost(typeof(AyondoTradeService));

            NetTcpBinding binding = new NetTcpBinding(SecurityMode.None);

            string endpoint = "net.tcp://localhost:10100/AyondoTrade";

            serviceHost.AddServiceEndpoint(typeof(IAyondoTradeService), binding, endpoint);

            //enable exception detail in faults
            var serviceDebugBehavior = serviceHost.Description.Behaviors.Find<ServiceDebugBehavior>();
            serviceDebugBehavior.IncludeExceptionDetailInFaults = true;

            serviceHost.Open();

            //CFDGlobal.LogLine("job main worker id " + Thread.CurrentThread.ManagedThreadId.ToString());

            ////Start WCF Service
            //var host = new ServiceHost(typeof(AyondoTradeService));

            ////var hostname = "localhost";
            //var hostname = "192.168.20.143";
            ////var hostname = "cfd-job.chinacloudapp.cn";

            ////tcp at 14001
            //host.AddServiceEndpoint(typeof(IAyondoTradeService), new NetTcpBinding(SecurityMode.None), new Uri("net.tcp://"+hostname+":14001/ayondo"));
            ////http at 14002
            //host.AddServiceEndpoint(typeof(IAyondoTradeService), new BasicHttpBinding(BasicHttpSecurityMode.None), new Uri("http://" + hostname + ":14002/ayondo"));

            //// Enable metadata publishing.
            //ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
            //smb.HttpGetEnabled = true;
            //smb.HttpGetUrl = new Uri("http://"+hostname+":14002/ayondo");
            //smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            //host.Description.Behaviors.Add(smb);

            ////auth
            //var collection = new ReadOnlyCollection<IAuthorizationPolicy>(new IAuthorizationPolicy[] { new MyPolicy() });
            //ServiceAuthorizationBehavior sa = host.Description.Behaviors.Find<ServiceAuthorizationBehavior>();
            //if (sa == null)
            //{
            //    sa = new ServiceAuthorizationBehavior();
            //    host.Description.Behaviors.Add(sa);
            //}
            //sa.ExternalAuthorizationPolicies = collection;

            //host.Open();
            //host.Close();

            Global.FixApp.Run();
            //while (true)
            //{
            //    //System.Console.WriteLine("o hai");
            //    System.Threading.Thread.Sleep(1000);
            //}

            //initiator.Stop();
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