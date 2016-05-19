using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using ServiceStack.Redis;

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

        [HttpGet]
        [Route("bookmark")]
        [BasicAuth]
        public List<SecurityDTO> GetBookmarkList(int page = 1, int perPage = 20)
        {
            var bookmarks = db.Bookmarks
                .Where(o => o.UserId == UserId)
                .Include(o => o.AyondoSecurity)
                .OrderBy(o => o.DisplayOrder)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            var securityDtos = bookmarks.Select(o => Mapper.Map<SecurityDTO>(o.AyondoSecurity)).ToList();

            UpdateProdDefQuote(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("byIds/{securityIds}")]
        public List<SecurityDTO> GetSecuritiesByIds(string securityIds)
        {
            if (securityIds == null)
                securityIds = string.Empty;

            var ids = securityIds.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct().ToList();

            var securities = db.AyondoSecurities.Where(o => ids.Contains(o.Id)).ToList();

            var securityDtos = securities.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateProdDefQuote(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("stock/topGainer")]
        public List<SecurityDTO> GetTopGainerList(int page = 1, int perPage = 20)
        {
            var all = RedisClient.As<ProdDef>().GetAll();

            var securities = db.AyondoSecurities.Where(o => o.Financing == "US Stocks" && o.CName != null).ToList();

            IList<ProdDef> prodDefs = new List<ProdDef>();
            foreach (var p in all)
            {
                var sec = securities.FirstOrDefault(s => s.Id == p.Id);
                if (sec != null)
                {
                    p.Name = sec.CName;
                    prodDefs.Add(p);
                }
            }

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            foreach (var o in securityDtos)
            {
                o.tag = "US";
            }

            UpdateQuote(securityDtos);

            securityDtos = securityDtos.OrderByDescending(o => o.last/o.preClose).Skip((page - 1)*perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("stock/topLoser")]
        public List<SecurityDTO> GetTopLoserList(int page = 1, int perPage = 20)
        {
            var all = RedisClient.As<ProdDef>().GetAll();

            var securities = db.AyondoSecurities.Where(o => o.Financing == "US Stocks" && o.CName != null).ToList();

            IList<ProdDef> prodDefs = new List<ProdDef>();
            foreach (var p in all)
            {
                var sec = securities.FirstOrDefault(s => s.Id == p.Id);
                if (sec != null)
                {
                    p.Name = sec.CName;
                    prodDefs.Add(p);
                }
            }

            var securityDtos = prodDefs.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            foreach (var o in securityDtos)
            {
                o.tag = "US";
            }

            UpdateQuote(securityDtos);

            securityDtos = securityDtos.OrderBy(o => o.last/o.preClose).Skip((page - 1)*perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("index")]
        public List<SecurityDTO> GetIndexList(int page = 1, int perPage = 20)
        {
            var all = RedisClient.As<ProdDef>().GetAll().Where(o => o.AssetClass == "Stock Indices");

            var securities = db.AyondoSecurities.Where(o => o.CName != null).ToList();

            IList<ProdDef> prodDefs = new List<ProdDef>();
            foreach (var p in all)
            {
                var sec = securities.FirstOrDefault(s => s.Id == p.Id);
                if (sec != null)
                {
                    p.Name = sec.CName;
                    prodDefs.Add(p);
                }
            }

            var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateQuote(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("fx")]
        public List<SecurityDTO> GetFxList(int page = 1, int perPage = 20)
        {
            var all = RedisClient.As<ProdDef>().GetAll().Where(o => o.AssetClass == "Currencies");

            var securities = db.AyondoSecurities.Where(o => o.CName != null).ToList();

            IList<ProdDef> prodDefs = new List<ProdDef>();
            foreach (var p in all)
            {
                var sec = securities.FirstOrDefault(s => s.Id == p.Id);
                if (sec != null)
                {
                    p.Name = sec.CName;
                    prodDefs.Add(p);
                }
            }

            var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateQuote(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("futures")]
        public List<SecurityDTO> GetFuturesList(int page = 1, int perPage = 20)
        {
            var all = RedisClient.As<ProdDef>().GetAll().Where(o => o.AssetClass == "Commodities");

            var securities = db.AyondoSecurities.Where(o => o.CName != null).ToList();

            IList<ProdDef> prodDefs = new List<ProdDef>();
            foreach (var p in all)
            {
                var sec = securities.FirstOrDefault(s => s.Id == p.Id);
                if (sec != null)
                {
                    p.Name = sec.CName;
                    prodDefs.Add(p);
                }
            }

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
        public List<SecurityDetail2DTO> GetAllList(int page = 1, int perPage = 20)
        {
            var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();
            var prodDefs = redisProdDefClient.GetAll();
            var quotes = redisQuoteClient.GetAll();

            var securities = db.AyondoSecurities.ToList();

            var result = prodDefs.Select(o => Mapper.Map<SecurityDetail2DTO>(o)).ToList();

            foreach (var secDTO in result)
            {
                //get cname
                var @default = securities.FirstOrDefault(o => o.Id == secDTO.id);
                if (@default != null && @default.CName != null)
                    secDTO.cname = @default.CName;

                //get new price
                var quote = quotes.FirstOrDefault(o => o.Id == secDTO.id);
                if (quote != null)
                {
                    secDTO.last = Quotes.GetLastPrice(quote);

                    //calculate min/max trade value
                    var prodDef = prodDefs.FirstOrDefault(o => o.Id == secDTO.id);

                    var perPriceCcy2 = prodDef.LotSize/prodDef.PLUnits;

                    decimal minLong = perPriceCcy2*quote.Offer*prodDef.MinSizeLong;
                    decimal minShort = perPriceCcy2*quote.Bid*prodDef.MinSizeShort;
                    decimal maxLong = perPriceCcy2*quote.Offer*prodDef.MaxSizeLong;
                    decimal maxShort = perPriceCcy2*quote.Bid*prodDef.MaxSizeShort;

                    try
                    {
                        var fxRate = FX.Convert(1, prodDef.Ccy2, "USD", prodDefs, quotes);

                        secDTO.minValueLong = minLong*fxRate;
                        secDTO.minValueShort = minShort*fxRate;
                        secDTO.maxValueLong = maxLong*fxRate;
                        secDTO.maxValueShort = maxShort*fxRate;
                    }
                    catch (Exception e)
                    {
                    }
                }
                else
                {
                    secDTO.last = null;
                }
            }

            return result;
        }

        [HttpGet]
        [Route("search")]
        public List<SecurityDTO> SearchSecurity(string keyword, int page = 1, int perPage = 20)
        {
            var all = RedisClient.As<ProdDef>().GetAll();

            var securities = db.AyondoSecurities.Where(o => o.CName != null).ToList();

            IList<ProdDef> prodDefs = new List<ProdDef>();
            foreach (var p in all)
            {
                var sec = securities.FirstOrDefault(s => s.Id == p.Id);
                if (sec != null)
                {
                    if (p.AssetClass == "Single Stocks" && sec.Financing != "US Stocks") //for stocks, only US stocks
                        continue;

                    if (!p.Symbol.ToLower().Contains(keyword.ToLower()) && !sec.CName.Contains(keyword)) //search keyword
                        continue;

                    p.Name = sec.CName;
                    prodDefs.Add(p);
                }
            }

            var securityDtos = prodDefs.OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

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

            var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == securityId);
            //get cname
            if (security != null && security.CName != null)
                result.name = security.CName;

            //get new price
            var quote = redisQuoteClient.GetById(securityId);
            result.last = Quotes.GetLastPrice(quote);

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


            var lev = (int) prodDef.MaxLeverage;

            //1,2,5,10,15,20,50,100

            if (lev <= 10)
            {
                result.levList = Enumerable.Range(1, lev).ToList();
            }
            else if (lev <= 15)
            {
                result.levList = new List<int>() {1, 2, 5, 10, lev};
            }
            else if (lev <= 20)
            {
                result.levList = new List<int>() {1, 2, 5, 10, 15, lev};
            }
            else if (lev <= 50)
            {
                result.levList = new List<int>() {1, 2, 5, 10, 15, 20, lev};
            }
            else if (lev <= 100)
            {
                result.levList = new List<int>() {1, 2, 5, 10, 15, 20, 50, lev};
            }
            else
            {
                result.levList = new List<int>() {1, 2, 5, 10, 15, 20, 50, 100, lev};
            }

            return result;
        }

        [HttpPost]
        [Route("bookmark")]
        [BasicAuth]
        public ResultDTO AddBookmark(string securityIds)
        {
            var ids = securityIds.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct().ToList();

            var securityService = new SecurityService(db);
            securityService.AddBookmarks(UserId, ids);

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