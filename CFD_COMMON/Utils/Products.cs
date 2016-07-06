using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class Products
    {
        public static string GetStockTag(string symbol)
        {
            if (IsUSStocks(symbol))
                return "US";

            if (IsHKStocks(symbol))
                return "HK";

            return null;
        }

        public static bool IsUSStocks(string symbol)
        {
            return symbol.EndsWith(" UW") || symbol.EndsWith(" UN");
        }

        public static bool IsHKStocks(string symbol)
        {
            return symbol.EndsWith(" HK");
        }

        public static bool IsGermanStocks(string symbol)
        {
            return symbol.EndsWith(" GY");
        }

        /// <summary>
        /// 瑞士
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static bool IsSwissStocks(string symbol)
        {
            return symbol.EndsWith(" VX");
        }

        /// <summary>
        /// 瑞典
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static bool IsSwedishStocks(string symbol)
        {
            return symbol.EndsWith(" SS");
        }

        public static bool IsSpanishStocks(string symbol)
        {
            return symbol.EndsWith(" SM");
        }

        public static bool IsFrenchStocks(string symbol)
        {
            return symbol.EndsWith(" FP");
        }
    }
}
