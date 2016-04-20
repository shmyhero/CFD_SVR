using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using AyondoTrade.FaultModel;
using QuickFix.Fields;
using QuickFix.FIX44;

namespace AyondoTrade
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "AyondoTradeService" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select AyondoTradeService.svc or AyondoTradeService.svc.cs at the Solution Explorer and start debugging.
    public class AyondoTradeService : IAyondoTradeService
    {
        public static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(20);
        public static readonly int SCAN_WAIT_MILLI_SECOND = 500;

        public string Test(string text)
        {
            //CFDGlobal.LogLine("host service thread id " + Thread.CurrentThread.ManagedThreadId.ToString());
            return "You entered: " + text;
        }

        public Model.PositionReport NewOrder(string username, string password, int securityId, bool isLong, decimal orderQty,
            decimal? leverage = null, decimal? stopPx = null, string nettingPositionId = null)
        {
            string account = GetAccount(username, password);

            PositionReport report;
            try
            {
                report = NewOrderSingle(account, securityId, isLong, orderQty,
                    leverage: leverage, stopPx: stopPx, nettingPositionId: nettingPositionId);
            }
            catch (BusinettMessageRejectException) //get reject
            {
                //maybe user is not logged in, try to login ONCE
                account = Login(username, password);

                //get data again
                report = NewOrderSingle(account, securityId, isLong, orderQty,
                    leverage: leverage, stopPx: stopPx, nettingPositionId: nettingPositionId);
            }

            var result = PositionReportMapping(report);
            return result;
        }

        public decimal GetBalance(string username, string password)
        {
            string account = GetAccount(username, password);

            decimal balance;
            try
            {
                balance = GetMDS6Balance(account);
            }
            catch (BusinettMessageRejectException) //get reject
            {
                //maybe user is not logged in, try to login ONCE
                account = Login(username, password);

                //get data again
                balance = GetMDS6Balance(account);
            }

            return balance;
        }

        public IList<Model.PositionReport> GetPositionReport(string username, string password)
        {
            string account = GetAccount(username, password);

            IList<PositionReport> result;
            try
            {
                result = GetPositionReport(account);
            }
            catch (BusinettMessageRejectException) //get reject
            {
                //maybe user is not logged in, try to login ONCE
                account = Login(username, password);

                //get data again
                result = GetPositionReport(account);
            }

            //mapping FIX message model --> WCF model
            var positionReports = result.Select(PositionReportMapping).ToList();

            return positionReports;
        }

        private Model.PositionReport PositionReportMapping(PositionReport report)
        {
            var noPositionsGroup = new PositionMaintenanceRequest.NoPositionsGroup();
            report.GetGroup(1, noPositionsGroup);

            return new Model.PositionReport
            {
                PosMaintRptID = report.PosMaintRptID.Obj,
                SecurityID = report.SecurityID.Obj,
                SettlPrice = report.SettlPrice.Obj,

                //CreateTime = report.ClearingBusinessDate.Obj,
                CreateTime =
                    DateTime.ParseExact(report.ClearingBusinessDate.Obj, "yyyy-MM-dd HH:mm:ss.FFF", CultureInfo.CurrentCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                ShortQty = noPositionsGroup.Any(o => o.Key == Tags.ShortQty) ? noPositionsGroup.ShortQty.Obj : (decimal?) null,
                LongQty = noPositionsGroup.Any(o => o.Key == Tags.LongQty) ? noPositionsGroup.LongQty.Obj : (decimal?) null,
                StopOID = report.Any(o => o.Key == Global.FixApp.TAG_StopOID) ? report.GetString(Global.FixApp.TAG_StopOID) : null,
                TakeOID = report.Any(o => o.Key == Global.FixApp.TAG_TakeOID) ? report.GetString(Global.FixApp.TAG_TakeOID) : null,
                StopPx = report.Any(o => o.Key == Tags.StopPx) ? report.GetDecimal(Tags.StopPx) : (decimal?) null,
                TakePx = report.Any(o => o.Key == Global.FixApp.TAG_TakePx) ? report.GetDecimal(Global.FixApp.TAG_TakePx) : (decimal?) null,
                PL = report.GetDecimal(Global.FixApp.TAG_MDS_PL)
            };
        }

        private static string GetAccount(string username, string password)
        {
            string account;
//user in online list?
            if (Global.FixApp.OnlineUsernameAccounts.ContainsKey(username))
            {
                account = Global.FixApp.OnlineUsernameAccounts[username];
            }
            else
            {
                account = Login(username, password);
            }
            return account;
        }

        private static IList<PositionReport> GetPositionReport(string account)
        {
            var reqId = Global.FixApp.RequestForPositions(account);

            RequestForPositionsAck ack = null;
            IList<PositionReport> result = null;
            var dtPositionReport = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);

                //RequestForPositionsAck?
                if (Global.FixApp.RequestForPositionsAcks.ContainsKey(reqId))
                {
                    var tryGetValue = Global.FixApp.RequestForPositionsAcks.TryGetValue(reqId, out ack);

                    if (!tryGetValue) continue;

                    if (ack.TotalNumPosReports.Obj == 0) //have no position
                    {
                        result = new List<PositionReport>();
                        break;
                    }

                    if (Global.FixApp.PositionReports.ContainsKey(reqId))
                    {
                        tryGetValue = Global.FixApp.PositionReports.TryGetValue(reqId, out result);

                        if (!tryGetValue) continue;

                        if (result.Count == ack.TotalNumPosReports.Obj) //all reports fetched
                            break;
                    }
                }

                //BusinessMessageReject? e.g. not logged in
                if (Global.FixApp.BusinessMessageRejects.ContainsKey(reqId))
                {
                    BusinessMessageReject msg;
                    var tryGetValue = Global.FixApp.BusinessMessageRejects.TryGetValue(reqId, out msg);
                    throw new BusinettMessageRejectException(msg == null ? "" : msg.Text.Obj);
                }
            } while (DateTime.UtcNow - dtPositionReport <= TIMEOUT); // timeout

            if (ack == null || ack.TotalNumPosReports.Obj != 0 && result == null)
                throw new Exception("fail getting position report");

            if (result.Count != 0 && result.Count != result[0].TotalNumPosReports.Obj)
                throw new Exception("timeout getting position report. " + result.Count + "/" + result[0].TotalNumPosReports.Obj);

            return result;
        }

        private static PositionReport NewOrderSingle(string account, int securityId, bool isLong, decimal orderQty,
            decimal? leverage = null, decimal? stopPx = null, string nettingPositionId = null)
        {
            //send NewOrderSingle message
            var reqId = Global.FixApp.NewOrderSingle(account, securityId.ToString(), isLong ? Side.BUY : Side.SELL, orderQty, OrdType.MARKET,
                leverage: leverage, stopPx: stopPx, nettingPositionId: nettingPositionId);

            //wait/get response message(s)
            IList<PositionReport> reports = null;
            PositionReport report = null;
            ExecutionReport executionReport = null;
            var dt = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);

                //check execution report
                if (Global.FixApp.RejectedExecutionReports.ContainsKey(reqId))
                {
                    var tryGetValue = Global.FixApp.RejectedExecutionReports.TryGetValue(reqId, out executionReport);

                    if (!tryGetValue) continue;

                    if (executionReport != null)
                    {
                        //throw new Exception("Order rejected. Message: " + executionReport.Text.Obj);
                        var fault = new OrderRejectedFault();
                        fault.Text = executionReport.Text.Obj;
                        throw new FaultException<OrderRejectedFault>(fault);
                    }
                }

                //check position report
                if (Global.FixApp.OrderPositionReports.ContainsKey(reqId))
                {
                    var tryGetValue = Global.FixApp.OrderPositionReports.TryGetValue(reqId, out reports);

                    if (!tryGetValue) continue;

                    if (nettingPositionId != null) //closing position: a 'Text=Position DELETE by MarketOrder' should be received
                    {
                        report = reports.FirstOrDefault(o => o.Text.Obj == "Position DELETE by MarketOrder");
                        if (report != null)
                            break;
                    }
                    else //open new position: ONLY ONE position report will be received
                    {
                        report = reports.FirstOrDefault();
                        break;
                    }
                }

                //BusinessMessageReject? e.g. not logged in
                if (Global.FixApp.BusinessMessageRejects.ContainsKey(reqId))
                {
                    BusinessMessageReject msg;
                    var tryGetValue = Global.FixApp.BusinessMessageRejects.TryGetValue(reqId, out msg);
                    throw new BusinettMessageRejectException(msg == null ? "" : msg.Text.Obj);
                }
            } while (DateTime.UtcNow - dt <= TIMEOUT);

            if (report == null)
                throw new Exception("fail getting order result");

            return report;
        }

        public decimal GetMDS6Balance(string account)
        {
            var reqId = Global.FixApp.MDS5BalanceRequest(account);
            decimal balance = -1;
            var dt = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);

                if (Global.FixApp.Balances.ContainsKey(reqId))
                {
                    var tryGetValue = Global.FixApp.Balances.TryGetValue(reqId, out balance);

                    if (!tryGetValue) continue;

                    break;
                }

                //BusinessMessageReject? e.g. not logged in
                if (Global.FixApp.BusinessMessageRejects.ContainsKey(reqId))
                {
                    BusinessMessageReject msg;
                    var tryGetValue = Global.FixApp.BusinessMessageRejects.TryGetValue(reqId, out msg);
                    throw new BusinettMessageRejectException(msg == null ? "" : msg.Text.Obj);
                }
            } while (DateTime.UtcNow - dt <= TIMEOUT); // timeout

            if (balance == -1)
                throw new Exception("fail getting balance");

            return balance;
        }

        private static string Login(string username, string password)
        {
            string account = null;

            var guid = Global.FixApp.LogOn(username, password);

            var dtLogon = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);
                if (Global.FixApp.OnlineUsernameAccounts.ContainsKey(username))
                {
                    account = Global.FixApp.OnlineUsernameAccounts[username];
                    break;
                }
            } while (DateTime.UtcNow - dtLogon <= TIMEOUT); // timeout

            if (string.IsNullOrEmpty(account))
                throw new Exception("fix log on time out");

            return account;
        }
    }

    internal class BusinettMessageRejectException : Exception
    {
        public BusinettMessageRejectException(string s) : base(s)
        {
        }
    }
}