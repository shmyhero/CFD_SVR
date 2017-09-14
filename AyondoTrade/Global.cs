using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CFD_COMMON;
using QuickFix;
using QuickFix.FIX44;
using QuickFix.Transport;

namespace AyondoTrade
{
    public class Global
    {
        public const string WCF_MSG_HEADER_TOKEN_KEY = "token";
        public const string WCF_MSG_HEADER_TOKEN_NS = "TH";
        public const string WCF_MSG_HEADER_TOKEN_VALUE = "xM3dY7pZ4bH7mB0oC2xY5vF1aP0bN7eV4iL1sT2";

        private static Timer _timer;
        private static TimeSpan _updateInterval = TimeSpan.FromMinutes(10);

        // Singleton instance
        private static readonly Lazy<AyondoFixTradeApp> _instance = new Lazy<AyondoFixTradeApp>(delegate
        {
            CFDGlobal.LogInformation("Starting FIX initiator...");

            var application = new AyondoFixTradeApp();

            SessionSettings settings = new SessionSettings(CFDGlobal.GetConfigurationSetting("ayondoFixTradeCfgFilePath"));

            //var path = CFDGlobal.GetConfigurationSetting("ayondoFixTradeCfgFilePath");
            //var serverPath = HostingEnvironment.MapPath("~/" + path);

            //SessionSettings settings = new SessionSettings(serverPath);

            ////Resetting some config path because this code is run by IIS, different from RoleEntryPoint
            ////Therefore the current path is now in IIS folder, not application folder
            ////Converting all the path to the application folder:
            //var dictionary = settings.Get(new SessionID("FIX.4.4", "THCN_Trade", "TXIOBridge"));

            ////DataDictionary=.\Fix44.xml
            //var cfgKey = "DataDictionary";
            //var oldValue = dictionary.GetString(cfgKey);
            //var newValue = HostingEnvironment.MapPath("~/Fix44.xml");
            //CFDGlobal.LogLine("Setting FIX Config - " + cfgKey + ": " + oldValue + " -> " + newValue);
            //dictionary.SetString(cfgKey, newValue);
            
            ////FileStorePath=.\fixfiles
            //cfgKey = "FileStorePath";
            //oldValue = dictionary.GetString(cfgKey);
            //newValue = HostingEnvironment.MapPath("~/fixfiles");
            //CFDGlobal.LogLine("Setting FIX Config - " + cfgKey + ": " + oldValue + " -> " + newValue);
            //dictionary.SetString(cfgKey, newValue);

            ////FileLogPath=.\fixfiles\log
            //cfgKey = "FileLogPath";
            //oldValue = dictionary.GetString(cfgKey);
            //newValue = HostingEnvironment.MapPath("~/fixfiles/log");
            //CFDGlobal.LogLine("Setting FIX Config - " + cfgKey + ": " + oldValue + " -> " + newValue);
            //dictionary.SetString(cfgKey, newValue);

            ////DebugFileLogPath=.\fixfiles\log
            //cfgKey = "DebugFileLogPath";
            //oldValue = dictionary.GetString(cfgKey);
            //newValue = HostingEnvironment.MapPath("~/fixfiles/log");
            //CFDGlobal.LogLine("Setting FIX Config - " + cfgKey + ": " + oldValue + " -> " + newValue);
            //dictionary.SetString(cfgKey, newValue);

            ////settings.Set(new SessionID("FIX.4.4", "THCN_Trade", "TXIOBridge"), new Dictionary("DataDictionary",));

            IMessageStoreFactory storeFactory = new MemoryStoreFactory();//new FileStoreFactory(settings);
            ILogFactory logFactory = new FileLogFactory(settings); //fix logging
            SocketInitiator initiator = new SocketInitiator(application, storeFactory, settings, logFactory);

            initiator.Start();

            CFDGlobal.LogLine("FIX initiator started. Waiting for FIX session...");

            var dt = DateTime.UtcNow;

            //check fix.Session
            while (application.Session == null)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));

                if (DateTime.UtcNow - dt >= TimeSpan.FromMinutes(1))
                {
                    CFDGlobal.LogWarning("FIX session establish timeout.");
                    break;
                }
            }

            if (application.Session != null)
                CFDGlobal.LogLine("FIX session established.");

            //set timer for clearing old fix messages
            _timer = new Timer(Start, null, _updateInterval, _updateInterval);

