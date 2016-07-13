using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class Ticks
    {
        public static string GetTickListNamePrefix(TickSize tickSize)
        {
            switch (tickSize)
            {
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
    }

    public enum TickSize
    {
        OneMinute,
        TenMinute,
        OneHour,
    }
}
