using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Entities;

namespace CFD_COMMON.Utils
{
    public class Quotes
    {
        public static decimal GetLastPrice(Quote quote)
        {
            return (quote.Offer + quote.Bid)/2;
        }
        public static decimal GetLastPrice(AyondoSecurity security)
        {
            return (security.Ask.Value + security.Bid.Value) / 2;
        }
        public static decimal GetLastPrice(ProdDef prodDef)
        {
            return (prodDef.Offer.Value + prodDef.Bid.Value) / 2;
        }

        public static decimal GetClosePrice(ProdDef prodDef)
        {
            return (prodDef.CloseAsk.Value + prodDef.CloseBid.Value) / 2;
        }

        public static decimal GetOpenPrice(ProdDef prodDef)
        {
            return (prodDef.OpenAsk.Value + prodDef.OpenBid.Value) / 2;
        }
    }
}
