using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class DateTimes
    {
        public static DateTime GetChinaDateTimeNow()
        {
            var dt = DateTime.UtcNow.AddHours(8);
            //dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);//change kind from UTC to local
            return dt;
        }

        public static bool IsEqualDownToMinute(DateTime t1, DateTime t2)
        {
            return t1.Minute == t2.Minute
                   && t1.Hour == t2.Hour
                   && t1.Day == t2.Day
                   && t1.Month == t2.Month
                   && t1.Year == t2.Year;
        }

        public static bool IsEqualDownTo10Minute(DateTime t1, DateTime t2)
        {
            return t1.Minute/10 == t2.Minute/10
                    && t1.Hour == t2.Hour
                    && t1.Day == t2.Day
                    && t1.Month == t2.Month
                    && t1.Year == t2.Year;
        }

        public static bool IsEqualDownToHour(DateTime t1, DateTime t2)
        {
            return t1.Hour == t2.Hour
                   && t1.Day == t2.Day
                   && t1.Month == t2.Month
                   && t1.Year == t2.Year;
        }

        public static DateTime GetHistoryQueryStartTime(DateTime endTime)
        {
            return endTime.AddDays(-10);
        }
    }
}
