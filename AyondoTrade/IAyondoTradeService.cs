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
        IList<PositionReport> GetPositionReport(string username, string password);

        [OperationContract]
        [FaultContract(typeof (OrderRejectedFault))]
        PositionReport NewOrder(string username, string password, int securityId, bool isLong, decimal orderQty, decimal? leverage = null, decimal? stopPx = null, string nettingPositionId = null);

        [OperationContract]
        decimal GetBalance(string username, string password);
    }
}