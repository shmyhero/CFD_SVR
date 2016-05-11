namespace CFD_COMMON.Localization
{
    public class Translator
    {
        public static string Translate(TransKey transKey)
        {
            if (Translations.Values.ContainsKey(transKey))
                return Translations.Values[transKey];
            else
                return transKey.ToString();
        }

        public static string AyondoOrderRejectMessageTranslate(string ayondoText)
        {
            if (ayondoText == "Order Delete: Not Sufficient Funds")
                return "剩余资金不足";
            if (ayondoText == "Server detected error: Above maximum lotsize")
                return "高于最大下单金额";
            if (ayondoText == "Server detected error: Below minimum lotsize")
                return "低于最小下单金额";
            if (ayondoText == "Server detected error: Leverage is above the maximum for this product")
                return "杠杆高于最大限制";
            if (ayondoText == "Server detected error: Trading not permitted outside of market hours")
                return "闭市时间不能交易";

            if (ayondoText == "No such order: StopLoss level within minimum stop distance")
                return "止损价与当前价间距过小";

            CFDGlobal.LogWarning("Cannot find ayondo translate for: [" + ayondoText + "]");
            return ayondoText;
        }
    }
}