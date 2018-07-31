using System;
using System.Collections.Generic;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Cached;

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

        //public static IList<int> IDFilterList = new List<int>()
        //{
        //    34821,
        //    34847,
        //    20951,
        //    20950,
        //    29779,
        //    29782,
        //    20952,
        //    29773,
        //    21612,
        //    20906,
        //    20949,
        //    29776,
        //    20953,
        //    20954,
        //    34781,
        //    29724,
        //    29727,
        //    20957,
        //    22422,
        //    29718,
        //    29721,
        //    34703,
        //    34706,
        //    34709,
        //    34721,
        //    34712,
        //    34724,
        //    34715,
        //    34727,
        //    34718,
        //    34730,
        //    34733,
        //    34736,
        //    34739,
        //    34742,
        //    34745,
        //    36236,
        //    36242,
        //    36240,
        //    36229,
        //    36234,
        //    36238,
        //    10880,
        //    10879,
        //    34802,
        //    10874,
        //    22414,
        //    10870,
        //    34803,
        //    29534,
        //    10877,
        //    34804,
        //    22419,
        //    10891,
        //    10875,
        //    10885,
        //    10876,
        //    34805,
        //    10883,
        //    10895,
        //    10892,
        //    34815,
        //    10871,
        //    24365,
        //    29531,
        //    10893,
        //    34816,
        //    22295,
        //    10872,
        //    21906,
        //    10873,
        //    34817,
        //    20958,
        //    22578,
        //    22585,
        //    29791,
        //    29797,
        //    21613,
        //    29785,
        //    29794,
        //    21614,
        //    29788,
        //    29800,
        //    29730,
        //    29736,
        //    29739,
        //    29742,
        //    28419,
        //    29745,
        //    22425,
        //    29733,
        //    29748,
        //    29754,
        //    21909,
        //    29764,
        //    29767,
        //    29757,
        //    29751,
        //    29770,
        //    29761,
        //    20955,
        //    34859,
        //    20956,
        //    34548,
        //    29715,
        //    29528,
        //    34879,
        //    21615,
        //    34860,
        //    20959,
        //    21616,
        //    20960,
        //    20961,
        //    34747,
        //    36340,
        //    34748,
        //    34749,
        //    34750,
        //    34751,
        //    34752,
        //    34753,
        //    34754,
        //    34755,
        //    34756,
        //    34757,
        //    34758,
        //    34759,
        //    34760,
        //    34761,
        //    36334,
        //    34762,
        //    34763,
        //    34764,
        //    34765,
        //    34766,
        //    34767,
        //    34768,
        //    34769,
        //    34770,
        //    34771,
        //    34772,
        //    34773,
        //    34774,
        //    34775,
        //    34776,
        //    34777,
        //    34778,
        //    34779,
        //    34780,
        //    34782,
        //    34784,
        //    34785,
        //    34786,
        //    34787,
        //    34788,
        //    34789,
        //    34790,
        //    34791,
        //    34792,
        //    34793,
        //    36336,
        //    34794,
        //    34795,
        //    34796,
        //    34797,
        //    34798,
        //    34799,
        //    34800,
        //    34806,
        //    34807,
        //    34808,
        //    34809,
        //    34810,
        //    34812,
        //    34813,
        //    34814,
        //    34818,
        //    34819,
        //    34822,
        //    34823,
        //    34824,
        //    34825,
        //    34826,
        //    34827,
        //    36335,
        //    34829,
        //    34830,
        //    34831,
        //    34833,
        //    34834,
        //    34835,
        //    34836,
        //    34837,
        //    34838,
        //    34839,
        //    35063,
        //    34841,
        //    34842,
        //    34843,
        //    34844,
        //    34845,
        //    34846,
        //    36333,
        //    36338,
        //    34848,
        //    34849,
        //    34850,
        //    34851,
        //    34852,
        //    34853,
        //    34855,
        //    34856,
        //    34861,
        //    36337,
        //    34862,
        //    34863,
        //    34865,
        //    34866,
        //    34867,
        //    36339,
        //    34783,
        //    34801,
        //    34811,
        //    34820,
        //    34828,
        //    34909,
        //    34854,
        //    34857,
        //    34858,
        //    34864,
        //};

        //public static bool HasChineseTranslation(int id)
        //{
        //    return IDFilterList.Contains(id);
        //}

        public static bool HasChineseTranslation(string name)
        {
            return Translations.ProdCNames.ContainsKey(Translator.RemoveENameSuffix(name).ToLower());
        }

        public static decimal? GetLeverage(ProdDef src)
        {
            switch (src.AssetClass)
            {
                 case "Currencies":
                     return 20;
                case "Commodities":
                    return 10;
                case "Stock Indices":
                    return 10;
                case "Single Stocks":
                    return 5;
                case "Cryptocurrencies":
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}