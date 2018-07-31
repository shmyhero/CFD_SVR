using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CFD_COMMON.Localization
{
    public class Translator
    {
        public const string CULTURE_SYSTEM_DEFAULT = "zh-CN";
        public const string CULTURE_zhCN = "zh-CN";
        public const string CULTURE_en = "en";

        public static bool IsChineseCulture(string cultureName)
        {
            var lower = cultureName.ToLower();
            return lower == "cn" || lower.StartsWith("zh");
        }

        public static bool IsEnglishCulture(string cultureName)
        {
            return cultureName.StartsWith("en");
        }

        public static readonly string[] CULTURE_LIST_Chinese = { "cn", "zh-CN", null };
        public static readonly string[] CULTURE_LIST_English = { "en" };

        public static List<string> GetCultureNamesForSQLLookupByThreadCulture()
        {
            //List<string> languages = new List<string>();
            if (IsEnglishCulture(Thread.CurrentThread.CurrentUICulture.Name))
            {
                return CULTURE_LIST_English.ToList();
            }

            return CULTURE_LIST_Chinese.ToList();
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static string Translate(TransKey transKey)
        {
            if (Translations.Values.ContainsKey(transKey))
                return Translations.Values[transKey];
            else
                return transKey.ToString();
        }

        public static string GetProductNameByThreadCulture(string name)
        {
            var str = RemoveENameSuffix(name);

            if (IsEnglishCulture(Thread.CurrentThread.CurrentUICulture.Name))
                return str;

            var lower = str.ToLower();

            //if (lower.StartsWith("china 50 "))
            //    return "新华富时A50";
            //return str.Replace("China 50 ","新华富时A50 ");
            //if (lower.StartsWith("japan 225 "))
            //    return "日经225";
            //return str.Replace("Japan 225 ", "日经225 ");

            if (Translations.ProdCNames.ContainsKey(lower))
                return Translations.ProdCNames[lower];
            else
                return str;
            //var strLower = str.ToLower();
            //var first = Translations.ProdCNamesList.FirstOrDefault(o => o.Key.ToLower() == strLower);
            //if (first.Key != null)
            //    return first.Value;
            //else
            //    return str;
        }

        public static string GetCName(string name)
        {
            var str = RemoveENameSuffix(name);

           var lower = str.ToLower();

            //if (lower.StartsWith("china 50 "))
            //    return "新华富时A50";
            //return str.Replace("China 50 ","新华富时A50 ");
            //if (lower.StartsWith("japan 225 "))
            //    return "日经225";
            //return str.Replace("Japan 225 ", "日经225 ");

            if (Translations.ProdCNames.ContainsKey(lower))
                return Translations.ProdCNames[lower];
            else
                return str;
            //var strLower = str.ToLower();
            //var first = Translations.ProdCNamesList.FirstOrDefault(o => o.Key.ToLower() == strLower);
            //if (first.Key != null)
            //    return first.Value;
            //else
            //    return str;
        }

        public static string RemoveENameSuffix(string name)
        {
            return name.Replace(" CFD", String.Empty)
                .Replace(" TradeHero", String.Empty)
                .Replace(" Mini", String.Empty)
                .Replace(" Outright", String.Empty)
                .Replace(" Spot", String.Empty)
                .Replace(" (1 EUR Contract)",string.Empty);//;
        }

        public static string AyondoOrderRejectMessageTranslate(string ayondoText)
        {
            if (ayondoText == "Order Delete: Not Sufficient Funds")
                return "剩余资金不足";
            if (ayondoText == "Order Delete: NOLIQ")
                return "商品流动性不足，请稍后再试";
            if (ayondoText == "Server detected error: Above maximum lotsize")
                return "高于最大下单金额";
            if (ayondoText == "Server detected error: Below minimum lotsize")
                return "低于最小下单金额";
            if (ayondoText == "Server detected error: Leverage is above the maximum for this product")
                return "杠杆高于最大限制";
            if (ayondoText == "Server detected error: Trading not permitted outside of market hours")
                return "闭市时间不能交易";
            if (ayondoText == "Server detected error: This product can currently only be traded via the phone. Please call the dealing desk.")
                return "该产品暂时无法交易";
            if (ayondoText == "Server detected error: The target position is pending close")
                return "该仓位正在关闭中";
            if (ayondoText == "No Position")
                return "仓位不存在";
            if (ayondoText == "Server detected error: Not shortable")
                return "该商品无法做空";

            if (ayondoText == "No such order: StopLoss level within minimum stop distance")
                return "止损价与当前价间距过小";
            if (ayondoText == "No such order: Invalid stop level")
                return "不正确的止损价";

            if (ayondoText == "No such order: Invalid profit level")
                return "不正确的止盈价";
            if (ayondoText == "Request Failed")
                return "操作未成功";

            //gekko/ayondo/tradehubcompany close (not market close)
            if (ayondoText == "No such order: Trading not permitted outside of Gekko business hours")
                return "非交易日无法更改设置";

            CFDGlobal.LogWarning("Cannot find OrderReject translate for: [" + ayondoText + "]");
            return ayondoText;
        }

        public static string AyondoMDSTransferErrorMessageTranslate(string ayondoText)
        {
            if (ayondoText == "Invalid Transfer Request: ca.txio.mds.utility.MDException: Insufficient Funds")
                return "资金不足";

            CFDGlobal.LogWarning("Cannot find MDSTransferError translate for: [" + ayondoText + "]");
            return ayondoText;
        }
    }
}