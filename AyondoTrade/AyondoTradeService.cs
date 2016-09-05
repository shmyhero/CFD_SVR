using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using AyondoTrade.FaultModel;
using CFD_COMMON;
using QuickFix.Fields;
using QuickFix.FIX44;

namespace AyondoTrade
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "AyondoTradeService" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select AyondoTradeService.svc or AyondoTradeService.svc.cs at the Solution Explorer and start debugging.
    public class AyondoTradeService : IAyondoTradeService
    {
        public static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(5);
        public static readonly int SCAN_WAIT_MILLI_SECOND = 250;

        public AyondoTradeService()
        {
            string token = null;
            try
            {
                token = OperationContext.Current.IncomingMessageHeaders.GetHeader<string>(Global.WCF_MSG_HEADER_TOKEN_KEY, Global.WCF_MSG_HEADER_TOKEN_NS);
            }
            catch (Exception)
            {
                throw new FaultException("authentication failed. no token in message header");
            }

            if (token != Global.WCF_MSG_HEADER_TOKEN_VALUE)
            {
                throw new FaultException("authentication failed. wrong token");
            }
        }

        public string Test(string text)
        {
            //CFDGlobal.LogLine("host service thread id " + Thread.CurrentThread.ManagedThreadId.ToString());
            return "You entered: " + text;
        }

        public string TestSleep(TimeSpan ts)
        {
            Thread.Sleep(ts);
            return "OK";
        }

        public IList<Model.PositionReport> DataTest(int count)
        {
            var result = new List<Model.PositionReport>();
            for (int i = 0; i < count; i++)
            {
                result.Add(new Model.PositionReport()
                {
                    CreateTime = DateTime.UtcNow,
                    Leverage = 10,
                    LongQty = 1.234567890m,
                    PosMaintRptID = "138111122223",
                    SecurityID = "12345",
                    SettlPrice = 123.4567m,
                    StopOID = "138111133334",
                    StopPx = 123.7654m,
                    //PL=null,
                    //ShortQty = null,
                });
            }
            return result;
        }

        public IDictionary<string, IList<Model.PositionReport>> PopAutoClosedPositionReports(IList<string> usernames)
        {
            var result = new Dictionary<string, IList<Model.PositionReport>>();

            foreach (var username in usernames)
            {
                if (Global.FixApp.AutoClosedPositionReports.ContainsKey(username))
                {
                    //CFDGlobal.LogInformation("popping AutoCloseMsg for username:"+username+ "...");

                    IList<KeyValuePair<DateTime, PositionReport>> value;
                    var tryRemove = Global.FixApp.AutoClosedPositionReports.TryRemove(username, out value);

                    if (tryRemove)
                    {
                        CFDGlobal.LogInformation("Popping " + value.Count + " AutoCloseMsg(s) for username:" + username);

                        result.Add(username, value.Select(o => MapPositionReport(o.Value)).ToList());
                    }
                }
            }

            return result;
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

            var result = MapPositionReport(report);
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

            var result = MapPositionReport(report);
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

            var result = MapPositionReport(report);
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

            var result = MapPositionReport(report);
            return result;
        }

        public decimal GetBalance(string username, string password, bool ignoreCache = false)
        {
            string account = GetAccount(username, password);

            decimal balance;

            if (!ignoreCache && CFDCacheManager.Instance.TryGetBalance(account, out balance))
            {
                return balance;
            }
            else
            {
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
        }

        public IList<Model.PositionReport> GetPositionReport(string username, string password, bool ignoreCache = false)
        {
            string account = GetAccount(username, password);

            IList<PositionReport> result = null;

            if (!ignoreCache)
            {
                result = CFDCacheManager.Instance.GetOpenPosition(account);
            }

            if (result == null)
            {
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

                CFDCacheManager.Instance.SetOpenPositions(account, result);
            }

            //mapping FIX message model --> WCF model
            var positionReports = result.Select(MapPositionReport).ToList();

            return positionReports;
        }

        public IList<Model.PositionReport> GetPositionHistoryReport(string username, string password, DateTime startTime, DateTime endTime, bool ignoreCache = false, bool updateCache = true)
        {
            string account = GetAccount(username, password);

            IList<PositionReport> result = null;

            if (!ignoreCache)
            {
                result = CFDCacheManager.Instance.GetClosedPosition(account);
            }

            if (result == null)
            {
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

                if (updateCache)
                    CFDCacheManager.Instance.SetClosedPositions(account, result);
            }

            //mapping FIX message model --> WCF model
            var positionReports = result.Select(MapPositionReport).ToList();

            return positionReports;
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
            {
                throw new FaultException("fail getting position report. guid:" + reqId);
                //return new List<PositionReport>();
            }

            if (reports.Count != 0 && reports.Count != reports[0].Value.TotalNumPosReports.Obj)
            {
                throw new FaultException("timeout getting position report. guid:" + reqId + " " + reports.Count + "/" + reports[0].Value.TotalNumPosReports.Obj);
                //return reports.Select(o => o.Value).ToList();
            }

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

                try
                {
                    CheckBusinessMessageReject(reqId);
                }
                catch (NoDataAvailableException)
                {
                    return new List<PositionReport>();
                }
            } while (DateTime.UtcNow - dtPositionReport <= TIMEOUT); // timeout

            if (reports == null)
            {
                CFDGlobal.LogError("fail getting position history report. guid:" + reqId);
                return new List<PositionReport>();
            }

            var lastPositionReport = reports.Last();
            var reportCount = Convert.ToInt32(lastPositionReport.Value.GetString(Global.FixApp.TAG_MDS_SetSize));
            var lastReportIndex = Convert.ToInt32(lastPositionReport.Value.GetString(Global.FixApp.TAG_MDS_SetIndex));

            if (lastReportIndex < reportCount - 1)
            {
                CFDGlobal.LogError("timeout getting position history report. guid:" + reqId + " count:" + reportCount + " lastReportIndex:" + lastReportIndex);
                return reports.Select(o => o.Value).ToList();
            }

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

                    if (nettingPositionId != null) //closing position: a 'Text=Position DELETE by MarketOrder' should be received after a 'Text=Position UPDATE by ...'
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
                throw new FaultException("fail getting order result " + reqId);

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
                throw new FaultException("fail getting new take order result (position report)");

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
                throw new FaultException("fail getting replace order result (position report)");

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
                throw new FaultException("fail getting cancel order result (position report)");

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
                throw new FaultException("fail getting balance");

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

                //if (Global.FixApp.UsernameAccounts.ContainsKey(username))
                //{
                //    account = Global.FixApp.UsernameAccounts[username];
                //    break;
                //}

                if (Global.FixApp.SuccessUserResponses.ContainsKey(guid))
                {
                    KeyValuePair<DateTime, UserResponse> msg = new KeyValuePair<DateTime, UserResponse>(DateTime.UtcNow, null);
                    var tryGetValue = Global.FixApp.SuccessUserResponses.TryGetValue(guid, out msg);

                    if (tryGetValue)
                    {
                        account = msg.Value.GetString(Tags.Account);
                        break;
                    }
                }

                CheckFailedUserResponse(guid);
            } while (DateTime.UtcNow - dtLogon <= TIMEOUT); // timeout

            //if no success user response: 1. login failed   2. donotresend caught
            //try to get account from UsernameAccounts then
            if (string.IsNullOrEmpty(account))
            {
                if (Global.FixApp.UsernameAccounts.ContainsKey(username))
                {
                    account = Global.FixApp.UsernameAccounts[username];
                }
            }

            if (string.IsNullOrEmpty(account))
                throw new FaultException("fix log on time out " + guid);

            return account;
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

        private Model.PositionReport MapPositionReport(PositionReport report)
        {
            var noPositionsGroup = new PositionMaintenanceRequest.NoPositionsGroup();
            report.GetGroup(1, noPositionsGroup);

            return new Model.PositionReport
            {
                PosMaintRptID = report.PosMaintRptID.Obj,

                Account = report.Account.Obj,

                SecurityID = report.SecurityID.Obj,
                SettlPrice = report.SettlPrice.Obj,

                //CreateTime = report.ClearingBusinessDate.Obj,
                CreateTime =
                    DateTime.ParseExact(report.ClearingBusinessDate.Obj, CFDGlobal.AYONDO_DATETIME_MASK, CultureInfo.CurrentCulture,
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
                Text = report.Text.Obj,
            };
        }

        private static void CheckFailedUserResponse(string reqId)
        {
            if (Global.FixApp.FailedUserResponses.ContainsKey(reqId))
            {
                KeyValuePair<DateTime, UserResponse> msg = new KeyValuePair<DateTime, UserResponse>(DateTime.UtcNow, null);
                var tryGetValue = Global.FixApp.FailedUserResponses.TryGetValue(reqId, out msg);

                if (tryGetValue)
                {
                    throw new FaultException(msg.Value.UserStatusText.Obj);
                }
            }
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
                    else if (msg.Value.Text.Obj == "No Data Available")
                        throw new NoDataAvailableException();
                    else
                        throw new FaultException("BusinessMessageReject: " + msg.Value.Text.Obj);
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

        public string PrintCache(string username)
        {
            if (!Global.FixApp.UsernameAccounts.ContainsKey(username))
            {
                return "User name doesn't not exist";
            }

            return CFDCacheManager.Instance.PrintStatusHtml(Global.FixApp.UsernameAccounts[username], username);
        }

        public void SwitchCache(string mode)
        {
            if (mode.ToLower() == "off")
            {
                CFDCacheManager.Instance.SwitchCache(false);
            }
            else if (mode.ToLower() == "on")
            {
                CFDCacheManager.Instance.SwitchCache(true);
            }
        }

        public void ClearCache(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                CFDCacheManager.Instance.ClearCache();
            }
            else if (Global.FixApp.UsernameAccounts.ContainsKey(username))
            {
                CFDCacheManager.Instance.ClearCache(Global.FixApp.UsernameAccounts[username]);
            }
        }
    }

    //internal class BusinessMessageRejectException : Exception
    //{
    //    public BusinessMessageRejectException(string s) : base(s)
    //    {
    //    }
    //}

    internal class UserNotLoggedInException : FaultException
    {
    }

    internal class NoDataAvailableException : FaultException
    {
    }
}