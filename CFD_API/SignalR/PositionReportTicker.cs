using System;
using System.Collections.Concurrent;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using AyondoTrade;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Cached;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using ServiceStack.Redis.Generic;

namespace CFD_API.SignalR
{
    public class PositionReportTicker
    {
        // Singleton instance
        private static readonly Lazy<PositionReportTicker> _instance =
            new Lazy<PositionReportTicker>(() => new PositionReportTicker(GlobalHost.ConnectionManager.GetHubContext<AlertHub>().Clients));

        public static PositionReportTicker Instance
        {
            get { return _instance.Value; }
        }

        //key: ayondoUsername, value: connectionId
        private readonly ConcurrentDictionary<string, string> _subscription = new ConcurrentDictionary<string, string>();

        private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(10);

        private readonly Timer _timer;

        private readonly IRedisTypedClient<ProdDef> _redisClient;

        private IHubConnectionContext<dynamic> Clients { get; set; }

        private PositionReportTicker(IHubConnectionContext<dynamic> clients)
        {
            Clients = clients;

            _redisClient = CFDGlobal.BasicRedisClientManager.GetClient().As<ProdDef>();

            CFDGlobal.LogLine("Starting QuoteFeedTicker...");
            //Start();
            _timer = new Timer(Start, null, _updateInterval, TimeSpan.FromMilliseconds(-1));
        }

        private void Start(object state)
        {
            DateTime dtLastBegin;
            while (true)
            {
                dtLastBegin = DateTime.Now;

                if (_subscription.Count > 0)
                {
                    var ayondoUsernames = _subscription.Select(o => o.Key).ToList();

                    try
                    {
                        EndpointAddress edpHttp = new EndpointAddress(CFDGlobal.AYONDO_TRADE_SVC_URL);
                        //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
                        AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

                        var dicUserPositionReports = clientHttp.PopAutoClosedPositionReports(ayondoUsernames);

                        if (dicUserPositionReports.Count > 0)
                        {
                            var secIds = dicUserPositionReports.SelectMany(o => o.Value.Select(p => Convert.ToInt32(p.SecurityID))).Distinct().ToList();
                            var prodDefs = _redisClient.GetByIds(secIds);

                            foreach (var pair in dicUserPositionReports) //for every ayondo username
                            {
                                string connectionId = null;
                                var tryGetValue = _subscription.TryGetValue(pair.Key, out connectionId);

                                if (tryGetValue)
                                {
                                    Clients.Group(connectionId).p(pair.Value.Select(report =>
                                    {
                                        var secId = Convert.ToInt32(report.SecurityID);
                                        var prodDef = prodDefs.FirstOrDefault(o => o.Id == secId);
                                        var name = Translator.GetCName(prodDef.Name);
                                        var stopTake = report.Text == "Position DELETE by StopLossOrder" ? "止损" : "止盈";
                                        var price = Math.Round(report.SettlPrice, prodDef.Prec);
                                        var pl = Math.Round(report.PL.Value, 2);
                                        return "您够买的" + name + "已被" + stopTake + "在" + price + "，收益为" + pl + "美元";
                                    }));
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogException(e);
                    }
                }

                var workTime = DateTime.Now - dtLastBegin;

                //broadcast prices every second
                var sleepTime = _updateInterval > workTime ? _updateInterval - workTime : TimeSpan.Zero;

                Thread.Sleep(sleepTime);
            }
        }

        public void AddSubscription(string ayondoUsername, string connectionId)
        {
            _subscription.AddOrUpdate(ayondoUsername, connectionId, (key, value) => connectionId);
        }

        public void RemoveSubscription(string connectionId)
        {
            string key = null;
            foreach (var pair in _subscription)
            {
                if (pair.Value == connectionId)
                {
                    key = pair.Key;
                    break;
                }
            }

            if (key != null)
            {
                string value;
                _subscription.TryRemove(key, out value);
            }
        }
    }
}