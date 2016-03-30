using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON;
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
            : base(db, mapper,redisClient)
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
                    security.open=prodDef.OpenAsk;
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

            securityDtos = securityDtos.OrderByDescending(o => o.last / o.preClose).Skip((page - 1) * perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("stock/topGainer")]
        public List<SecurityDTO> GetTopGainerList(int page = 1, int perPage = 20)
        {
            //var aliveIds = GetAliveStocks();

            var security =
                db.AyondoSecurities
                    .Where(o => o.AssetClass == "Single Stocks" && o.Financing == "US Stocks" && o.CName != null)
                    //.OrderBy(o => o.Symbol)
                    //.Skip((page - 1)*perPage).Take(perPage)
                    .ToList();
            var securityDtos = security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateStockInfo(securityDtos);

            securityDtos = securityDtos.OrderByDescending(o => o.last / o.preClose).Skip((page - 1) * perPage).Take(perPage).ToList();

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
                    .Where(o => o.AssetClass == "Single Stocks" && o.Financing == "US Stocks" && o.CName != null)
                    //.OrderBy(o => o.Symbol)
                    //.Skip((page - 1)*perPage).Take(perPage)
                    .ToList();
            var securityDtos = security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();

            UpdateStockInfo(securityDtos);

            securityDtos = securityDtos.OrderByDescending(o => o.last / o.preClose).Skip((page - 1) * perPage).Take(perPage).ToList();

            return securityDtos;
        }

        [HttpGet]
        [Route("index")]
        public List<SecurityDTO> GetIndexList(int page = 1, int perPage = 20)
        {
//            var aliveIds = GetAliveStocks();

            var security = db.AyondoSecurities
//                .Where(o => aliveIds.Contains(o.Id))
                .Where(o => o.AssetClass == "Stock Indices" && o.CName != null)
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
                .Where(o => o.AssetClass == "Currencies" && o.CName != null)
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
                .Where(o => o.AssetClass == "Commodities" && o.CName != null)
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
                .Where(o => o.CName != null)
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
                    (o.CName.Contains(keyword) || o.Symbol.Contains(keyword)) &&
                    o.CName != null)
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
            var sec = db.AyondoSecurities.FirstOrDefault(o => o.Id == securityId);

            var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();

            var prodDef = redisProdDefClient.GetById(securityId);

            if (prodDef == null)
                return null;

            var result = Mapper.Map<SecurityDetailDTO>(prodDef);

            //get new price
            var quote = redisQuoteClient.GetById(securityId);
            result.last = quote.Offer;

            //get cname
            if (sec != null && sec.CName != null)
                result.name = sec.CName;

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
            var ids = securityIds.Split(',').Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct();

            var securityService = new SecurityService(db);
            securityService.AddBookmarks(UserId, ids);

            return new ResultDTO { success = true };
        }

        [HttpPut]
        [Route("bookmark")]
        [BasicAuth]
        public ResultDTO ResetBookmark(string securityIds)
        {
            var ids = securityIds.Split(',').Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct();

            var securityService = new SecurityService(db);
            securityService.DeleteBookmarks(UserId, ids);
            securityService.AddBookmarks(UserId, ids);

            return new ResultDTO { success = true };
        }

        [HttpDelete]
        [Route("bookmark")]
        [BasicAuth]
        public ResultDTO DeleteBookmark(string securityIds)
        {
            var ids = securityIds.Split(',').Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct();

            var securityService = new SecurityService(db);
            securityService.DeleteBookmarks(UserId, ids);

            return new ResultDTO { success = true };
        }
    }
}