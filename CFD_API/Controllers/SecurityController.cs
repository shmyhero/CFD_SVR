using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.Caching;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using EntityFramework.Extensions;
using Pinyin4net;
using Pinyin4net.Format;

namespace CFD_API.Controllers
{
    //[BasicAuth]
    [RoutePrefix("api/security")]
    public class SecurityController : CFDController
    {
        //public SecurityController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
        //    : base(db, mapper, redisClient)
        //{
        //}

        public SecurityController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        public void UpdateLastPrice(IList<SecurityLiteDTO> list)
        {
            if (list.Count == 0) return;

            //var redisProdDefClient = RedisClient.As<ProdDef>();
            //var redisQuoteClient = RedisClient.As<Quote>();

            var ids = list.Select(o => o.id).ToList();
            //var quotes = redisQuoteClient.GetByIds(ids);
            var quotes = WebCache.GetInstance(IsLiveUrl).Quotes.Where(o => ids.Contains(o.Id)).ToList();
            //var prodDefs = redisProdDefClient.GetByIds(ids);

            foreach (var security in list)
            {
                //var prodDef = prodDefs.FirstOrDefault(o => o.Id == security.id);
                //if (prodDef != null)
                //{
                //    security.preClose = prodDef.PreClose;
                //    security.open = Quotes.GetOpenPrice(prodDef);
                //    security.isOpen = prodDef.QuoteType == enmQuoteType.Open;
                //}

                var quote = quotes.FirstOrDefault(o => o.Id == security.id);
                if (quote != null)
                {
                    security.last = Quotes.GetLastPrice(quote);
                }
            }
        }

        //public void UpdateProdDefQuote(IList<SecurityDTO> list)
        //{
        //    if (list.Count == 0) return;

        //    var redisProdDefClient = RedisClient.As<ProdDef>();
        //    var redisQuoteClient = RedisClient.As<Quote>();

        //    var ids = list.Select(o => o.id).ToList();
        //    var quotes = redisQuoteClient.GetByIds(ids);
        //    var prodDefs = redisProdDefClient.GetByIds(ids);

        //    foreach (var security in list)
        //    {
        //        var prodDef = prodDefs.FirstOrDefault(o => o.Id == security.id);
        //        if (prodDef != null)
        //        {
        //            if (security.name == null) security.name = prodDef.Name;
        //            security.symbol = prodDef.Symbol;

        //            security.preClose = prodDef.PreClose;
        //            security.open = Quotes.GetOpenPrice(prodDef);
        //            security.isOpen = prodDef.QuoteType == enmQuoteType.Open;
        //        }

        //        var quote = quotes.FirstOrDefault(o => o.Id == security.id);
        //        if (quote != null)
        //        {
        //            security.last = Quotes.GetLastPrice(quote);
        //        }
        //    }
        //}

        private IList<ProdDef> GetActiveProds(bool isLive = false, bool includeUntranslated = false)
        {
            return WebCache.GetInstance(isLive).ProdDefs
                .Where(o => o.QuoteType != enmQuoteType.Inactive
                            && (DateTime.UtcNow - o.Time) < CFDGlobal.PROD_DEF_ACTIVE_IF_TIME_NOT_OLDER_THAN_TS
                            && o.Bid.HasValue && o.Offer.HasValue
                            && (includeUntranslated || Products.HasChineseTranslation(o.Name))
                )
                .ToList();
        }

        private IList<ProdDef> GetActiveProdsByIdsKeepOrder(IList<int> ids, bool isLive)
        {
            var activeProds = GetActiveProds(isLive);
            return ids.Select(id => activeProds.FirstOrDefault(o => o.Id == id)).Where(o => o != null).ToList();
        }

