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
        public static readonly string FIX_DATETIME_MASK = "yyyy-MM-dd HH:mm:ss.FFF";

        public string Test(string text)
        {
            //CFDGlobal.LogLine("host service thread id " + Thread.CurrentThread.ManagedThreadId.ToString());
            return "You entered: " + text;
        }

        public Model.PositionReport NewOrder(string username, string password, int securityId, bool isLong, decimal orderQty, //char? ordType = null, decimal? price = null,
            decimal? leverage = null, decimal? stopPx = null, string nettingPositionId = null)
        {
            string account = GetAccount(username, password);

            PositionReport report;
            try
            {
                report = SendNewOrderSingleAndWait(account, securityId, isLong, orderQty,
                    leverage: leverage, stopPx: stopPx, nettingPositionId: nettingPositionId);
            }
            catch (UserNotLoggedInException)
            {
                //user is not logged in, try to login ONCE
                account = SendLoginRequestAndWait(username, password);

                //get data again
                report = SendNewOrderSingleAndWait(account, securityId, isLong, orderQty,
                    leverage: leverage, stopPx: stopPx, nettingPositionId: nettingPositionId);
            }

            var result = PositionReportMapping(report);
            return result;
        }

        public Model.PositionReport NewTakeOrder(string username, string password, int securityId, decimal price, string nettingPositionId)
        {
            string account = GetAccount(username, password);

            PositionReport report;
            try
            {
                report = SendNewTakeOrderAndWait(account, securityId, price, nettingPositionId);
            }
            catch (UserNotLoggedInException)
            {
                //user is not logged in, try to login ONCE
                account = SendLoginRequestAndWait(username, password);

                //get data again
                report = SendNewTakeOrderAndWait(account, securityId, price, nettingPositionId);
            }

            var result = PositionReportMapping(report);
            return result;
        }

        public Model.PositionReport ReplaceOrder(string username, string password, int securityId, string orderId, decimal price, string nettingPositionId)
        {
            string account = GetAccount(username, password);

            PositionReport report;
            try
            {
                report = SendReplaceOrderAndWait(account, securityId, orderId, price, nettingPositionId);
            }
            catch (UserNotLoggedInException)
            {
                //user is not logged in, try to login ONCE
                account = SendLoginRequestAndWait(username, password);

                //get data again
                report = SendReplaceOrderAndWait(account, securityId, orderId, price, nettingPositionId);
            }

            var result = PositionReportMapping(report);
            return result;
        }

        public Model.PositionReport CancelOrder(string username, string password, int securityId, string orderId, string nettingPositionId)
        {
            string account = GetAccount(username, password);

            PositionReport report;
            try
            {
                report = SendCancelOrderAndWait(account, securityId, orderId, nettingPositionId);
            }
            catch (UserNotLoggedInException)
            {
                //user is not logged in, try to login ONCE
                account = SendLoginRequestAndWait(username, password);

                //get data again
                report = SendCancelOrderAndWait(account, securityId, orderId, nettingPositionId);
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
                balance = SendBalanceRequestAndWait(account);
            }
            catch (UserNotLoggedInException)
            {
                //user is not logged in, try to login ONCE
                account = SendLoginRequestAndWait(username, password);

                //get data again
                balance = SendBalanceRequestAndWait(account);
            }

            return balance;
        }

        public IList<Model.PositionReport> GetPositionReport(string username, string password)
        {
            string account = GetAccount(username, password);

            IList<PositionReport> result;
            try
            {
                result = SendPositionRequestAndWait(account);
            }
            catch (UserNotLoggedInException)
            {
                //user is not logged in, try to login ONCE
                account = SendLoginRequestAndWait(username, password);

                //get data again
                result = SendPositionRequestAndWait(account);
            }

            //mapping FIX message model --> WCF model
            var positionReports = result.Select(PositionReportMapping).ToList();

            return positionReports;
        }

        public IList<Model.PositionReport> GetPositionHistoryReport(string username, string password, DateTime startTime, DateTime endTime)
        {
            string account = GetAccount(username, password);

            IList<PositionReport> result;
            try
            {
                result = SendPositionHistoryRequestAndWait(account, startTime, endTime);
            }
            catch (UserNotLoggedInException)
            {
                //user is not logged in, try to login ONCE
                account = SendLoginRequestAndWait(username, password);

                //get data again
                result = SendPositionHistoryRequestAndWait(account, startTime, endTime);
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
                    DateTime.ParseExact(report.ClearingBusinessDate.Obj, FIX_DATETIME_MASK, CultureInfo.CurrentCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                ShortQty = noPositionsGroup.Any(o => o.Key == Tags.ShortQty) ? noPositionsGroup.ShortQty.Obj : (decimal?) null,
                LongQty = noPositionsGroup.Any(o => o.Key == Tags.LongQty) ? noPositionsGroup.LongQty.Obj : (decimal?) null,
                StopOID = report.Any(o => o.Key == Global.FixApp.TAG_StopOID) ? report.GetString(Global.FixApp.TAG_StopOID) : null,
                TakeOID = report.Any(o => o.Key == Global.FixApp.TAG_TakeOID) ? report.GetString(Global.FixApp.TAG_TakeOID) : null,
                StopPx = report.Any(o => o.Key == Tags.StopPx) ? report.GetDecimal(Tags.StopPx) : (decimal?) null,
                TakePx = report.Any(o => o.Key == Global.FixApp.TAG_TakePx) ? report.GetDecimal(Global.FixApp.TAG_TakePx) : (decimal?) null,
                PL = report.GetDecimal(Global.FixApp.TAG_MDS_PL),
                UPL = report.Any(o => o.Key == Global.FixApp.TAG_MDS_UPL) ? report.GetDecimal(Global.FixApp.TAG_MDS_UPL) : (decimal?) null,
                Leverage = report.Any(o => o.Key == Global.FixApp.TAG_Leverage) ? report.GetDecimal(Global.FixApp.TAG_Leverage) : (decimal?) null,
            };
        }

        private static string GetAccount(string username, string password)
        {
            string account;
//user in online list?
            if (Global.FixApp.UsernameAccounts.ContainsKey(username))
            {
                account = Global.FixApp.UsernameAccounts[username];
            }
            else
            {
                account = SendLoginRequestAndWait(username, password);
            }
            return account;
        }

        private static IList<PositionReport> SendPositionRequestAndWait(string account)
        {
            var reqId = Global.FixApp.RequestForPositions(account);

            KeyValuePair<DateTime, RequestForPositionsAck> ack = new KeyValuePair<DateTime, RequestForPositionsAck>(DateTime.UtcNow, null);
            IList<KeyValuePair<DateTime, PositionReport>> reports = null;
            var dtPositionReport = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);

                //RequestForPositionsAck?
                if (Global.FixApp.RequestForPositionsAcks.ContainsKey(reqId))
                {
                    var tryGetValue = Global.FixApp.RequestForPositionsAcks.TryGetValue(reqId, out ack);

                    if (!tryGetValue) continue;

                    if (ack.Value.TotalNumPosReports.Obj == 0) //have no position
                    {
                        reports = new List<KeyValuePair<DateTime, PositionReport>>();
                        break;
                    }

                    if (Global.FixApp.PositionReports.ContainsKey(reqId))
                    {
                        tryGetValue = Global.FixApp.PositionReports.TryGetValue(reqId, out reports);

                        if (!tryGetValue) continue;

                        if (reports.Count == ack.Value.TotalNumPosReports.Obj) //all reports fetched
                            break;
                    }
                }

                CheckBusinessMessageReject(reqId);
            } while (DateTime.UtcNow - dtPositionReport <= TIMEOUT); // timeout

            if (ack.Value == null || ack.Value.TotalNumPosReports.Obj != 0 && reports == null)
                throw new Exception("fail getting position report");

            if (reports.Count != 0 && reports.Count != reports[0].Value.TotalNumPosReports.Obj)
                throw new Exception("timeout getting position report. " + reports.Count + "/" + reports[0].Value.TotalNumPosReports.Obj);

            return reports.Select(o => o.Value).ToList();
        }

        private static IList<PositionReport> SendPositionHistoryRequestAndWait(string account, DateTime startTime, DateTime endTime)
        {
            var reqId = Global.FixApp.RequestForPositionHistories(account, startTime, endTime);

            IList<KeyValuePair<DateTime, PositionReport>> reports = null;
            var dtPositionReport = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);

                if (Global.FixApp.PositionReports.ContainsKey(reqId))
                {
                    var tryGetValue = Global.FixApp.PositionReports.TryGetValue(reqId, out reports);

                    if (!tryGetValue) continue;

                    var last = reports.Last();
                    var setSize = Convert.ToInt32(last.Value.GetString(Global.FixApp.TAG_MDS_SetSize));
                    var setIndex = Convert.ToInt32(last.Value.GetString(Global.FixApp.TAG_MDS_SetIndex));

                    if (setIndex == setSize - 1) //all reports fetched
                        break;
                }

                CheckBusinessMessageReject(reqId);////////////////////////////////////////////////////////////////////////////////////////todo
            } while (DateTime.UtcNow - dtPositionReport <= TIMEOUT); // timeout

            if (reports == null)
                throw new Exception("fail getting position report");

            var lastPositionReport = reports.Last();
            var reportCount = Convert.ToInt32(lastPositionReport.Value.GetString(Global.FixApp.TAG_MDS_SetSize));
            var lastReportIndex = Convert.ToInt32(lastPositionReport.Value.GetString(Global.FixApp.TAG_MDS_SetIndex));

            if (lastReportIndex < reportCount - 1)
                throw new Exception("timeout getting position report. count:" + reportCount + " lastReportIndex:" + lastReportIndex);

            return reports.Select(o => o.Value).ToList();
        }

        private static PositionReport SendNewOrderSingleAndWait(string account, int securityId, bool isLong, decimal orderQty,
            //char ordType = OrdType.MARKET, decimal? price = null,
            decimal? leverage = null, decimal? stopPx = null, string nettingPositionId = null)
        {
            //send NewOrderSingle message
            var reqId = Global.FixApp.NewOrderSingle(account, securityId.ToString(), OrdType.MARKET, isLong ? Side.BUY : Side.SELL, orderQty,
                leverage: leverage, stopPx: stopPx, nettingPositionId: nettingPositionId);

            //wait/get response message(s)
            IList<KeyValuePair<DateTime, PositionReport>> reports = null;
            PositionReport report = null;
            KeyValuePair<DateTime, ExecutionReport> executionReport = new KeyValuePair<DateTime, ExecutionReport>(DateTime.UtcNow, null);
            var dt = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);

                //check execution report
                CheckRejectedExecutionReport(reqId);

                //check position report
                if (Global.FixApp.OrderPositionReports.ContainsKey(reqId))
                {
                    var tryGetValue = Global.FixApp.OrderPositionReports.TryGetValue(reqId, out reports);

                    if (!tryGetValue) continue;

                    if (nettingPositionId != null) //closing position: a 'Text=Position DELETE by MarketOrder' should be received
                    {
                        report = reports.FirstOrDefault(o => o.Value.Text.Obj == "Position DELETE by MarketOrder").Value;
                        if (report != null)
                            break;
                    }
                    else //open new position: ONLY ONE position report will be received
                    {
                        report = reports.FirstOrDefault().Value;
                        break;
                    }
                }

                CheckBusinessMessageReject(reqId);
            } while (DateTime.UtcNow - dt <= TIMEOUT);

            if (report == null)
                throw new Exception("fail getting order result");

            return report;
        }

        private PositionReport SendNewTakeOrderAndWait(string account, int securityId, decimal price, string nettingPositionId)
        {
            var dtSent = DateTime.UtcNow;
            var reqId = Global.FixApp.NewOrderSingle(account, securityId.ToString(), '4', price: price, nettingPositionId: nettingPositionId);

            //wait/get response message(s)
            IList<KeyValuePair<DateTime, PositionReport>> reports = null;
            PositionReport report = null;
            var dt = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);

                //check execution report
                CheckRejectedExecutionReport(reqId);

                //check position report
                if (Global.FixApp.StopTakePositionReports.ContainsKey(nettingPositionId))
                {
                    var tryGetValue = Global.FixApp.StopTakePositionReports.TryGetValue(nettingPositionId, out reports);

                    if (!tryGetValue) continue;

                    if (reports != null && reports.Count > 0)
                    {
                        var lastReport = reports[reports.Count - 1];
                        if (lastReport.Key > dtSent)
                        {
                            report = lastReport.Value;
                            break;
                        }
                    }
                }

                CheckBusinessMessageReject(reqId);
            } while (DateTime.UtcNow - dt <= TIMEOUT);

            if (report == null)
                throw new Exception("fail getting new take order result (position report)");

            return report;
        }

        private PositionReport SendReplaceOrderAndWait(string account, int securityId, string orderId, decimal price, string nettingPositionId)
        {
            //send NewOrderSingle message
            var dtSent = DateTime.UtcNow;
            var reqId = Global.FixApp.OrderCancelReplaceRequest(account, securityId.ToString(), orderId, price);

            //wait/get response message(s)
            IList<KeyValuePair<DateTime, PositionReport>> reports = null;
            PositionReport report = null;
            KeyValuePair<DateTime, ExecutionReport> executionReport = new KeyValuePair<DateTime, ExecutionReport>(DateTime.UtcNow, null);
            var dt = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);

                //check rejected execution report
                CheckRejectedExecutionReport(reqId);

                //check position report
                if (Global.FixApp.StopTakePositionReports.ContainsKey(nettingPositionId))
                {
                    var tryGetValue = Global.FixApp.StopTakePositionReports.TryGetValue(nettingPositionId, out reports);

                    if (!tryGetValue) continue;

                    if (reports != null && reports.Count > 0)
                    {
                        var lastReport = reports[reports.Count - 1];
                        if (lastReport.Key > dtSent)
                        {
                            report = lastReport.Value;
                            break;
                        }
                    }
                }

                CheckBusinessMessageReject(reqId);
            } while (DateTime.UtcNow - dt <= TIMEOUT);

            if (report == null)
                throw new Exception("fail getting replace order result (position report)");

            return report;
        }

        private PositionReport SendCancelOrderAndWait(string account, int securityId, string orderId, string nettingPositionId)
        {
            var dtSent = DateTime.UtcNow;
            var reqId = Global.FixApp.OrderCancelRequest(account, securityId.ToString(), orderId);

            //wait/get response message(s)
            IList<KeyValuePair<DateTime, PositionReport>> reports = null;
            PositionReport report = null;
            KeyValuePair<DateTime, ExecutionReport> executionReport = new KeyValuePair<DateTime, ExecutionReport>(DateTime.UtcNow, null);
            var dt = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);

                //check rejected execution report
                CheckRejectedExecutionReport(reqId);

                //check position report
                if (Global.FixApp.StopTakePositionReports.ContainsKey(nettingPositionId))
                {
                    var tryGetValue = Global.FixApp.StopTakePositionReports.TryGetValue(nettingPositionId, out reports);

                    if (!tryGetValue) continue;

                    if (reports != null && reports.Count > 0)
                    {
                        var lastReport = reports[reports.Count - 1];
                        if (lastReport.Key > dtSent)
                        {
                            report = lastReport.Value;
                            break;
                        }
                    }
                }

                CheckBusinessMessageReject(reqId);
            } while (DateTime.UtcNow - dt <= TIMEOUT);

            if (report == null)
                throw new Exception("fail getting cancel order result (position report)");

            return report;
        }

        private decimal SendBalanceRequestAndWait(string account)
        {
            var reqId = Global.FixApp.MDS5BalanceRequest(account);
            KeyValuePair<DateTime, decimal> balanceWithTime = new KeyValuePair<DateTime, decimal>(DateTime.UtcNow, -1);
            var dt = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);

                if (Global.FixApp.Balances.ContainsKey(reqId))
                {
                    var tryGetValue = Global.FixApp.Balances.TryGetValue(reqId, out balanceWithTime);

                    if (!tryGetValue) continue;

                    break;
                }

                CheckBusinessMessageReject(reqId);
            } while (DateTime.UtcNow - dt <= TIMEOUT); // timeout

            if (balanceWithTime.Value == -1)
                throw new Exception("fail getting balance");

            return balanceWithTime.Value;
        }

        private static string SendLoginRequestAndWait(string username, string password)
        {
            string account = null;

            var guid = Global.FixApp.LogOn(username, password);

            var dtLogon = DateTime.UtcNow;
            do
            {
                Thread.Sleep(SCAN_WAIT_MILLI_SECOND);
                if (Global.FixApp.UsernameAccounts.ContainsKey(username))
                {
                    account = Global.FixApp.UsernameAccounts[username];
                    break;
                }
            } while (DateTime.UtcNow - dtLogon <= TIMEOUT); // timeout

            if (string.IsNullOrEmpty(account))
                throw new Exception("fix log on time out");

            return account;
        }

        private static void CheckBusinessMessageReject(string reqId)
        {
//BusinessMessageReject? e.g. not logged in
            if (Global.FixApp.BusinessMessageRejects.ContainsKey(reqId))
            {
                KeyValuePair<DateTime, BusinessMessageReject> msg = new KeyValuePair<DateTime, BusinessMessageReject>(DateTime.UtcNow, null);
                var tryGetValue = Global.FixApp.BusinessMessageRejects.TryGetValue(reqId, out msg);

                if (tryGetValue)
                {
                    if (msg.Value.Text.Obj == "Specified User not logged in")
                        throw new UserNotLoggedInException();
                    else
                        throw new Exception("BusinessMessageReject: " + msg.Value.Text.Obj);
                }
            }
        }

        private static void CheckRejectedExecutionReport(string reqId)
        {
            if (Global.FixApp.RejectedExecutionReports.ContainsKey(reqId))
            {
                KeyValuePair<DateTime, ExecutionReport> executionReport;
                var tryGetValue = Global.FixApp.RejectedExecutionReports.TryGetValue(reqId, out executionReport);

                if (tryGetValue && executionReport.Value != null)
                {
                    //throw new Exception("Order rejected. Message: " + executionReport.Text.Obj);
                    var fault = new OrderRejectedFault();
                    fault.Text = executionReport.Value.Text.Obj;
                    throw new FaultException<OrderRejectedFault>(fault);
                }
            }
        }
    }

    //internal class BusinessMessageRejectException : Exception
    //{
    //    public BusinessMessageRejectException(string s) : base(s)
    //    {
    //    }
    //}

    internal class UserNotLoggedInException : Exception
    {
    }
}