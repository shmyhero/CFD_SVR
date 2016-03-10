using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CFD_API.DTO.SignalRDTO;
using CFD_COMMON;
using CFD_JOBS.Models;
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

        private readonly ConcurrentDictionary<string, IEnumerable<int>> _subscription = new ConcurrentDictionary<string, IEnumerable<int>>();

        //private readonly object _updateStockPricesLock = new object();

        ////stock can go up or down by a percentage of this factor on each change
        //private readonly double _rangePercent = .002;

        private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(1000);
        //private readonly Random _updateOrNotRandom = new Random();

        private readonly Timer _timer;
        //private volatile bool _updatingStockPrices = false;

        private readonly IRedisTypedClient<Quote> _redisClient;

        private QuoteFeedTicker(IHubConnectionContext<dynamic> clients)
        {
            Clients = clients;

            //_stocks.Clear();
            //var stocks = new List<Stock>
            //{
            //    new Stock { Symbol = "MSFT", Price = 30.31m },
            //    new Stock { Symbol = "APPL", Price = 578.18m },
            //    new Stock { Symbol = "GOOG", Price = 570.30m }
            //};
            //stocks.ForEach(stock => _stocks.TryAdd(stock.Symbol, stock));

            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();
            _redisClient = basicRedisClientManager.GetClient().As<Quote>();

            _timer = new Timer(Start, null, _updateInterval,TimeSpan.FromMilliseconds(-1));
            //Start();
        }

        private void Start(object state)
        {
            while (true)
            {
                var quotes = _redisClient.GetAll();
                Clients.All.p(quotes.Where(o => o.Id <= 20961 && o.Id >= 20841
                    || o.Id >= 14012 && o.Id <= 14117
                    || o.Id >= 21612 && o.Id < 21752)
                    .Select(o => new QuoteFeed { id = o.Id, last = o.Offer }));

                Thread.Sleep(_updateInterval);
            }
        }

        private IHubConnectionContext<dynamic> Clients { get; set; }

        //public IEnumerable<Stock> GetAllStocks()
        //{
        //    return _stocks.Values;
        //}

        private void Tick(object state)
        {
            //lock (_updateStockPricesLock)
            //{
            //    if (!_updatingStockPrices)
            //    {
            //        _updatingStockPrices = true;

            //        foreach (var stock in _stocks.Values)
            //        {
            //            if (TryUpdateStockPrice(stock))
            //            {
            //                BroadcastStockPrice(stock);
            //            }
            //        }

            //        _updatingStockPrices = false;
            //    }
            //}

            //_subscription.SelectMany(o=>o.)
            var quotes = _redisClient.GetAll();

            //foreach (var pair in _subscription)
            //{
            //    dynamic user = Clients.User(1.ToString());
            //}

            //p -> publish
            Clients.All.p(quotes.Where(o => o.Id <= 20961 && o.Id >= 20841).Select(o => new QuoteFeed { id = o.Id, last = o.Offer }));
        }

        //private bool TryUpdateStockPrice(Stock stock)
        //{
        //    // Randomly choose whether to update this stock or not
        //    var r = _updateOrNotRandom.NextDouble();
        //    if (r > .1)
        //    {
        //        return false;
        //    }

        //    // Update the stock price by a random factor of the range percent
        //    var random = new Random((int)Math.Floor(stock.Price));
        //    var percentChange = random.NextDouble() * _rangePercent;
        //    var pos = random.NextDouble() > .51;
        //    var change = Math.Round(stock.Price * (decimal)percentChange, 2);
        //    change = pos ? change : -change;

        //    stock.Price += change;
        //    return true;
        //}

        //private void BroadcastStockPrice(Stock stock)
        //{
        //    Clients.All.updateStockPrice(stock);
        //}

        public void SetSubscription(string identity, IEnumerable<int> ids)
        {
            _subscription.AddOrUpdate(identity, ids, (key,value)=>ids );
        }
    }
}