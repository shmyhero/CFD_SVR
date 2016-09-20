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

            if (IsUKStocks(symbol))
                return "UK";

            if (IsFrenchStocks(symbol))
                return "FR";

            if (IsGermanStocks(symbol))
                return "DE";

            if (IsSpanishStocks(symbol))
                return "ES";

            if (IsSwedishStocks(symbol))
                return "SE";

            if (IsSwissStocks(symbol))
                return "CH";

            return null;
        }

        //public static int GetTimeZoneOffset(string symbol)
        //{
        //    return 0;
        //}

        public static bool IsUSStocks(string symbol)
        {
            return symbol.EndsWith(" UW") || symbol.EndsWith(" UN");
        }

        public static bool IsHKStocks(string symbol)
        {
            return symbol.EndsWith(" HK");
        }

        public static bool IsUKStocks(string symbol)
        {
            return symbol.EndsWith(" LN");
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