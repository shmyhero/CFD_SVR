using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CFD_API.DTO;
using CFD_COMMON.Models.Cached;
using ServiceStack.Redis;

namespace CFD_API.Caching
{
    public class WebCache
    {
        private static readonly Lazy<WebCacheInstance> _demo =
            new Lazy<WebCacheInstance>(() => new WebCacheInstance(false));

        private static readonly Lazy<WebCacheInstance> _live =
            new Lazy<WebCacheInstance>(() => new WebCacheInstance(true));

        public static WebCacheInstance Demo
        {
            get { return _demo.Value; }
        }

        public static WebCacheInstance Live
        {
            get { return _live.Value; }
        }
    }
}