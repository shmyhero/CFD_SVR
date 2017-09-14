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
        void TestException();

        [OperationContract]
        bool IsFixLoggingIn();

        [OperationContract]
        IList<PositionReport> DataTest(int count);

        [OperationContract]
        [FaultContract(typeof(OAuthLoginRequiredFault))]
        IList<PositionReport> GetPositionReport(string username, string password, bool ignoreCache = false);

        [OperationContract]
        [FaultContract(typeof(OAuthLoginRequiredFault))]
        IList<PositionReport> GetPositionHistoryReport(string username, string password, DateTime startTime, DateTime endTime, bool ignoreCache, bool updateCache);

        [OperationContract]
        IDictionary<string,IList<PositionReport>> PopAutoClosedPositionReports(IList<string> usernames);

        [OperationContract]
        [FaultContract(typeof (OrderRejectedFault))]
        [FaultContract(typeof(OAuthLoginRequiredFault))]
        PositionReport NewOrder(string username, string password, int securityId, bool isLong, decimal orderQty, //char? ordType = null, decimal? price = null,
            decimal? leverage = null, decimal? stopPx = null, string nettingPositionId = null);

        [OperationContract]
        [FaultContract(typeof(OrderRejectedFault))]
        [FaultContract(typeof(OAuthLoginRequiredFault))]
        PositionReport NewTakeOrder(string username, string password, int securityId, decimal price,string nettingPositionId);

        [OperationContract]
        [FaultContract(typeof(OrderRejectedFault))]
        [FaultContract(typeof(OAuthLoginRequiredFault))]
        PositionReport ReplaceOrder(string username, string password, int securityId, string orderId, decimal price, string nettingPositionId);

        [OperationContract]
        [FaultContract(typeof(OrderRejectedFault))]
        [FaultContract(typeof(OAuthLoginRequiredFault))]
        PositionReport CancelOrder(string username, string password, int securityId, string orderId, string nettingPositionId);

        [OperationContract]
        [FaultContract(typeof(OAuthLoginRequiredFault))]
        decimal GetBalance(string username, string password, bool ignoreCache = false);

        [OperationContract]
        string PrintCache(string username);

        [OperationContract]
        void SwitchCache(string mode);

        [OperationContract]
        void ClearCache(string username);

        [OperationContract]
        string LoginOAuth(string username, string oauthToken);

        [OperationContract]
        [FaultContract(typeof(OAuthLoginRequiredFault))]
        string NewDeposit(string username, string password, decimal amount, TransferType transferType);

        [OperationContract]
        [FaultContract(typeof(OAuthLoginRequiredFault))]
        [FaultContract(typeof(MDSTransferErrorFault))]
        string NewWithdraw(string username, string password, decimal amount);

        [OperationContract]
        [FaultContract(typeof(OAuthLoginRequiredFault))]
        [FaultContract(typeof(MDSTransferErrorFault))]
        string NewCashTransfer(string username, string password, decimal amount, string targetBalanceId);

        [OperationContract]
        void LogOut(string username);
    }
}