            return application;
        });

        private static void Start(object state)
        {
            CFDGlobal.LogLine("Start clearing old fix messages...");

            var dtNow = DateTime.UtcNow;
            var ts = TimeSpan.FromMinutes(10);

            int countOld = 0;
            int countNew = 0;
            List<string> keysToRemove;

            //Balance
            countOld = FixApp.Balances.Count;
            keysToRemove = FixApp.Balances.Where(pair => dtNow - pair.Value.Key > ts).Select(pair => pair.Key).ToList();
            foreach (var key in keysToRemove)
            {
                KeyValuePair<DateTime, decimal> value;
                FixApp.Balances.TryRemove(key, out value);
            }
            countNew = FixApp.Balances.Count;
            CFDGlobal.LogLine("Balances:" + countOld + " -> " + countNew);

            //BusinessMessageRejects
            countOld = FixApp.BusinessMessageRejects.Count;
            keysToRemove = FixApp.BusinessMessageRejects.Where(pair => dtNow - pair.Value.Key > ts).Select(pair => pair.Key).ToList();
            foreach (var key in keysToRemove)
            {
                KeyValuePair<DateTime, BusinessMessageReject> value;
                FixApp.BusinessMessageRejects.TryRemove(key, out value);
            }
            countNew = FixApp.BusinessMessageRejects.Count;
            CFDGlobal.LogLine("BusinessMessageRejects:" + countOld + " -> " + countNew);

            //SuccessUserResponses
            countOld = FixApp.SuccessUserResponses.Count;
            keysToRemove = FixApp.SuccessUserResponses.Where(pair => dtNow - pair.Value.Key > ts).Select(pair => pair.Key).ToList();
            foreach (var key in keysToRemove)
            {
                KeyValuePair<DateTime, UserResponse> value;
                FixApp.SuccessUserResponses.TryRemove(key, out value);
            }
            countNew = FixApp.SuccessUserResponses.Count;
            CFDGlobal.LogLine("SuccessUserResponses:" + countOld + " -> " + countNew);

            //RejectedExecutionReports
            countOld = FixApp.RejectedExecutionReports.Count;
            keysToRemove = FixApp.RejectedExecutionReports.Where(pair => dtNow - pair.Value.Key > ts).Select(pair => pair.Key).ToList();
            foreach (var key in keysToRemove)
            {
                KeyValuePair<DateTime, ExecutionReport> value;
                FixApp.RejectedExecutionReports.TryRemove(key, out value);
            }
            countNew = FixApp.RejectedExecutionReports.Count;
            CFDGlobal.LogLine("RejectedExecutionReports:" + countOld + " -> " + countNew);

            //RequestForPositionsAcks
            countOld = FixApp.RequestForPositionsAcks.Count;
            keysToRemove = FixApp.RequestForPositionsAcks.Where(pair => dtNow - pair.Value.Key > ts).Select(pair => pair.Key).ToList();
            foreach (var key in keysToRemove)
            {
                KeyValuePair<DateTime, RequestForPositionsAck> value;
                FixApp.RequestForPositionsAcks.TryRemove(key, out value);
            }
            countNew = FixApp.RequestForPositionsAcks.Count;
            CFDGlobal.LogLine("RequestForPositionsAcks:" + countOld + " -> " + countNew);

            CFDGlobal.LogLine("FailedUserResponses:" + FixApp.FailedUserResponses.Count);
            CFDGlobal.LogLine("LoggedOutUserResponses:" + FixApp.LoggedOutUserResponses.Count);
            CFDGlobal.LogLine("CreatedTransferIDs:" + FixApp.CreatedTransferIDs.Count);
            CFDGlobal.LogLine("CompleteTransferIDs:" + FixApp.CompletedTransferIDs.Count);
            CFDGlobal.LogLine("ErroredTransferRequests:" + FixApp.ErroredTransferRequests.Count);

            //AutoClosedPositionReports
            countOld = FixApp.AutoClosedPositionReports.Sum(o => o.Value.Count);
            keysToRemove = FixApp.AutoClosedPositionReports.Where(pair => pair.Value.Count > 0 && dtNow - pair.Value.Last().Key > ts).Select(pair => pair.Key).ToList();
            foreach (var key in keysToRemove)
            {
                IList<KeyValuePair<DateTime, PositionReport>> value;
                FixApp.AutoClosedPositionReports.TryRemove(key, out value);
            }
            countNew = FixApp.AutoClosedPositionReports.Sum(o => o.Value.Count);
            CFDGlobal.LogLine("AutoClosedPositionReports:" + countOld + " -> " + countNew);

            //OrderPositionReports
            countOld = FixApp.OrderPositionReports.Sum(o => o.Value.Count);
            keysToRemove = FixApp.OrderPositionReports.Where(pair => pair.Value.Count > 0 && dtNow - pair.Value.Last().Key > ts).Select(pair => pair.Key).ToList();
            foreach (var key in keysToRemove)
            {
                IList<KeyValuePair<DateTime, PositionReport>> value;
                FixApp.OrderPositionReports.TryRemove(key, out value);
            }
            countNew = FixApp.OrderPositionReports.Sum(o => o.Value.Count);
            CFDGlobal.LogLine("OrderPositionReports:" + countOld + " -> " + countNew);

            //PositionReports
            countOld = FixApp.PositionReports.Sum(o => o.Value.Count);
            keysToRemove = FixApp.PositionReports.Where(pair => pair.Value.Count > 0 && dtNow - pair.Value.Last().Key > ts).Select(pair => pair.Key).ToList();
            foreach (var key in keysToRemove)
            {
                IList<KeyValuePair<DateTime, PositionReport>> value;
                FixApp.PositionReports.TryRemove(key, out value);
            }
            countNew = FixApp.PositionReports.Sum(o => o.Value.Count);
            CFDGlobal.LogLine("PositionReports:" + countOld + " -> " + countNew);

            //StopTakePositionReports
            countOld = FixApp.StopTakePositionReports.Sum(o => o.Value.Count);
            keysToRemove = FixApp.StopTakePositionReports.Where(pair => pair.Value.Count > 0 && dtNow - pair.Value.Last().Key > ts).Select(pair => pair.Key).ToList();
            foreach (var key in keysToRemove)
            {
                IList<KeyValuePair<DateTime, PositionReport>> value;
                FixApp.StopTakePositionReports.TryRemove(key, out value);
            }
            countNew = FixApp.StopTakePositionReports.Sum(o => o.Value.Count);
            CFDGlobal.LogLine("StopTakePositionReports:" + countOld + " -> " + countNew);

            CFDGlobal.LogLine("End clearing old fix messages.");
        }

        public static AyondoFixTradeApp FixApp
        {
            get { return _instance.Value; }
        }
    }
}