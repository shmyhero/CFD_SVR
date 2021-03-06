﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CFD_API.Caching;
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

        /// <summary>
        /// key:    clientID
        /// value:  security IDs
        /// </summary>
        private readonly ConcurrentDictionary<string, IList<int>> _subscription = new ConcurrentDictionary<string, IList<int>>();

        private readonly ConcurrentDictionary<string, IList<int>> _subscription_Live = new ConcurrentDictionary<string, IList<int>>();

        //private readonly object _updateStockPricesLock = new object();

        ////stock can go up or down by a percentage of this factor on each change
        //private readonly double _rangePercent = .002;

        private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(1000);
        //private readonly Random _updateOrNotRandom = new Random();

        private readonly Timer _timer;
        private readonly Timer _timer_Live;
        //private volatile bool _updatingStockPrices = false;

        //private readonly IRedisTypedClient<Quote> _redisClient;

        private IDictionary<int, Quote> dicLastQuotes;
        private IDictionary<int, Quote> dicLastQuotes_Live;

        private IHubConnectionContext<dynamic> Clients { get; set; }

        private QuoteFeedTicker(IHubConnectionContext<dynamic> clients)
        {
            Clients = clients;

            //var basicRedisClientManager = CFDGlobal.GetNewBasicRedisClientManager();
            //_redisClient = CFDGlobal.BasicRedisClientManager.GetClient().As<Quote>();

            CFDGlobal.LogLine("Starting QuoteFeedTicker...");
            //Start();
            _timer = new Timer(Start, null, _updateInterval, TimeSpan.FromMilliseconds(-1));
            _timer_Live = new Timer(Start_Live, null, _updateInterval, TimeSpan.FromMilliseconds(-1));
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
                        //var quotes = _redisClient.GetAll();
                        var quotes = WebCache.Demo.Quotes;

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

                            if (subscribedQuotes.Any())
                            {
                                //Clients.Group(userId)
                                Clients.Client(userId)
                                    .p(subscribedQuotes.Select(o => new QuoteFeed
                                {
                                    id = o.Id,
                                    last = Quotes.GetLastPrice(o),
                                    bid = o.Bid,
                                    ask = o.Offer,

                                    time=o.Time,
                                }));
                            }
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

        private void Start_Live(object state)
        {
            DateTime dtLastBegin;
            while (true)
            {
                dtLastBegin = DateTime.Now;

                if (_subscription_Live.Count > 0)
                {
                    try
                    {
                        //var quotes = _redisClient.GetAll();
                        var quotes = WebCache.Live.Quotes;

                        if (dicLastQuotes_Live == null) //first time
                        {
                            dicLastQuotes_Live = quotes.ToDictionary(o => o.Id);
                            continue;
                        }

                        var updatedQuotes = quotes.Where(o => !dicLastQuotes_Live.ContainsKey(o.Id) || o.Time != dicLastQuotes_Live[o.Id].Time).ToList();

                        foreach (var pair in _subscription_Live)
                        {
                            var userId = pair.Key;
                            var subscribedQuotesIds = pair.Value;
                            var subscribedQuotes = updatedQuotes.Where(o => subscribedQuotesIds.Contains(o.Id));

                            if (subscribedQuotes.Any())
                            {
                                //Clients.Group(userId)
                                Clients.Client(userId)
                                    .p(subscribedQuotes.Select(o => new QuoteFeed
                                    {
                                        id = o.Id,
                                        last = Quotes.GetLastPrice(o),
                                        bid = o.Bid,
                                        ask = o.Offer,

                                        time = o.Time,
                                    }));
                            }
                        }

                        dicLastQuotes_Live = quotes.ToDictionary(o => o.Id);
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

            IList<int> output;
            _subscription_Live.TryRemove(identity, out output);
        }

        public void AddSubscription_Live(string identity, IList<int> ids)
        {
            _subscription_Live.AddOrUpdate(identity, ids, (key, value) => ids);

            IList<int> output;
            _subscription.TryRemove(identity, out output);
        }

        public void RemoveAllSubscription(string identity)
        {
            IList<int> value;

            _subscription.TryRemove(identity, out value);

            _subscription_Live.TryRemove(identity, out value);
        }

        public int GetSubscriptionCount(bool isLive = false)
        {
            return isLive? _subscription_Live.Count : _subscription.Count;
        }
    }
}