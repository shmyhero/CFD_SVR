using System;
using System.Collections.Generic;
using System.Threading;
using QuickFix.FIX44;

namespace AyondoTrade
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "AyondoTradeService" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select AyondoTradeService.svc or AyondoTradeService.svc.cs at the Solution Explorer and start debugging.
    public class AyondoTradeService : IAyondoTradeService
    {
        public AyondoTradeService()
        {
        }

        public string Test(string text)
        {
            //CFDGlobal.LogLine("host service thread id " + Thread.CurrentThread.ManagedThreadId.ToString());
            return "You entered: " + text;
        }

        public IList<PositionReport> GetPositionReport(string username, string password)
        {
            string account = null;

            if (WebRole.FIXApp.OnlineUsernameAccounts.ContainsKey(username))
            {
                account = WebRole.FIXApp.OnlineUsernameAccounts[username];
            }
            else
            {
                var guid = WebRole.FIXApp.LogOn(username, password);

                var dtLogon = DateTime.UtcNow;
                do
                {
                    Thread.Sleep(1000);
                    if (WebRole.FIXApp.OnlineUsernameAccounts.ContainsKey(username))
                    {
                        account = WebRole.FIXApp.OnlineUsernameAccounts[username];
                        break;
                    }
                } while (DateTime.UtcNow - dtLogon <= TimeSpan.FromSeconds(20)); //20 second timeout
                if (string.IsNullOrEmpty(account))
                    throw new Exception("fix log on time out");
            }

            var reqId = WebRole.FIXApp.PositionReport(account);

            IList<PositionReport> result = null;
            var dtPositionReport = DateTime.UtcNow;
            do
            {
                Thread.Sleep(1000);
                if (WebRole.FIXApp.PositionReports.ContainsKey(reqId))
                {
                    var tryGetValue = WebRole.FIXApp.PositionReports.TryGetValue(reqId, out result);

                    if (!tryGetValue) continue;

                    if (result.Count == result[0].TotalNumPosReports.Obj) //all reports fetched
                        break;
                }
            } while (DateTime.UtcNow - dtPositionReport <= TimeSpan.FromSeconds(20)); //20 second timeout

            if (result == null)
                throw new Exception("fail getting position report");

            if(result.Count!=result[0].TotalNumPosReports.Obj)
                throw new Exception("unfinished getting position report. " + result.Count + "/" + result[0].TotalNumPosReports.Obj);

            return result;
        }
    }
}