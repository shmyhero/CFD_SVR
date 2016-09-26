using System;
using System.Collections.Generic;
using System.ServiceModel;
using AyondoTrade.FaultModel;
using AyondoTrade.Model;

namespace AyondoTrade
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IAyondoTradeService" in both code and config file together.
    [ServiceContract]
    public interface IAyondoTradeService
    {
        [OperationContract]
        string Test(string text);

        [OperationContract]
        string TestSleep(TimeSpan ts);

        [OperationContract]
        bool IsFixLoggingIn();

        [OperationContract]
        IList<PositionReport> DataTest(int count);

        [OperationContract]
        IList<PositionReport> GetPositionReport(string username, string password, bool ignoreCache = false);

        [OperationContract]
        IList<PositionReport> GetPositionHistoryReport(string username, string password, DateTime startTime, DateTime endTime, bool ignoreCache, bool updateCache);

        [OperationContract]
        IDictionary<string,IList<PositionReport>> PopAutoClosedPositionReports(IList<string> usernames);

        [OperationContract]
        [FaultContract(typeof (OrderRejectedFault))]
        PositionReport NewOrder(string username, string password, int securityId, bool isLong, decimal orderQty, //char? ordType = null, decimal? price = null,
            decimal? leverage = null, decimal? stopPx = null, string nettingPositionId = null);

        [OperationContract]
        [FaultContract(typeof(OrderRejectedFault))]
        PositionReport NewTakeOrder(string username, string password, int securityId, decimal price,string nettingPositionId);

        [OperationContract]
        [FaultContract(typeof (OrderRejectedFault))]
        PositionReport ReplaceOrder(string username, string password, int securityId, string orderId, decimal price, string nettingPositionId);

        [OperationContract]
        [FaultContract(typeof(OrderRejectedFault))]
        PositionReport CancelOrder(string username, string password, int securityId, string orderId, string nettingPositionId);

        [OperationContract]
        decimal GetBalance(string username, string password, bool ignoreCache = false);

        [OperationContract]
        string PrintCache(string username);

        [OperationContract]
        void SwitchCache(string mode);

        [OperationContract]
        void ClearCache(string username);

        [OperationContract]
        string LoginOAuth(string username, string oauthToken);
    }
}