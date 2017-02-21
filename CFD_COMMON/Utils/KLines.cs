using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
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
            DescriptionAttribute attr = GetAttribute<DescriptionAttribute>(kLineSize);
            if (attr == null)
            {
                return string.Empty;
            }

            return attr.Description;
        }

        public static DateTime GetKLineTime(DateTime quoteTime, KLineSize klineSize, string quoteSymbol=null)
        {
            PeriodAttribute attr = GetAttribute<PeriodAttribute>(klineSize);
            if (attr == null) //对于KLineSize.Day这种没有PeriodAttribute的
            {
                return quoteTime.Date;
            }
            int period = attr.Period;

            return DateTimes.GetStartTime(quoteTime, period);
        }

        public static int GetClearWhenSize(KLineSize klineSize)
        {
            ClearWhenAttribute attr = GetAttribute<ClearWhenAttribute>(klineSize);
            return attr.Size;
        }

        public static int GetClearToSize(KLineSize klineSize)
        {
            ClearToAttribute attr = GetAttribute<ClearToAttribute>(klineSize);
            return attr.Size;
        }

        private static T GetAttribute<T>(KLineSize klineSize) where T:Attribute
        {
            var type = klineSize.GetType();
            FieldInfo field = type.GetField(Enum.GetName(type, klineSize));
            T attr = Attribute.GetCustomAttribute(field, typeof(T)) as T;

            return attr;
        }
    }

    public enum KLineSize
    {
        [Description("kile1m:")]
        [Period(1)]
        [ClearWhen(60 * 8)] //1分钟K线，需要显示4小时
        [ClearTo(60 * 4)]
        OneMinute,

        [Description("kile5m:")]
        [Period(5)]
        [ClearWhen(12 * 24 * 10)] //5分钟K线，需要显示2个交易日
        [ClearTo(12 * 24 * 7)]
        FiveMinutes,

        [Description("kile15m:")]
        [Period(15)]
        [ClearWhen(4 * 24 * 6)] //15分钟K线，需要显示3个交易日
        [ClearTo(4 * 24 * 3)]
        FifteenMinutes,

        [Description("kile60m:")]
        [Period(60)]
        [ClearWhen(1 * 24 * 24)] //60分钟K线，需要显示12个交易日
        [ClearTo(1 * 24 * 12)]
        SixtyMinutes,

        [Description("kile1d:")]
        [ClearWhen(22 * 12)] //60分钟K线，需要显示12个交易日
        [ClearTo(22 * 6)]
        Day
    }

    public class PeriodAttribute : Attribute
    {
        private int period;
        public int Period
        {
            get
            {
                return period;
            }
        }
        public PeriodAttribute(int period)
        {
            this.period = period;
        }
    }

    public class ClearWhenAttribute : Attribute
    {
        private int size;
        public int Size
        {
            get
            {
                return size;
            }
        }
        public ClearWhenAttribute(int size)
        {
            this.size = size;
        }
    }

    public class ClearToAttribute : Attribute
    {
        private int size;
        public int Size
        {
            get
            {
                return size;
            }
        }
        public ClearToAttribute(int size)
        {
            this.size = size;
        }
    }
}
