using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using QuickFix.FIX44;

namespace AyondoTrade
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IAyondoTradeService" in both code and config file together.
    [ServiceContract]
    public interface IAyondoTradeService
    {
        [OperationContract]
        string Test(string text);

        [OperationContract]
        IList<Model.PositionReport> GetPositionReport(string username, string password);

        [OperationContract]
        Model.PositionReport NewOrder(string username, string password, int securityId, bool isLong, decimal orderQty, string nettingPositionId);
    }
}