        [HttpGet]
        [Route("bookmark")]
        [Route("live/bookmark")]
        [BasicAuth]
        public List<SecurityLiteDTO> GetBookmarkList(int page = 1, int perPage = 20)
        {
            var bookmarkIDs = IsLiveUrl
                ? db.Bookmark_Live.Where(o => o.UserId == UserId).OrderBy(o => o.DisplayOrder).Skip((page - 1)*perPage).Take(perPage).Select(o => o.AyondoSecurityId).ToList()
                : db.Bookmarks.Where(o => o.UserId == UserId).OrderBy(o => o.DisplayOrder).Skip((page - 1)*perPage).Take(perPage).Select(o => o.AyondoSecurityId).ToList();

            //var prodDefs = RedisClient.As<ProdDef>().GetByIds(bookmarkIDs);
            var prodDefs = GetActiveProdsByIdsKeepOrder(bookmarkIDs, IsLiveUrl);

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityLiteDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("byIds/{securityIds}")]
        [Route("live/byIds/{securityIds}")]
        public List<SecurityLiteDTO> GetSecuritiesByIds(string securityIds)
        {
            if (securityIds == null)
                securityIds = string.Empty;

            var ids = securityIds.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct().ToList();

            //var redisTypedClient = RedisClient.As<ProdDef>();

            //var prodDefs = redisTypedClient.GetByIds(ids);
            var prodDefs = GetActiveProdsByIdsKeepOrder(ids, IsLiveUrl);

            //var securities = db.AyondoSecurities.Where(o => ids.Contains(o.Id)).ToList();

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityLiteDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("stock/topGainer")]
        [Route("live/stock/topGainer")]
        public List<SecurityLiteDTO> GetTopGainerList(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds(IsLiveUrl);

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_STOCK && Products.IsUSStocks(o.Symbol)).ToList();

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityLiteDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            securityDtos = securityDtos.OrderByDescending(o => o.last/o.preClose).Skip((page - 1)*perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("stock")]
        public List<SecurityLiteDTO> GetAllStocks(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds();

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_STOCK).ToList();

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityLiteDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            securityDtos = securityDtos.OrderByDescending(o => o.last/o.preClose).Skip((page - 1)*perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("stock/us")]
        [Route("live/stock/us")]
        public List<SecurityLiteDTO> GetUSStocks(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds(IsLiveUrl);

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_STOCK && Products.IsUSStocks(o.Symbol)).ToList();

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityLiteDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            securityDtos = securityDtos.OrderByDescending(o => o.last / o.preClose).Skip((page - 1) * perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("stock/hk")]
        [Route("live/stock/hk")]
        public List<SecurityLiteDTO> GetHKStocks(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds(IsLiveUrl);

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_STOCK && Products.IsHKStocks(o.Symbol)).ToList();

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityLiteDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            securityDtos = securityDtos.OrderByDescending(o => o.last / o.preClose).Skip((page - 1) * perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("index")]
        [Route("live/index")]
        public List<SecurityLiteDTO> GetIndexList(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds(IsLiveUrl);

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_INDEX).ToList();

            var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).Select(o => Mapper.Map<SecurityLiteDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("fx")]
        [Route("live/fx")]
        public List<SecurityLiteDTO> GetFxList(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds(IsLiveUrl);

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_FX && !o.Name.EndsWith(" Outright")).ToList();

