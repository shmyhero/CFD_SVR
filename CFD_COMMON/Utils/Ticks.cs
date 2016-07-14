using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class Ticks
    {
        private const int CLEAR_HISTORY_WHEN_SIZE_1mTick = 60 * 24 * 10; //xx days' most possible count
        private const int CLEAR_HISTORY_TO_SIZE_1mTick = 60 * 24 * 7; //xx days' most possible count

        private const int CLEAR_HISTORY_WHEN_SIZE_10mTick = 6 * 24 * 15;
        private const int CLEAR_HISTORY_TO_SIZE_10mTick = 6 * 24 * 10;

        private const int CLEAR_HISTORY_WHEN_SIZE_1hTick = 24 * 60;
        private const int CLEAR_HISTORY_TO_SIZE_1hTick = 24 * 40;

        private const int CLEAR_HISTORY_WHEN_SIZE_RawTick =20000;
        private const int CLEAR_HISTORY_TO_SIZE_RawTick = 10000;

        public static string GetTickListNamePrefix(TickSize tickSize)
        {
            switch (tickSize)
            {
                case TickSize.Raw:
                    return "tickRaw:";
                    break;
                case TickSize.OneMinute:
                    return "tick:";
                    break;
                case TickSize.TenMinute:
                    return "tick10m:";
                    break;
                case TickSize.OneHour:
                    return "tick1h:";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("tickSize", tickSize, null);
            }
        }

        public static bool IsTickEqual(DateTime t1, DateTime t2, TickSize tickSize)
        {
            switch (tickSize)
            {
                case TickSize.OneMinute:
                    return DateTimes.IsEqualDownToMinute(t1, t2);
                    break;
                case TickSize.TenMinute:
                    return DateTimes.IsEqualDownTo10Minute(t1, t2);
                    break;
                case TickSize.OneHour:
                    return DateTimes.IsEqualDownToHour(t1, t2);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("tickSize", tickSize, null);
            }
        }

        public static int GetClearWhenSize(TickSize tickSize)
        {
            switch (tickSize)
            {
                case TickSize.Raw:
                    return CLEAR_HISTORY_WHEN_SIZE_RawTick;
                    break;
                case TickSize.OneMinute:
                    return CLEAR_HISTORY_WHEN_SIZE_1mTick;
                    break;
                case TickSize.TenMinute:
                    return CLEAR_HISTORY_WHEN_SIZE_10mTick;
                    break;
                case TickSize.OneHour:
                    return CLEAR_HISTORY_WHEN_SIZE_1hTick;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("tickSize", tickSize, null);
            }
        }

        public static int GetClearToSize(TickSize tickSize)
        {
            switch (tickSize)
            {
                case TickSize.Raw:
                    return CLEAR_HISTORY_TO_SIZE_RawTick;
                    break;
                case TickSize.OneMinute:
                    return CLEAR_HISTORY_TO_SIZE_1mTick;
                    break;
                case TickSize.TenMinute:
                    return CLEAR_HISTORY_TO_SIZE_10mTick;
                    break;
                case TickSize.OneHour:
                    return CLEAR_HISTORY_TO_SIZE_1hTick;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("tickSize", tickSize, null);
            }
        }
    }

    public enum TickSize
    {
        Raw,
        OneMinute,
        TenMinute,
        OneHour,
    }
}
