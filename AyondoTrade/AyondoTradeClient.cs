using System.Collections.Generic;
using System.ServiceModel;
using QuickFix.FIX44;

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
    }
}