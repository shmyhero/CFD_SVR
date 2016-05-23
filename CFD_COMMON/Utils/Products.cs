using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class Products
    {
        public static bool IsUsStocks(string symbol)
        {
            return symbol.EndsWith(" UW") || symbol.EndsWith(" UN");
        }
    }
}
