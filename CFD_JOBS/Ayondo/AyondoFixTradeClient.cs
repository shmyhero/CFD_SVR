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

            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            //var r1 = clientTcp.Test("haha tcp");
            //var r2 = clientHttp.Test("haha http");
            var positionReport = clientHttp.GetPositionReport("jiangyi1985", "ivan");
        }
    }
}
