using System;
using System.Collections.Generic;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using CFD_COMMON;
using CFD_JOBS.Ayondo;
using QuickFix;
using QuickFix.FIX44;
using QuickFix.Transport;

namespace AyondoTrade
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "AyondoTradeService" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select AyondoTradeService.svc or AyondoTradeService.svc.cs at the Solution Explorer and start debugging.
    public class AyondoTradeService : IAyondoTradeService
    {
        public string Test(string text)
        {
            //CFDGlobal.LogLine("host service thread id " + Thread.CurrentThread.ManagedThreadId.ToString());
            return "You entered: " + text;
        }

        public IList<PositionReport> GetPositionReport(string username, string password)
        {
             string account = null;

            if (FIXApp.Instance.OnlineUsernameAccounts.ContainsKey(username))
            {
                account = FIXApp.Instance.OnlineUsernameAccounts[username];
            }
            else
            {
                var guid = FIXApp.Instance.LogOn(username, password);

                var dtLogon = DateTime.UtcNow;
                do
                {
                    Thread.Sleep(1000);
                    if (FIXApp.Instance.OnlineUsernameAccounts.ContainsKey(username))
                    {
                        account = FIXApp.Instance.OnlineUsernameAccounts[username];
                        break;
                    }
                } while (DateTime.UtcNow - dtLogon <= TimeSpan.FromSeconds(20)); //20 second timeout
                if (string.IsNullOrEmpty(account))
                    throw new Exception("fix log on time out");
            }

            var reqId = FIXApp.Instance.PositionReport(account);

            RequestForPositionsAck ack = null;
            IList<PositionReport> result = null;
            var dtPositionReport = DateTime.UtcNow;
            do
            {
                Thread.Sleep(1000);
                if (FIXApp.Instance.RequestForPositionsAcks.ContainsKey(reqId))
                {
                    var tryGetValue = FIXApp.Instance.RequestForPositionsAcks.TryGetValue(reqId, out ack);

                    if (!tryGetValue) continue;

                    if (ack.TotalNumPosReports.Obj == 0)//have no position. RETURN
                       return new List<PositionReport>();

                    if (FIXApp.Instance.PositionReports.ContainsKey(reqId))
                    {
                        tryGetValue = FIXApp.Instance.PositionReports.TryGetValue(reqId, out result);

                        if (!tryGetValue) continue;

                        if (result.Count == ack.TotalNumPosReports.Obj) //all reports fetched
                            break;
                    }
                }
            } while (DateTime.UtcNow - dtPositionReport <= TimeSpan.FromSeconds(20)); //20 second timeout

            if (ack == null || ack.TotalNumPosReports.Obj != 0 && result == null)
                throw new Exception("fail getting position report");

            if (result.Count != result[0].TotalNumPosReports.Obj)
                throw new Exception("unfinished getting position report. " + result.Count + "/" + result[0].TotalNumPosReports.Obj);

            return result;
        }
    }
}