using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CFD_API.DTO.SignalRDTO;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Utils;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using ServiceStack.Redis.Generic;

namespace CFD_API.SignalR
{
    public class QuoteFeedTicker
    {
        // Singleton instance
        private static readonly Lazy<QuoteFeedTicker> _instance =
            new Lazy<QuoteFeedTicker>(() => new QuoteFeedTicker(GlobalHost.ConnectionManager.GetHubContext<QuoteFeedHub>().Clients));

        public static QuoteFeedTicker Instance
        {
            get { return _instance.Value; }
        }

        private readonly ConcurrentDictionary<string, IList<int>> _subscription = new ConcurrentDictionary<string, IList<int>>();

        //private readonly object _updateStockPricesLock = new object();

        ////stock can go up or down by a percentage of this factor on each change
        //private readonly double _rangePercent = .002;

        private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(1000);
        //private readonly Random _updateOrNotRandom = new Random();

        private readonly Timer _timer;
        //private volatile bool _updatingStockPrices = false;

        private readonly IRedisTypedClient<Quote> _redisClient;

        private IDictionary<int, Quote> dicLastQuotes;

        private IHubConnectionContext<dynamic> Clients { get; set; }

        private QuoteFeedTicker(IHubConnectionContext<dynamic> clients)
        {
            Clients = clients;

            //var basicRedisClientManager = CFDGlobal.GetNewBasicRedisClientManager();
            _redisClient = CFDGlobal.BasicRedisClientManager.GetClient().As<Quote>();

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
                    try
                    {
                        var quotes = _redisClient.GetAll();

                        if (dicLastQuotes == null) //first time
                        {
                            dicLastQuotes = quotes.ToDictionary(o => o.Id);
                            continue;
                        }

                        var updatedQuotes = quotes.Where(o => !dicLastQuotes.ContainsKey(o.Id) || o.Time != dicLastQuotes[o.Id].Time).ToList();

                        //CFDGlobal.LogLine("Broadcasting to " + _subscription.Count +" subscriber...");
                        foreach (var pair in _subscription)
                        {
                            var userId = pair.Key;
                            var subscribedQuotesIds = pair.Value;
                            var subscribedQuotes = updatedQuotes.Where(o => subscribedQuotesIds.Contains(o.Id));
                            Clients.Group(userId).p(subscribedQuotes.Select(o => new QuoteFeed
                            {
                                id = o.Id,
                                last = Quotes.GetLastPrice(o),
                                bid = o.Bid,
                                ask = o.Offer,
                            }));
                        }

                        dicLastQuotes = quotes.ToDictionary(o => o.Id);
                    }
                    catch (Exception)
                    {
                        
                    }
                }

                var workTime = DateTime.Now - dtLastBegin;

                //broadcast prices every second
                var sleepTime = _updateInterval > workTime ? _updateInterval - workTime : TimeSpan.Zero;

                Thread.Sleep(sleepTime);
            }
        }

        public void AddSubscription(string identity, IList<int> ids)
        {
            _subscription.AddOrUpdate(identity, ids, (key, value) => ids);
        }

        public void RemoveSubscription(string identity)
        {
            IList<int> value;
            _subscription.TryRemove(identity, out value);
        }
    }
}