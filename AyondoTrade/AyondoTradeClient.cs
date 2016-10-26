using System;
using System.Collections.Generic;
using System.ServiceModel;
using AyondoTrade.Model;
using CFD_COMMON;
using System.ServiceModel.Channels;

namespace AyondoTrade
{
    public class AyondoTradeClient : ClientBase<IAyondoTradeService>, IAyondoTradeService, IDisposable
    {
        public AyondoTradeClient(System.ServiceModel.Channels.Binding binding, EndpointAddress edpAddr)
            : base(binding, edpAddr)
        {
            var scope = new OperationContextScope(base.InnerChannel);
            MessageHeader myHeader = MessageHeader.CreateHeader(Global.WCF_MSG_HEADER_TOKEN_KEY, Global.WCF_MSG_HEADER_TOKEN_NS, Global.WCF_MSG_HEADER_TOKEN_VALUE);
            OperationContext.Current.OutgoingMessageHeaders.Add(myHeader);
        }

        public AyondoTradeClient()
            : this(
                new NetTcpBinding(SecurityMode.None) {MaxReceivedMessageSize = 10*1024*1024,},
                new EndpointAddress(CFDGlobal.AYONDO_TRADE_SVC_URL)
                  )
        {
        }

        //public static AyondoTradeClient GetAyondoTradeClient()
        //{
        //    //EndpointAddress edpHttp = new EndpointAddress(CFDGlobal.AYONDO_TRADE_SVC_URL);
        //    EndpointAddress edpTcp = new EndpointAddress(CFDGlobal.AYONDO_TRADE_SVC_URL);

        //    //var basicHttp = new BasicHttpBinding(BasicHttpSecurityMode.None);
        //    var netTcp = new NetTcpBinding(SecurityMode.None);

        //    //The maximum message size quota for incoming messages (65536) has been exceeded. To increase the quota, use the MaxReceivedMessageSize property on the appropriate binding element.
        //    //basicHttp.MaxReceivedMessageSize = 10*1024*1024;
        //    netTcp.MaxReceivedMessageSize = 10 * 1024 * 1024;

        //    //AyondoTradeClient clientHttp = new AyondoTradeClient(basicHttp, edpHttp);
        //    AyondoTradeClient clientTcp = new AyondoTradeClient(netTcp, edpTcp);

        //    //return clientHttp;
        //    return clientTcp;
        //}

        public string Test(string text)
        {
            return base.Channel.Test(text);
        }

        public string TestSleep(TimeSpan ts)
        {
            return base.Channel.TestSleep(ts);
        }

        public void TestException()
        {
            base.Channel.TestException();
        }

        public bool IsFixLoggingIn()
        {
            return base.Channel.IsFixLoggingIn();
        }

        public IList<PositionReport> DataTest(int count)
        {
            return base.Channel.DataTest(count);
        }

        public IList<PositionReport> GetPositionReport(string username, string password, bool ignoreCache = false)
        {
            return base.Channel.GetPositionReport(username, password, ignoreCache);
        }

        public IList<PositionReport> GetPositionHistoryReport(string username, string password, DateTime startTime, DateTime endTime, bool ignoreCache = false, bool updateCache = true)
        {
            return base.Channel.GetPositionHistoryReport(username, password, startTime, endTime, ignoreCache, updateCache);
        }

        public IDictionary<string, IList<PositionReport>> PopAutoClosedPositionReports(IList<string> usernames)
        {
            return base.Channel.PopAutoClosedPositionReports(usernames);
        }

        public PositionReport NewOrder(string username, string password, int securityId, bool isLong, decimal orderQty, //char? ordType = null, decimal? price = null, 
            decimal? leverage = null, decimal? stopPx = null, string nettingPositionId = null)
        {
            return base.Channel.NewOrder(username, password, securityId, isLong, orderQty, //ordType: ordType, price: price,
                 leverage: leverage, stopPx: stopPx, nettingPositionId: nettingPositionId);
        }

        public PositionReport NewTakeOrder(string username, string password, int securityId, decimal price, string nettingPositionId)
        {
            return base.Channel.NewTakeOrder(username, password, securityId, price, nettingPositionId);
        }

        public PositionReport ReplaceOrder(string username, string password, int securityId, string orderId, decimal price,string nettingPositionId)
        {
            return base.Channel.ReplaceOrder(username, password, securityId, orderId, price, nettingPositionId);
        }

        public PositionReport CancelOrder(string username, string password, int securityId, string orderId, string nettingPositionId)
        {
            return base.Channel.CancelOrder(username, password, securityId, orderId, nettingPositionId);
        }

        public decimal GetBalance(string username, string password, bool ignoreCache = false)
        {
            return base.Channel.GetBalance(username, password);
        }

        public string PrintCache(string username)
        {
            return base.Channel.PrintCache(username);
        }

        public void SwitchCache(string mode)
        {
            base.Channel.SwitchCache(mode);
        }

        public void ClearCache(string username)
        {
            base.Channel.ClearCache(username);
        }

        public string LoginOAuth(string username, string oauthToken)
        {
            return base.Channel.LoginOAuth(username, oauthToken);
        }

        public string NewDeposit(string username, string password, decimal amount)
        {
            return base.Channel.NewDeposit(username, password, amount);
        }

        public void LogOut(string username)
        {
            base.Channel.LogOut(username);
        }

        //important
        //https://msdn.microsoft.com/en-us/library/aa355056.aspx
        //https://devzone.channeladam.com/articles/2014/07/how-to-call-wcf-service-properly/
        public void Dispose()
        {
            bool success = false;
            try
            {
                if (State != CommunicationState.Faulted)
                {
                    Close();
                    success = true;
                }
            }
            finally
            {
                if (!success)
                {
                    Abort();
                }
            }
        }
    }
}