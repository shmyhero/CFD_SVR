using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON;
using ServiceStack.Text;

namespace CFD_JOBS.Ayondo
{
    public class AyondoTradeHistory
    {
        private DateTime lastEndTime;

        public static void Run()
        {
            var dtEnd = DateTime.UtcNow;
            var dtStart = dtEnd.AddDays(-1);

            var tsStart = dtStart.ToUnixTimeMs();
            var tsEnd = dtEnd.ToUnixTimeMs();

            var webClient = new WebClient();
            var downloadString = webClient.DownloadString(
                "http://thvm-prod4.cloudapp.net:14535/demo/reports/tradehero/cn/tradehistory?start=" 
                + tsStart + "&end=" +tsEnd);

            var lines = downloadString.Split(new []{"\r\n"},StringSplitOptions.None);

            var autoClosedLines = lines.Skip(1)//skip headers
                .Select(o=>o.Split(','))
                .Where(o => o.Last() == "NA")//DeviceType == NA
                .ToList();

            //PositionID,TradeID,AccountID,FirstName,LastName,
            //TradeTime,ProductID,ProductName,Direction,Trade Size,
            //Trade Price,Realized P&L,GUID,StopLoss,TakeProfit,
            //CreationTime,UpdateType,DeviceType
            foreach (var arr in autoClosedLines)
            {
                var posId = arr[0];
                var account = arr[2];
                var time = DateTime.ParseExact(arr[5], CFDGlobal.AYONDO_DATETIME_MASK, CultureInfo.CurrentCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                var secIdD = Convert.ToInt32(arr[6]);
                var price = Convert.ToDecimal(arr[10]);
                var pl = Convert.ToDecimal(arr[11]);
                var updateType = arr[16];
            }
        }
    }
}
