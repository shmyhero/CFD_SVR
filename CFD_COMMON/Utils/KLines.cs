using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class KLines
    {
        private const int CLEAR_HISTORY_WHEN_SIZE_5m = 12 * 24 * 10; //xx days' most possible count
        private const int CLEAR_HISTORY_TO_SIZE_5m = 12 * 24 * 7; //xx days' most possible count

        private const int CLEAR_HISTORY_WHEN_SIZE_1d = 22*12;
        private const int CLEAR_HISTORY_TO_SIZE_1d = 22*6;

        public static string GetKLineListNamePrefix(KLineSize kLineSize)
        {
            switch (kLineSize)
            {
                case KLineSize.FiveMinutes:
                    return "kline5m:";
                    break;
                case KLineSize.Day:
                    return "kline1d:";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kLineSize), kLineSize, null);
            }
        }

        public static DateTime GetKLineTime(DateTime quoteTime, KLineSize klineSize, string quoteSymbol=null)
        {
            switch (klineSize)
            {
                case KLineSize.FiveMinutes:
                    return DateTimes.GetStartTimeEvery5Minutes(quoteTime);
                    break;
                case KLineSize.Day:
                    return quoteTime.Date;//todo: get date by timezone?
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(klineSize), klineSize, null);
            }
        }

        public static int GetClearWhenSize(KLineSize klineSize)
        {
            switch (klineSize)
            {
                case KLineSize.FiveMinutes:
                    return CLEAR_HISTORY_WHEN_SIZE_5m;
                    break;
                case KLineSize.Day:
                    return CLEAR_HISTORY_WHEN_SIZE_1d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(klineSize), klineSize, null);
            }
        }

        public static int GetClearToSize(KLineSize klineSize)
        {
            switch (klineSize)
            {
                case KLineSize.FiveMinutes:
                    return CLEAR_HISTORY_TO_SIZE_5m;
                    break;
                case KLineSize.Day:
                    return CLEAR_HISTORY_TO_SIZE_1d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(klineSize), klineSize, null);
            }
        }
    }

    public enum KLineSize
    {
        FiveMinutes,
        Day
    }
}
