using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AyondoTrade;
using AyondoTrade.Model;
using CFD_API.Caching;
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

        private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(5);

        private readonly Timer _timer;

        //private readonly IRedisTypedClient<ProdDef> _redisClient;

        private IHubConnectionContext<dynamic> Clients { get; set; }

        private PositionReportTicker(IHubConnectionContext<dynamic> clients)
        {
            Clients = clients;

            //_redisClient = CFDGlobal.BasicRedisClientManager.GetClient().As<ProdDef>();

            //CFDGlobal.LogLine("Starting QuoteFeedTicker...");
            //_timer = new Timer(Start, null, _updateInterval, TimeSpan.FromMilliseconds(-1));
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
                        IDictionary<string, IList<PositionReport>> dicUserPositionReports;
                        using (var clientHttp = new AyondoTradeClient())
                        {
                            dicUserPositionReports = clientHttp.PopAutoClosedPositionReports(ayondoUsernames);
                        }

                        if (dicUserPositionReports.Count > 0)
                        {
                            var secIds = dicUserPositionReports.SelectMany(o => o.Value.Select(p => Convert.ToInt32(p.SecurityID))).Distinct().ToList();
                            //var prodDefs = _redisClient.GetByIds(secIds);

                            foreach (var pair in dicUserPositionReports) //for every ayondo username
                            {
                                string connectionId = null;
                                var tryGetValue = _subscription.TryGetValue(pair.Key, out connectionId);

                                if (tryGetValue)
                                {
                                    //var dtNow = DateTime.UtcNow;

                                    var alerts = pair.Value
                                        //.Where(o => dtNow -o.CreateTime <= TimeSpan.FromMinutes(3))//do not send AutoClose alert about the positions that are closed more than xxx minute ago
                                        .Select(report =>
                                    {
                                        var secId = Convert.ToInt32(report.SecurityID);
                                        var prodDef = WebCache.Demo.ProdDefs.FirstOrDefault(o => o.Id == secId);
                                        var name = Translator.GetCName(prodDef.Name);
                                        var stopTake = report.Text == "Position DELETE by StopLossOrder" ? "止损" : "止盈";
                                        var price = Math.Round(report.SettlPrice, prodDef.Prec);
                                        var pl = Math.Round(report.PL.Value, 2);
                                        return "您购买的" + name + "已被" + stopTake + "在" + price + "，收益为" + pl + "美元";
                                    }).ToList();

                                    Clients.Group(connectionId).p(alerts);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogExceptionAsInfo(e);
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

            //CFDGlobal.LogInformation("AlertHub add: username:" + ayondoUsername + " connectionId:" + connectionId);
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
                var tryRemove = _subscription.TryRemove(key, out value);

                //if(tryRemove)
                //    CFDGlobal.LogInformation("AlertHub remove: username:" + key + " connectionId:" + value);
            }
        }
    }
}