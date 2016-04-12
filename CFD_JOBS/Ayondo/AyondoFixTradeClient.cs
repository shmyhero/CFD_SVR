using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using AyondoTrade;
using QuickFix.FIX44;

namespace CFD_JOBS.Ayondo
{
    public class AyondoFixTradeClient
    {
        public static void Run()
        {
            //EndpointAddress edpTcp = new EndpointAddress("net.tcp://localhost:38113/ayondotradeservice.svc");
            //EndpointAddress edpHttp = new EndpointAddress("http://localhost:38113/ayondotradeservice.svc");
            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");

            //MyClient clientTcp = new MyClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            MyClient clientHttp = new MyClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            //var r1 = clientTcp.Test("haha tcp");
            //var r2 = clientHttp.Test("haha http");
            var positionReport = clientHttp.GetPositionReport("jiangyi1985", "ivan");
        }
    }

    public class MyClient : ClientBase<IAyondoTradeService>, IAyondoTradeService
    {
        public MyClient(System.ServiceModel.Channels.Binding binding, EndpointAddress edpAddr)
            : base(binding, edpAddr) { }

        public string Test(string text)
        {
            return base.Channel.Test(text);
        }

        public IList<PositionReport> GetPositionReport(string username, string password)
        {
            return base.Channel.GetPositionReport(username, password);
        }
    }  
}