            var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).Select(o => Mapper.Map<SecurityLiteDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("fx/outright")]
        [Route("live/fx/outright")]
        public List<SecurityLiteDTO> GetFxOutrightList(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds(IsLiveUrl, true);

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_FX && o.Name.EndsWith(" Outright")).ToList();

            var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1) * perPage).Take(perPage).Select(o => Mapper.Map<SecurityLiteDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("futures")]
        [Route("live/futures")]
        public List<SecurityLiteDTO> GetFuturesList(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds(IsLiveUrl);

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_COMMODITY).ToList();

            var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).Select(o => Mapper.Map<SecurityLiteDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            return securityDtos;
        }

        /// <summary>
        /// todo: for test use only
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("all")]
        [Route("live/all")]
        public List<ProdDefDTO> GetAllList(int page = 1, int perPage = 20)
        {
            var cache = Request.RequestUri.AbsolutePath.EndsWith("live/all") ? WebCache.Live : WebCache.Demo;

            //var redisProdDefClient = RedisClient.As<ProdDef>();
            //var redisQuoteClient = RedisClient.As<Quote>();
            //var prodDefs = RedisClient.As<ProdDef>().GetAll();
            //var quotes = redisQuoteClient.GetAll();

            //var securities = db.AyondoSecurities.ToList();

            var result = cache.ProdDefs.Select(o => Mapper.Map<ProdDefDTO>(o)).ToList();

            foreach (var prodDTO in result)
            {
                ////get cname
                //var @default = securities.FirstOrDefault(o => o.Id == prodDTO.Id);
                //if (@default != null && @default.CName != null)
                //    prodDTO.cname = @default.CName;
                prodDTO.cname = Translator.GetCName(prodDTO.Name);

                //get new price
                var quote = cache.Quotes.FirstOrDefault(o => o.Id == prodDTO.Id);
                if (quote != null)
                {
                    //prodDTO.last = Quotes.GetLastPrice(quote);

                    //calculate min/max trade value
                    var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == prodDTO.Id);

                    var perPriceCcy2 = prodDef.LotSize/prodDef.PLUnits;

                    decimal minLong = perPriceCcy2*quote.Offer*prodDef.MinSizeLong;
                    decimal minShort = perPriceCcy2*quote.Bid*prodDef.MinSizeShort;
                    decimal maxLong = perPriceCcy2*quote.Offer*prodDef.MaxSizeLong;
                    decimal maxShort = perPriceCcy2*quote.Bid*prodDef.MaxSizeShort;

                    try
                    {
                        var perSizeValueUSD = FX.ConvertByOutrightMidPrice(1, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes);

                        prodDTO.minValueLong = minLong*perSizeValueUSD;
                        prodDTO.minValueShort = minShort*perSizeValueUSD;
                        prodDTO.maxValueLong = maxLong*perSizeValueUSD;
                        prodDTO.maxValueShort = maxShort*perSizeValueUSD;
                    }
                    catch (Exception e)
                    {
                    }
                }
                else
                {
                    //prodDTO.last = null;
                }
            }

            return result.OrderBy(o => o.AssetClass).ThenBy(o => o.Name).ToList();
        }

        [HttpGet]
        [Route("live/tradable")]
        [IPAuth]
        public List<ProdDefDTO> GetAllTradableSecurities()
        {
            var activeProds = GetActiveProds(true);

            var tradable = activeProds.Where(o => !o.Name.EndsWith(" Outright")).ToList();

            var result = tradable.Select(o => Mapper.Map<ProdDefDTO>(o)).ToList();

            foreach (var prodDTO in result)
            {
                ////get cname
                //var @default = securities.FirstOrDefault(o => o.Id == prodDTO.Id);
                //if (@default != null && @default.CName != null)
                //    prodDTO.cname = @default.CName;
                prodDTO.cname = Translator.GetCName(prodDTO.Name);
            }

            return result.OrderBy(o => o.AssetClass).ThenBy(o => o.Name).ToList();
        }

        [HttpGet]
        [Route("search")]
        [Route("live/search")]
        public List<SecurityLiteDTO> SearchSecurity(string keyword, int page = 1, int perPage = 20)
        {
            if(string.IsNullOrWhiteSpace(keyword)) return new List<SecurityLiteDTO>();

            keyword = keyword.ToLower();

            var activeProds = GetActiveProds(IsLiveUrl);

            //var securities = db.AyondoSecurities.Where(o => o.CName != null).ToList();

            var format = new HanyuPinyinOutputFormat();
            format.ToneType = HanyuPinyinToneType.WITHOUT_TONE;
            format.CaseType = HanyuPinyinCaseType.LOWERCASE;
            format.VCharType = HanyuPinyinVCharType.WITH_V;

            var securityDtos = activeProds
                .Where(o =>
                    (o.AssetClass != CFDGlobal.ASSET_CLASS_STOCK || (Products.IsUSStocks(o.Symbol) || Products.IsHKStocks(o.Symbol)))//US Stocks and non-stocks
                    && !o.Name.EndsWith(" Outright") //exclude Outright
                ) 
                .Select(delegate(ProdDef o)
                {
                    var result= Mapper.Map<SecurityLiteDTO>(o);
                    result.eName = o.Name;
                    return result;
                })
                .Where(delegate(SecurityLiteDTO o)
                {
                    //for example 雅培制药

                    var charArray = o.name.ToCharArray();//雅,培,制,药
                    var arrPinyin = charArray.Select(c => PinyinHelper.ToHanyuPinyinStringArray(c, format))
                        .Where(arr => arr != null && arr.Length > 0)
                        .Select(arr => arr[0]);//ya,pei,zhi,yao

                    bool pinyinMatch = false;

                    if (arrPinyin.Any())
                    {
                        var aggregateFirstLetters = arrPinyin.Select(p=>p.Substring(0,1)).Aggregate((p, n) => p + n);//ypzy
                        var aggregateFullPinyin = arrPinyin.Aggregate((p, n) => p + n);//yapeizhiyao

                        pinyinMatch = aggregateFirstLetters.Contains(keyword)|| aggregateFullPinyin.Contains(keyword);
                    }

                    return (o.name.ToLower().Contains(keyword)
                            || o.eName.ToLower().Contains(keyword)
                            || o.symbol.ToLower().Contains(keyword)
                            || pinyinMatch
                            );
                })
                .OrderBy(o => o.symbol)
                .Skip((page - 1)*perPage).Take(perPage).ToList();

            //IList<ProdDef> prodDefs = new List<ProdDef>();
            //foreach (var p in all)
            //{
            //    var sec = securities.FirstOrDefault(s => s.Id == p.Id);
            //    if (sec != null)
            //    {
            //        if (p.AssetClass == "Single Stocks" && sec.Financing != "US Stocks") //for stocks, only US stocks
            //            continue;

            //        if (!p.Symbol.ToLower().Contains(keyword.ToLower()) && !sec.CName.Contains(keyword)) //search keyword
            //            continue;

            //        p.Name = sec.CName;
            //        prodDefs.Add(p);
            //    }
            //}

            //var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateLastPrice(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("{securityId}")]
        [Route("live/{securityId}")]
        public SecurityDetailDTO GetSecurity(int securityId)
        {
            //var redisProdDefClient = RedisClient.As<ProdDef>();
            //var redisQuoteClient = RedisClient.As<Quote>();

            //var prodDef = redisProdDefClient.GetById(securityId);

            var cache = WebCache.GetInstance(IsLiveUrl);

            var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == securityId);

            if (prodDef == null)
                return null;

            //mapping
            var result = Mapper.Map<SecurityDetailDTO>(prodDef);

            //var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == securityId);
            ////get cname
            //if (security != null && security.CName != null)
            //    result.name = security.CName;

            //get new price
            //var quote = redisQuoteClient.GetById(securityId);
            var quote = cache.Quotes.FirstOrDefault(o => o.Id == securityId);
            if (Quotes.IsPriceDown(cache.GetProdSettingByID(quote.Id), quote.Time))
            {
                result.isPriceDown = true;
            }

            result.last = Quotes.GetLastPrice(quote);
            result.ask = quote.Offer;
            result.bid = quote.Bid;

            //************************************************************************
            //TradeValue (to ccy2) = QuotePrice * (1 / MDS_PLUNITS * MDS_LOTSIZE) * quantity
            //************************************************************************

            var perPriceCcy2 = prodDef.LotSize/prodDef.PLUnits;

            //GSMS limit
            var maxLongSize = prodDef.MaxSizeLong;
            var maxShortSize = prodDef.MaxSizeShort;
            if (prodDef.GSMS > 0)
            {
                maxLongSize = prodDef.GSMS;
                maxShortSize = prodDef.GSMS;
            }

            decimal minLong = perPriceCcy2*quote.Offer*prodDef.MinSizeLong;
            decimal minShort = perPriceCcy2*quote.Bid*prodDef.MinSizeShort;
            decimal maxLong = perPriceCcy2*quote.Offer*maxLongSize;
            decimal maxShort = perPriceCcy2*quote.Bid*maxShortSize;

            var perSizeValueUSD = FX.ConvertByOutrightMidPrice(1, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes);

            result.minValueLong = Math.Ceiling(minLong*perSizeValueUSD);
            result.minValueShort = Math.Ceiling(minShort*perSizeValueUSD);
            result.maxValueLong = Math.Floor(maxLong*perSizeValueUSD);
            result.maxValueShort = Math.Floor(maxShort*perSizeValueUSD);
            
            if (IsLiveUrl) //只有实盘需要设置最小投入本金
            {
                var prodSetting = cache.GetProdSettingByID(securityId);
                if (prodSetting != null && prodSetting.MinInvestUSD.HasValue)
                {
                    result.minInvestUSD = prodSetting.MinInvestUSD.Value;
                }
            }

            //demo data
            Random r = new Random();
            result.longPct = (decimal) r.NextDouble();

            //for single stocks and ..., reduct max lev so that gsmd will be much smaller than 100%
            var lev = prodDef.AssetClass == CFDGlobal.ASSET_CLASS_STOCK ||
                      (prodDef.AssetClass == CFDGlobal.ASSET_CLASS_FX && prodDef.Symbol.StartsWith("XBT"))
                ? (int) (prodDef.MaxLeverage/2)
                : (int) prodDef.MaxLeverage;

            if (lev*prodDef.GSMD > 0.5m)
                CFDGlobal.LogWarning("max_lev * gsmd > 50% detected! sec_id:" + prodDef.Id);

            //lev to int
            result.maxLeverage = Math.Floor(prodDef.MaxLeverage);

            //generate lev list for client
            if (lev <= 10)
            {
                result.levList = Enumerable.Range(1, lev).ToList();
            }
            else if (lev <= 15)
            {
                result.levList = new List<int>() {1, 2, 3, 4, 5, 6, 7, 10, lev};
            }
            else if (lev <= 20)
            {
                result.levList = new List<int>() {1, 2, 3, 4, 5, 6, 7, 10, 15, lev};
            }
            else if (lev <= 30)
            {
                result.levList = new List<int>() {1, 2, 3, 4, 5, 10, 15, 20, lev};
            }
            else if (lev <= 50)
            {
                result.levList = new List<int>() {1, 2, 3, 4, 5, 10, 15, 20, 30, lev};
            }
            else if (lev <= 70)
            {
                result.levList = new List<int>() {1, 2, 3, 4, 5, 10, 15, 20, 30, 50, lev};
            }
            else if (lev <= 100)
            {
                result.levList = new List<int>() {1, 2, 3, 4, 5, 10, 15, 20, 30, 50, 70, lev};
            }
            else
            {
                result.levList = new List<int>() {1, 2, 3, 4, 5, 10, 15, 20, 30, 50, 70, 100, lev};
            }

            //default fx rate for client calculation
            if (prodDef.Ccy2 != "USD")
            {
                var fxProdDef =
                    cache.ProdDefs.FirstOrDefault(
                        o => o.Symbol == prodDef.Ccy2 + "USD" && o.Name.EndsWith(" Outright"));

                if (fxProdDef == null)
                {
                    fxProdDef =
                        cache.ProdDefs.FirstOrDefault(
                            o => o.Symbol == "USD" + prodDef.Ccy2 && o.Name.EndsWith(" Outright"));
                }

                if (fxProdDef != null)
                {
                    var fx = new SecurityDTO();
                    fx.id = fxProdDef.Id;
                    fx.symbol = fxProdDef.Symbol;

                    var fxQuote = cache.Quotes.FirstOrDefault(o => o.Id == fx.id);
                    if (fxQuote != null)
                    {
                        fx.ask = fxQuote.Offer;
                        fx.bid = fxQuote.Bid;
                        fx.last = Quotes.GetLastPrice(fxQuote);
                    }

                    result.fxOutright = fx;
                }
            }

            return result;
        }

        [HttpPost]
        [Route("bookmark")]
        [Route("live/bookmark")]
        [BasicAuth]
        public ResultDTO AddBookmark(string securityIds)
        {
            var ids = securityIds.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct().ToList();

            var idsExistingProducts = WebCache.GetInstance(IsLiveUrl).ProdDefs.Where(o => ids.Contains(o.Id)).Select(o => o.Id).ToList();

            var securityService = new SecurityService(db);
            securityService.PrependBookmarks(UserId, idsExistingProducts, IsLiveUrl);

            return new ResultDTO {success = true};
        }

        [HttpPut]
        [Route("bookmark")]
        [Route("live/bookmark")]
        [BasicAuth]
        public ResultDTO ResetBookmark(string securityIds)
        {
            if (securityIds == null)
                securityIds = string.Empty;

            var ids = securityIds.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct().ToList();

            var securityService = new SecurityService(db);
            securityService.DeleteBookmarks(UserId, IsLiveUrl);
            securityService.AppendBookmarks(UserId, ids, IsLiveUrl);

            //delete stock alerts NOT IN id list
            if (IsLiveUrl)
                db.UserAlert_Live.Where(o => o.UserId == UserId && !ids.Contains(o.SecurityId)).Delete();
            else
                db.UserAlerts.Where(o => o.UserId == UserId && !ids.Contains(o.SecurityId)).Delete();

            return new ResultDTO {success = true};
        }

        [HttpDelete]
        [Route("bookmark")]
        [Route("live/bookmark")]
        [BasicAuth]
        public ResultDTO DeleteBookmark(string securityIds)
        {
            var ids = securityIds.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct().ToList();

            var securityService = new SecurityService(db);
            securityService.DeleteBookmarks(UserId, ids, IsLiveUrl);

            //delete stock alerts IN id list
            if (IsLiveUrl)
                db.UserAlert_Live.Where(o => o.UserId == UserId && ids.Contains(o.SecurityId)).Delete();
            else
                db.UserAlerts.Where(o => o.UserId == UserId && ids.Contains(o.SecurityId)).Delete();

            return new ResultDTO {success = true};
        }

        [HttpGet]
        [Route("byPopularity")]
        [Route("live/byPopularity")]
        public List<ByPopularityDTO> GetByPopularity()
        {
            var activeProd = GetActiveProds(IsLiveUrl);

            var ts1day = TimeSpan.FromDays(1);
            var dtEnd = DateTime.UtcNow;
            var dtStart = DateTime.UtcNow - ts1day;

            var tradeHistory = new List<NewPositionHistoryBase>();
            for (int i = 0; i < 10; i++)
            {
                tradeHistory =IsLiveUrl
                    ? db.NewPositionHistory_live.AsNoTracking().Where(o => o.CreateTime >= dtStart && o.CreateTime < dtEnd).ToList().Select(o=>o as NewPositionHistoryBase).ToList()
                    :db.NewPositionHistories.AsNoTracking().Where(o => o.CreateTime >= dtStart && o.CreateTime < dtEnd).ToList().Select(o => o as NewPositionHistoryBase).ToList(); // >= start and < end
                    
                //trade history list covers more than 3 active securities
                if (tradeHistory.Select(o => o.SecurityId).Distinct().Count(o => activeProd.Any(p => p.Id == o)) >= 3)
                    break;

                //back 1 day
                dtStart = dtStart - ts1day;
                //dtEnd = dtEnd - ts1day;
            }

            //var period = TimeSpan.FromDays(1);

            //var dtStart = DateTime.UtcNow-period;
            //var tradeHistory = db.NewPositionHistories.AsNoTracking().Where(o => o.CreateTime >= dtStart).ToList();

            ////if no data in the latest period
            //if (tradeHistory.Count == 0)
            //{
            //    var lastTrade =
            //        db.NewPositionHistories.AsNoTracking().OrderByDescending(o => o.CreateTime).FirstOrDefault();

            //    if(lastTrade==null)
            //        return new List<ByPopularityDTO>();

            //    dtStart = lastTrade.CreateTime.Value-period;

            //    tradeHistory = db.NewPositionHistories.AsNoTracking().Where(o => o.CreateTime >= dtStart).ToList();
            //}

            var result =
                tradeHistory.GroupBy(o => o.SecurityId)
                .Where(o => activeProd.Any(p => p.Id == o.Key))//active products
                .Select(o =>
                {
                    var secId = o.Key.Value;
                    var prodDef = activeProd.FirstOrDefault(p => p.Id == secId);

                    return new ByPopularityDTO()
                    {
                        id = secId,
                        longCount = o.Count(p => p.LongQty.HasValue),
                        shortCount = o.Count(p => p.ShortQty.HasValue),
                        userCount = o.Select(p => p.UserId).Distinct().Count(),

                        symbol = prodDef?.Symbol,
                        name = prodDef != null ? Translator.GetCName(prodDef.Name) : null,
                    };
                })
                    .OrderByDescending(o => o.userCount)
                    .Take(20)
                    .ToList();

            //int maxCount = 7;//max loop count
            //while(result.Count < 3 && maxCount >0) //return at least Top 3 popular securities
            //{
            //    maxCount--;
            //    int difference = 3 - result.Count;
            //    List<int> secIDs = result.Select(o => o.id).ToList();

            //    //trace back for one more day
            //   var dtEnd = dtStart;
            //    dtStart = dtStart - period;

            //    //securities exist in current result should be excluded from query
            //    tradeHistory = db.NewPositionHistories.AsNoTracking().Where(o => o.CreateTime >= dtStart && o.CreateTime <= dtEnd
            //    && !secIDs.Contains(o.SecurityId.Value)).ToList();
            //    result.AddRange(tradeHistory.GroupBy(o => o.SecurityId).Select(o =>
            //    {
            //        var secId = o.Key.Value;
            //        var prodDef = activeProd.FirstOrDefault(p => p.Id == secId);
            //        return new ByPopularityDTO()
            //        {
            //            id = secId,
            //            longCount = o.Count(p => p.LongQty.HasValue),
            //            shortCount = o.Count(p => p.ShortQty.HasValue),
            //            userCount = o.Select(p => p.UserId).Distinct().Count(),

            //            symbol = prodDef?.Symbol,
            //            name = prodDef != null ? Translator.GetCName(prodDef.Name) : null,
            //        };
            //    }).OrderByDescending(o => o.userCount).Take(difference));
            //}

            return result;
        }

        [HttpGet]
        [Route("live/report")]
        [IPAuth]
        public List<ProdRankReportDTO> GetSecurityReport()
        {
            var cache = WebCache.GetInstance(true);

            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
            var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);

            var positions = db.NewPositionHistory_live.AsNoTracking().ToList();

            var result = positions.GroupBy(o => o.SecurityId).Select(o =>
            {
                var prodDef = cache.ProdDefs.FirstOrDefault(p => p.Id == o.Key);
                return new ProdRankReportDTO
                {
                    id = o.Key.Value,
                    symbol = prodDef.Symbol,
                    name = Translator.GetCName(prodDef.Name),
                        totalCount = o.Count(),
                };
            }).ToList();

            var activeProds = GetActiveProds(true).Where(o=>!o.Name.EndsWith(" Outright"));
            foreach (var activeProd in activeProds)
            {
                if(result.All(o => o.id != activeProd.Id))
                    result.Add(new ProdRankReportDTO
                    {
                        id=activeProd.Id,
                        symbol = activeProd.Symbol,
                        name=Translator.GetCName(activeProd.Name),
                    });
            }

            var monthCounts = positions.Where(o=>o.CreateTime>oneMonthAgo).GroupBy(o => o.SecurityId);
            foreach (var monthCount in monthCounts)
            {
                result.FirstOrDefault(o => o.id == monthCount.Key).monthCount = monthCount.Count();
            }

            var weekCounts = positions.Where(o => o.CreateTime > oneWeekAgo).GroupBy(o => o.SecurityId);
            foreach (var weekCount in weekCounts)
            {
                result.FirstOrDefault(o => o.id == weekCount.Key).weekCount = weekCount.Count();
            }

            return result.OrderByDescending(o=>o.totalCount).ToList();
        }
    }
}