using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using CFD_COMMON;
using CFD_JOBS.Ayondo;
using QuickFix;
using QuickFix.Fields;
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

        public IList<Model.PositionReport> GetPositionReport(string username, string password)
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
                        return new List<Model.PositionReport>();

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

            var positionReports = result.Select(delegate(PositionReport report)
            {
                var noPositionsGroup = new PositionMaintenanceRequest.NoPositionsGroup();
                report.GetGroup(1, noPositionsGroup);

                return new Model.PositionReport
                {
                    PosMaintRptID = report.PosMaintRptID.Obj,
                    SecurityID = report.SecurityID.Obj,
                    SettlPrice = report.SettlPrice.Obj,

                    ShortQty = noPositionsGroup.Any(o => o.Key == Tags.ShortQty) ? noPositionsGroup.ShortQty.Obj : (decimal?)null,
                    LongQty = noPositionsGroup.Any(o=>o.Key==Tags.LongQty)?noPositionsGroup.LongQty.Obj:(decimal?) null,

                    StopOID = report.Any(o => o.Key == FIXApp.Instance.StopOID) ? report.GetString(FIXApp.Instance.StopOID) : null,
                    TakeOID = report.Any(o => o.Key == FIXApp.Instance.TakeOID) ? report.GetString(FIXApp.Instance.TakeOID) : null,
                    StopPx = report.Any(o => o.Key == Tags.StopPx) ? report.GetDecimal(Tags.StopPx) : (decimal?)null,
                    TakePx = report.Any(o => o.Key == FIXApp.Instance.TakePx) ? report.GetDecimal(FIXApp.Instance.TakePx) : (decimal?)null
                };
            }).ToList();

            return positionReports;
        }
    }
}