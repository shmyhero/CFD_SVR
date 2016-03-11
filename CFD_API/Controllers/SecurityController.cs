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
using CFD_JOBS.Models;
using ServiceStack.Redis;
using ServiceStack.Redis.Generic;

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

            quotes = quotes.Where(o => DateTime.UtcNow - o.Time < TimeSpan.FromHours(1)).ToList();

            return quotes.Select(o=>Convert.ToInt32(o.Id)).ToList();
        }

        [HttpGet]
        [Route("bookmark")]
        [BasicAuth]
        public List<SecurityDTO> GetBookmarkList(int page=1, int perPage=20)
        {
            var bookmarks = db.Bookmarks
                .Where(o => o.UserId == UserId)
                .Include(o => o.AyondoSecurity)
                .OrderBy(o => o.CreatedAt)
                .Skip((page - 1)*perPage).Take(perPage).ToList();
            return bookmarks.Select(o => Mapper.Map<SecurityDTO>(o.AyondoSecurity)).ToList();
        }

        [HttpPost]
        [Route("bookmark")]
        [BasicAuth]
        public ResultDTO SetBookmark(int securityId)
        {
            if (!db.AyondoSecurities.Any(o => o.Id == securityId))
                return new ResultDTO {success = false, message = "security not exist"};

            if (!db.Bookmarks.Any(o => o.UserId == UserId && o.AyondoSecurityId == securityId))
            {
                db.Bookmarks.Add(new Bookmark
                {
                    UserId = UserId,
                    AyondoSecurityId = securityId,
                    CreatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            return new ResultDTO {success = true};
        }

        [HttpDelete]
        [Route("bookmark")]
        [BasicAuth]
        public ResultDTO DeleteBookmark(int securityId)
        {
            if (!db.AyondoSecurities.Any(o => o.Id == securityId))
                return new ResultDTO {success = false, message = "security not exist"};

            var bookmark = db.Bookmarks.FirstOrDefault(o => o.UserId == UserId && o.AyondoSecurityId == securityId);
            if (bookmark != null)
            {
                db.Bookmarks.Remove(bookmark);
                db.SaveChanges();
            }

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
                .Skip((page - 1) * perPage).Take(perPage).ToList();
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
                .Skip((page - 1) * perPage).Take(perPage).ToList();
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
                .Where(o => o.AssetClass == "Indices")
                .OrderBy(o => o.Symbol)
                .Skip((page - 1) * perPage).Take(perPage).ToList();
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