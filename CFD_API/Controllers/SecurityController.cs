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

        public void UpdateStockInfo(IList<SecurityDTO> list)
        {
            if (list.Count == 0) return;

            var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();

            var ids = list.Select(o => o.id);
            var quotes = redisQuoteClient.GetByIds(ids);
            var prodDefs = redisProdDefClient.GetByIds(ids);

            foreach (var security in list)
            {
                var prodDef = prodDefs.FirstOrDefault(o => o.Id == security.id);
                if (prodDef != null)
                {
                    security.preClose = prodDef.PreClose;
                    security.open = prodDef.OpenAsk;
                    security.isOpen = prodDef.QuoteType == enmQuoteType.Open;
                }

                var quote = quotes.FirstOrDefault(o => o.Id == security.id);
                if (quote != null)
                {
                    security.last = quote.Offer;
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

            UpdateStockInfo(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("stock/topGainer")]
        public List<SecurityDTO> GetTopGainerList(int page = 1, int perPage = 20)
        {
            //var aliveIds = GetAliveStocks();

            var security =
                db.AyondoSecurities
                    .Where(o => o.AssetClass == "Single Stocks" && o.Financing == "US Stocks"
                                && o.CName != null && o.DefUpdatedAt != null
                    )
                    //.OrderBy(o => o.Symbol)
                    //.Skip((page - 1)*perPage).Take(perPage)
                    .ToList();
            var securityDtos = security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateStockInfo(securityDtos);

            securityDtos = securityDtos.OrderByDescending(o => o.last/o.preClose).Skip((page - 1)*perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("stock/topLoser")]
        public List<SecurityDTO> GetTopLoserList(int page = 1, int perPage = 20)
        {
//            var aliveIds = GetAliveStocks();

            var security =
                db.AyondoSecurities
//                    .Where(o => aliveIds.Contains(o.Id))
                    .Where(o => o.AssetClass == "Single Stocks" && o.Financing == "US Stocks"
                                && o.CName != null && o.DefUpdatedAt != null
                    )
                    //.OrderBy(o => o.Symbol)
                    //.Skip((page - 1)*perPage).Take(perPage)
                    .ToList();

            var securityDtos = security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateStockInfo(securityDtos);

            securityDtos = securityDtos.OrderBy(o => o.last/o.preClose).Skip((page - 1)*perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("index")]
        public List<SecurityDTO> GetIndexList(int page = 1, int perPage = 20)
        {
//            var aliveIds = GetAliveStocks();

            var security = db.AyondoSecurities
//                .Where(o => aliveIds.Contains(o.Id))
                .Where(o => o.AssetClass == "Stock Indices"
                            && o.CName != null && o.DefUpdatedAt != null
                )
                .OrderBy(o => o.Symbol)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            var securityDtos = security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateStockInfo(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("fx")]
        public List<SecurityDTO> GetFxList(int page = 1, int perPage = 20)
        {
//            var aliveIds = GetAliveStocks();

            var security = db.AyondoSecurities
//                .Where(o => aliveIds.Contains(o.Id))
                .Where(o => o.AssetClass == "Currencies"
                            && o.CName != null && o.DefUpdatedAt != null
                )
                .OrderBy(o => o.Symbol)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            var securityDtos = security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateStockInfo(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("futures")]
        public List<SecurityDTO> GetFuturesList(int page = 1, int perPage = 20)
        {
//            var aliveIds = GetAliveStocks();

            var security = db.AyondoSecurities
//                .Where(o => aliveIds.Contains(o.Id))
                .Where(o => o.AssetClass == "Commodities"
                            && o.CName != null && o.DefUpdatedAt != null)
                .OrderBy(o => o.Symbol)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            var securityDtos = security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateStockInfo(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("all")]
        public List<SecurityDTO> GetAllList(int page = 1, int perPage = 20)
        {
            var security = db.AyondoSecurities
                .Where(o => o.CName != null && o.DefUpdatedAt != null)
                .OrderBy(o => o.Symbol)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("search")]
        public List<SecurityDTO> SearchSecurity(string keyword, int page = 1, int perPage = 20)
        {
//            var aliveIds = GetAliveStocks();

            var security = db.AyondoSecurities
//                .Where(o => aliveIds.Contains(o.Id))
                .Where(o => //o.AssetClass != "Interest Rates" &&
                    (o.CName.Contains(keyword) || o.Symbol.Contains(keyword))
                    && o.CName != null && o.DefUpdatedAt != null
                    && (o.AssetClass != "Single Stocks" || o.Financing == "US Stocks")
                )
                .OrderBy(o => o.Symbol)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            var securityDtos = security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateStockInfo(securityDtos);

            return securityDtos;
        }

        [HttpGet]
        [Route("{securityId}")]
        public SecurityDetailDTO GetSecurity(int securityId)
        {
            var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == securityId);

            var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();

            var prodDef = redisProdDefClient.GetById(securityId);

            if (prodDef == null)
                return null;

            var result = Mapper.Map<SecurityDetailDTO>(prodDef);

            //get new price
            var quote = redisQuoteClient.GetById(securityId);
            result.last = quote.Offer;

            if (security != null)
            {
                //get cname
                if (security.CName != null)
                    result.name = security.CName;

                switch (security.AssetClass)
                {
                    case "Currencies":
                        decimal lotSizeInUSD;

                        if (security.BaseCcy == "USD")
                            lotSizeInUSD = security.LotSize.Value;
                        else
                        {
                            //get fxRate and convert product's lotSize from its BaseCcy to USD
                            //the fx for convertion! not the fx that is being bought!
                            var fxConverterProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == security.BaseCcy + "USD");

                            if (fxConverterProdDef == null)
                                throw new Exception("Cannot find fx rate: " + security.BaseCcy + "/" + "USD");

                            var fxConverterQuote = redisQuoteClient.GetById(fxConverterProdDef.Id);
                            var fxConverterRate = (fxConverterQuote.Bid + fxConverterQuote.Offer)/2;

                            lotSizeInUSD = security.LotSize.Value*fxConverterRate;
                        }

                        result.minValueLong = lotSizeInUSD*prodDef.MinSizeLong;
                        result.minValueShort = lotSizeInUSD*prodDef.MinSizeShort;
                        result.maxValueLong = lotSizeInUSD*prodDef.MaxSizeLong;
                        result.maxValueShort = lotSizeInUSD*prodDef.MaxSizeShort;
                        break;

                    case "Commodities":
                    case "Stock Indices":
                        if (security.PerUnitEquals == null || security.PerUnit == null)
                            throw new Exception("Cannot find PerUnit or PerUnitEquals for this security.");

                        var unitPrice = security.PerUnitEquals.Value/security.PerUnit.Value;
                        decimal unitPriceInUSD;
                        if (security.BaseCcy == "USD")
                            unitPriceInUSD = unitPrice;
                        else
                        {
                            var fxProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == security.BaseCcy + "USD");

                            if (fxProdDef == null)
                                throw new Exception("Cannot find fx rate: " + security.BaseCcy + "/" + "USD");

                            var fxRate = (fxProdDef.Offer + fxProdDef.Bid)/2;
                            unitPriceInUSD = unitPrice*fxRate.Value;
                        }

                        result.minValueLong = unitPriceInUSD*quote.Offer*prodDef.MinSizeLong;
                        result.minValueShort = unitPriceInUSD*quote.Bid*prodDef.MinSizeShort;
                        result.maxValueLong = unitPriceInUSD*quote.Offer*prodDef.MaxSizeLong;
                        result.maxValueShort = unitPriceInUSD*quote.Bid*prodDef.MaxSizeShort;
                        break;

                    case "Single Stocks":
                        decimal quoteOfferInUsd;
                        decimal quoteBidInUsd;
                        if (security.BaseCcy == "USD")
                        {
                            quoteOfferInUsd = quote.Offer;
                            quoteBidInUsd = quote.Bid;
                        }
                        else
                        {
                            var fxProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == security.BaseCcy + "USD");

                            if (fxProdDef == null)
                                throw new Exception("Cannot find fx rate: " + security.BaseCcy + "/" + "USD");

                            var fxRate = (fxProdDef.Offer + fxProdDef.Bid)/2;
                            quoteOfferInUsd = quote.Offer*fxRate.Value;
                            quoteBidInUsd = quote.Bid*fxRate.Value;
                        }
                        result.minValueLong = quoteOfferInUsd*prodDef.MinSizeLong;
                        result.minValueShort = quoteBidInUsd*prodDef.MinSizeShort;
                        result.maxValueLong = quoteOfferInUsd*prodDef.MaxSizeLong;
                        result.maxValueShort = quoteBidInUsd*prodDef.MaxSizeShort;
                        break;
                }
            }

            //demo data
            Random r = new Random();
            result.longPct = (decimal) r.NextDouble();

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