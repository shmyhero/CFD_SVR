using System.Collections.Generic;
using System.ServiceModel;
using AyondoTrade.Model;

namespace AyondoTrade
{
    public class AyondoTradeClient : ClientBase<IAyondoTradeService>, IAyondoTradeService
    {
        public AyondoTradeClient(System.ServiceModel.Channels.Binding binding, EndpointAddress edpAddr)
            : base(binding, edpAddr)
        {
        }

        public string Test(string text)
        {
            return base.Channel.Test(text);
        }

        public IList<Model.PositionReport> GetPositionReport(string username, string password)
        {
            return base.Channel.GetPositionReport(username, password);
        }

        public PositionReport NewOrder(string username, string password, int securityId, bool isLong, decimal orderQty,
            decimal? leverage = null, decimal? stopPx = null, string nettingPositionId = null)
        {
            return base.Channel.NewOrder(username, password, securityId, isLong, orderQty,
                leverage: leverage, stopPx: stopPx, nettingPositionId: nettingPositionId);
        }

        public decimal GetBalance(string username, string password)
        {
            return base.Channel.GetBalance(username, password);
        }
    }
}