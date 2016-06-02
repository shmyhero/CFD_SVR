using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using EntityFramework.Caching;

namespace CFD_API.Caching
{
    public class WebCache
    {
        private static Timer _timerProdDef;
        private static Timer _timerQuote;
        private static TimeSpan _updateIntervalProdDef = TimeSpan.FromMinutes(1);
        private static TimeSpan _updateIntervalQuote = TimeSpan.FromSeconds(1);

        static WebCache()
        {
            try
            {
                ProdDefs = CFDGlobal.BasicRedisClientManager.GetClient().As<ProdDef>().GetAll();
                Quotes = CFDGlobal.BasicRedisClientManager.GetClient().As<Quote>().GetAll();
            }
            catch (Exception e)
            {
                CFDGlobal.LogException(e);
            }

            _timerProdDef = new Timer(UpdateProdDefs, null, _updateIntervalProdDef, _updateIntervalProdDef);
            _timerQuote = new Timer(UpdateQuotes, null, _updateIntervalQuote, _updateIntervalQuote);
        }

        //private static readonly Lazy<IList<ProdDef>> _prodDefs = new Lazy<IList<ProdDef>>(() =>
        //{
        //    var prodDefs = CFDGlobal.BasicRedisClientManager.GetClient().As<ProdDef>().GetAll();

        //    _timer = new Timer(UpdateProdDefs, null, _updateInterval, _updateInterval);

        //    return prodDefs;
        //});

        private static void UpdateProdDefs(object state)
        {
            //CFDGlobal.LogLine("Updating WebCache ProdDefs...");

            try
            {
                ProdDefs = CFDGlobal.BasicRedisClientManager.GetClient().As<ProdDef>().GetAll();
            }
            catch (Exception e)
            {
                CFDGlobal.LogException(e);
            }
        }

        private static void UpdateQuotes(object state)
        {
            //CFDGlobal.LogLine("Updating WebCache Quotes...");

            try
            {
                Quotes = CFDGlobal.BasicRedisClientManager.GetClient().As<Quote>().GetAll();
            }
            catch (Exception e)
            {
                CFDGlobal.LogException(e);
            }
        }

        //private static readonly Lazy<IList<Quote>> _quotes = new Lazy<IList<Quote>>(() => { return null; });

        public static IList<ProdDef> ProdDefs { get; private set; }
        public static IList<Quote> Quotes { get; private set; }

        public static ConcurrentDictionary<int,IList> 

        //public IList<Quote> Quotes
        //{
        //    get { return _quotes.Value; }
        //}
    }
}