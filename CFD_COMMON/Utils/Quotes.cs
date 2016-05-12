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

        //public static decimal? GetLastPrice(AyondoSecurity security)
        //{
        //    return (security.Ask + security.Bid) / 2;
        //}

        public static decimal? GetLastPrice(ProdDef prodDef)
        {
            return (prodDef.Offer + prodDef.Bid) / 2;
        }

        public static decimal? GetClosePrice(ProdDef prodDef)
        {
            return (prodDef.CloseAsk + prodDef.CloseBid) / 2;
        }

        public static decimal? GetOpenPrice(ProdDef prodDef)
        {
            return (prodDef.OpenAsk + prodDef.OpenBid) / 2;
        }
    }
}
