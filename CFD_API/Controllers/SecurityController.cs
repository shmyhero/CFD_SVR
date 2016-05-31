using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using ServiceStack.Redis;
using ServiceStack.Redis.Generic;

namespace CFD_API.Controllers
{
    //[BasicAuth]
    [RoutePrefix("api/security")]
    public class SecurityController : CFDController
    {
        public SecurityController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
            : base(db, mapper, redisClient)
        {
        }

        public void UpdateQuote(IList<SecurityDTO> list)
        {
            if (list.Count == 0) return;

            //var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();

            var ids = list.Select(o => o.id).ToList();
            var quotes = redisQuoteClient.GetByIds(ids);
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

        public void UpdateProdDefQuote(IList<SecurityDTO> list)
        {
            if (list.Count == 0) return;

            var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();

            var ids = list.Select(o => o.id).ToList();
            var quotes = redisQuoteClient.GetByIds(ids);
            var prodDefs = redisProdDefClient.GetByIds(ids);

            foreach (var security in list)
            {
                var prodDef = prodDefs.FirstOrDefault(o => o.Id == security.id);
                if (prodDef != null)
                {
                    if (security.name == null) security.name = prodDef.Name;
                    security.symbol = prodDef.Symbol;

                    security.preClose = prodDef.PreClose;
                    security.open = Quotes.GetOpenPrice(prodDef);
                    security.isOpen = prodDef.QuoteType == enmQuoteType.Open;
                }

                var quote = quotes.FirstOrDefault(o => o.Id == security.id);
                if (quote != null)
                {
                    security.last = Quotes.GetLastPrice(quote);
                }
            }
        }

        private IList<ProdDef> GetActiveProds()
        {
            return RedisClient.As<ProdDef>().GetAll()
                .Where(o => o.QuoteType != enmQuoteType.Inactive 
                    && (DateTime.UtcNow - o.Time) < CFDGlobal.PROD_DEF_ACTIVE_IF_TIME_NOT_OLDER_THAN_TS
                    )
                .ToList();
        }

        [HttpGet]
        [Route("bookmark")]
        [BasicAuth]
        public List<SecurityDTO> GetBookmarkList(int page = 1, int perPage = 20)
        {
            var bookmarkIDs = db.Bookmarks
                .Where(o => o.UserId == UserId)
                //.Include(o => o.AyondoSecurity)
                .OrderBy(o => o.DisplayOrder)
                .Skip((page - 1)*perPage).Take(perPage).Select(o => o.AyondoSecurityId).ToList();

            var prodDefs = RedisClient.As<ProdDef>().GetByIds(bookmarkIDs);

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateQuote(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("byIds/{securityIds}")]
        public List<SecurityDTO> GetSecuritiesByIds(string securityIds)
        {
            if (securityIds == null)
                securityIds = string.Empty;

            var ids = securityIds.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct().ToList();

            var redisTypedClient = RedisClient.As<ProdDef>();

            var prodDefs = redisTypedClient.GetByIds(ids);

            //var securities = db.AyondoSecurities.Where(o => ids.Contains(o.Id)).ToList();

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateQuote(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("stock/topGainer")]
        public List<SecurityDTO> GetTopGainerList(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds();

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_STOCK && Products.IsUsStocks(o.Symbol)).ToList();

            //Where(o => o.Financing == "US Stocks" 

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            //foreach (var o in securityDtos)
            //{
            //    o.tag = "US";
            //}

            UpdateQuote(securityDtos);

            securityDtos = securityDtos.OrderByDescending(o => o.last/o.preClose).Skip((page - 1)*perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("stock/topLoser")]
        public List<SecurityDTO> GetTopLoserList(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds();

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_STOCK && Products.IsUsStocks(o.Symbol)).ToList();

            //.Where(o => o.Financing == "US Stocks"

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            //foreach (var o in securityDtos)
            //{
            //    o.tag = "US";
            //}

            UpdateQuote(securityDtos);

            securityDtos = securityDtos.OrderBy(o => o.last/o.preClose).Skip((page - 1)*perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("index")]
        public List<SecurityDTO> GetIndexList(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds();

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_INDEX).ToList();

            var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateQuote(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("fx")]
        public List<SecurityDTO> GetFxList(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds();

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_FX).ToList();

            var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateQuote(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("futures")]
        public List<SecurityDTO> GetFuturesList(int page = 1, int perPage = 20)
        {
            var activeProds = GetActiveProds();

            var prodDefs = activeProds.Where(o => o.AssetClass == CFDGlobal.ASSET_CLASS_COMMODITY).ToList();

            var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateQuote(securityDtos);

            return securityDtos;
        }

        /// <summary>
        /// for test use only
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("all")]
        public List<ProdDefDTO> GetAllList(int page = 1, int perPage = 20)
        {
            //var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();
            var prodDefs = RedisClient.As<ProdDef>().GetAll();
            var quotes = redisQuoteClient.GetAll();

            //var securities = db.AyondoSecurities.ToList();

            var result = prodDefs.Select(o => Mapper.Map<ProdDefDTO>(o)).ToList();

            foreach (var prodDTO in result)
            {
                ////get cname
                //var @default = securities.FirstOrDefault(o => o.Id == prodDTO.Id);
                //if (@default != null && @default.CName != null)
                //    prodDTO.cname = @default.CName;
                prodDTO.cname = Translator.GetCName(prodDTO.Name);

                //get new price
                var quote = quotes.FirstOrDefault(o => o.Id == prodDTO.Id);
                if (quote != null)
                {
                    //prodDTO.last = Quotes.GetLastPrice(quote);

                    //calculate min/max trade value
                    var prodDef = prodDefs.FirstOrDefault(o => o.Id == prodDTO.Id);

                    var perPriceCcy2 = prodDef.LotSize/prodDef.PLUnits;

                    decimal minLong = perPriceCcy2*quote.Offer*prodDef.MinSizeLong;
                    decimal minShort = perPriceCcy2*quote.Bid*prodDef.MinSizeShort;
                    decimal maxLong = perPriceCcy2*quote.Offer*prodDef.MaxSizeLong;
                    decimal maxShort = perPriceCcy2*quote.Bid*prodDef.MaxSizeShort;

                    try
                    {
                        var fxRate = FX.Convert(1, prodDef.Ccy2, "USD", prodDefs, quotes);

                        prodDTO.minValueLong = minLong*fxRate;
                        prodDTO.minValueShort = minShort*fxRate;
                        prodDTO.maxValueLong = maxLong*fxRate;
                        prodDTO.maxValueShort = maxShort*fxRate;
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
        [Route("search")]
        public List<SecurityDTO> SearchSecurity(string keyword, int page = 1, int perPage = 20)
        {
            keyword = keyword.ToLower();

            var activeProds = GetActiveProds();

            //var securities = db.AyondoSecurities.Where(o => o.CName != null).ToList();

            var securityDtos = activeProds
                .Where(o => o.AssetClass != CFDGlobal.ASSET_CLASS_STOCK || Products.IsUsStocks(o.Symbol))
                .Select(o => Mapper.Map<SecurityDTO>(o)).Where(o => (o.name.ToLower().Contains(keyword) || o.symbol.ToLower().Contains(keyword)))
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

            UpdateQuote(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("{securityId}")]
        public SecurityDetailDTO GetSecurity(int securityId)
        {
            var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();

            var prodDef = redisProdDefClient.GetById(securityId);

            if (prodDef == null)
                return null;

            //mapping
            var result = Mapper.Map<SecurityDetailDTO>(prodDef);

            //var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == securityId);
            ////get cname
            //if (security != null && security.CName != null)
            //    result.name = security.CName;

            //get new price
            var quote = redisQuoteClient.GetById(securityId);
            result.last = Quotes.GetLastPrice(quote);
            result.ask = quote.Offer;
            result.bid = quote.Bid;

            //************************************************************************
            //TradeValue (to ccy2) = QuotePrice * (1 / MDS_PLUNITS * MDS_LOTSIZE) * quantity
            //************************************************************************

            var perPriceCcy2 = prodDef.LotSize/prodDef.PLUnits;

            decimal minLong = perPriceCcy2*quote.Offer*prodDef.MinSizeLong;
            decimal minShort = perPriceCcy2*quote.Bid*prodDef.MinSizeShort;
            decimal maxLong = perPriceCcy2*quote.Offer*prodDef.MaxSizeLong;
            decimal maxShort = perPriceCcy2*quote.Bid*prodDef.MaxSizeShort;

            var fxRate = FX.Convert(1, prodDef.Ccy2, "USD", RedisClient);

            result.minValueLong = minLong*fxRate;
            result.minValueShort = minShort*fxRate;
            result.maxValueLong = maxLong*fxRate;
            result.maxValueShort = maxShort*fxRate;

            //demo data
            Random r = new Random();
            result.longPct = (decimal) r.NextDouble();

            //for single stocks, reduct max lev for gsmd
            var lev = prodDef.AssetClass == CFDGlobal.ASSET_CLASS_STOCK ? (int) (prodDef.MaxLeverage/2) : (int) prodDef.MaxLeverage;

            //1,2,5,10,15,20,50,100

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

            return result;
        }

        [HttpPost]
        [Route("bookmark")]
        [BasicAuth]
        public ResultDTO AddBookmark(string securityIds)
        {
            var ids = securityIds.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct().ToList();

            var idsExistingProducts = RedisClient.As<ProdDef>().GetByIds(ids).Select(o=>o.Id).ToList();

            var securityService = new SecurityService(db);
            securityService.AddBookmarks(UserId, idsExistingProducts);

            return new ResultDTO {success = true};
        }

        [HttpPut]
        [Route("bookmark")]
        [BasicAuth]
        public ResultDTO ResetBookmark(string securityIds)
        {
            if (securityIds == null)
                securityIds = string.Empty;

            var ids = securityIds.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct().ToList();

            var securityService = new SecurityService(db);
            securityService.DeleteBookmarks(UserId);
            securityService.AddBookmarks(UserId, ids);

            return new ResultDTO {success = true};
        }

        [HttpDelete]
        [Route("bookmark")]
        [BasicAuth]
        public ResultDTO DeleteBookmark(string securityIds)
        {
            var ids = securityIds.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct().ToList();

            var securityService = new SecurityService(db);
            securityService.DeleteBookmarks(UserId, ids);

            return new ResultDTO {success = true};
        }
    }
}