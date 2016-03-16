﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using CFD_JOBS.Models;
using EntityFramework.Extensions;

namespace CFD_API.Controllers
{
    //[BasicAuth]
    [RoutePrefix("api/security")]
    public class SecurityController : CFDController
    {
        public SecurityController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }

        public List<int> GetAliveStocks()
        {
            var basicRedisClientManager = CFDGlobal.GetBasicRedisClientManager();
            var redisTypedClient = basicRedisClientManager.GetClient().As<Quote>();

            var quotes = redisTypedClient.GetAll();

            quotes = quotes.Where(o => DateTime.UtcNow - o.Time < TimeSpan.FromHours(24)).ToList();

            return quotes.Select(o => Convert.ToInt32(o.Id)).ToList();
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
            return bookmarks.Select(o => Mapper.Map<SecurityDTO>(o.AyondoSecurity)).ToList();
        }

        [HttpPost]
        [Route("bookmark")]
        [BasicAuth]
        public ResultDTO AddBookmark(string securityIds)
        {
            var ids = securityIds.Split(',').Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct();

            var securityService = new SecurityService(db);
            securityService.AddBookmarks(UserId, ids);

            return new ResultDTO {success = true};
        }

        [HttpPut]
        [Route("bookmark")]
        [BasicAuth]
        public ResultDTO ResetBookmark(string securityIds)
        {
            var ids = securityIds.Split(',').Select(o => Convert.ToInt32(o)).Where(o => o > 0).Distinct();

            var securityService = new SecurityService(db);
            securityService.DeleteBookmarks(UserId, ids);
            securityService.AddBookmarks(UserId,ids);

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

            return new ResultDTO {success = true};
        }

        [HttpGet]
        [Route("stock/topGainer")]
        public List<SecurityDTO> GetTopGainerList(int page = 1, int perPage = 20)
        {
            var aliveIds = GetAliveStocks();

            var security =
                db.AyondoSecurities
                    .Where(o => aliveIds.Contains(o.Id))
                    .Where(o => o.AssetClass == "Single Stocks" && o.Financing == "US Stocks")
                    .OrderBy(o => o.Symbol)
                    .Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("stock/topLoser")]
        public List<SecurityDTO> GetTopLoserList(int page = 1, int perPage = 20)
        {
            var aliveIds = GetAliveStocks();

            var security =
                db.AyondoSecurities
                    .Where(o => aliveIds.Contains(o.Id))
                    .Where(o => o.AssetClass == "Single Stocks" && o.Financing == "US Stocks")
                    .OrderBy(o => o.Symbol)
                    .Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("stock/trend")]
        public List<SecurityDTO> GetTrendList(int page = 1, int perPage = 20)
        {
            var aliveIds = GetAliveStocks();

            var security =
                db.AyondoSecurities
                    .Where(o => aliveIds.Contains(o.Id))
                    .Where(o => o.AssetClass == "Single Stocks" && o.Financing == "US Stocks")
                    .OrderBy(o => o.Symbol)
                    .Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("index")]
        public List<SecurityDTO> GetIndexList(int page = 1, int perPage = 20)
        {
            var aliveIds = GetAliveStocks();

            var security = db.AyondoSecurities
                .Where(o => aliveIds.Contains(o.Id))
                .Where(o => o.AssetClass == "Stock Indices")
                .OrderBy(o => o.Symbol)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("fx")]
        public List<SecurityDTO> GetFxList(int page = 1, int perPage = 20)
        {
            var aliveIds = GetAliveStocks();

            var security = db.AyondoSecurities
                .Where(o => aliveIds.Contains(o.Id))
                .Where(o => o.AssetClass == "Currencies")
                .OrderBy(o => o.Symbol)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("futures")]
        public List<SecurityDTO> GetFuturesList(int page = 1, int perPage = 20)
        {
            var aliveIds = GetAliveStocks();

            var security = db.AyondoSecurities
                .Where(o => aliveIds.Contains(o.Id))
                .Where(o => o.AssetClass == "Commodities")
                .OrderBy(o => o.Symbol)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("search")]
        public List<SecurityDTO> SearchSecurity(string keyword, int page = 1, int perPage = 20)
        {
            var aliveIds = GetAliveStocks();

            var security = db.AyondoSecurities
                .Where(o => aliveIds.Contains(o.Id))
                .Where(o => o.AssetClass != "Interest Rates" && (o.Name.Contains(keyword) || o.Symbol.Contains(keyword)))
                .OrderBy(o => o.Symbol)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }
    }
}