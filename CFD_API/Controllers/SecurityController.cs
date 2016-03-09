using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;

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

        [HttpGet]
        [Route("bookmark")]
        [BasicAuth]
        public List<SecurityDTO> GetBookmarkList(int page, int perPage)
        {
            var bookmarks = db.Bookmarks.Where(o => o.UserId == UserId).Include(o => o.AyondoSecurity).OrderBy(o => o.CreatedAt).Skip((page - 1)*perPage).Take(perPage).ToList();
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
        public List<SecurityDTO> GetTopGainerList(int page, int perPage)
        {
            var security =
                db.AyondoSecurities.Where(o => o.AssetClass == "Single Stocks" && o.Financing == "US Stocks").OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("stock/topLoser")]
        public List<SecurityDTO> GetTopLoserList(int page, int perPage)
        {
            var security =
                db.AyondoSecurities.Where(o => o.AssetClass == "Single Stocks" && o.Financing == "US Stocks").OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("stock/trend")]
        public List<SecurityDTO> GetTrendList(int page, int perPage)
        {
            var security =
                db.AyondoSecurities.Where(o => o.AssetClass == "Single Stocks" && o.Financing == "US Stocks").OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("index")]
        public List<SecurityDTO> GetIndexList(int page, int perPage)
        {
            var security = db.AyondoSecurities.Where(o => o.AssetClass == "Indices").OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("fx")]
        public List<SecurityDTO> GetFxList(int page, int perPage)
        {
            var security = db.AyondoSecurities.Where(o => o.AssetClass == "Currencies").OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("futures")]
        public List<SecurityDTO> GetFuturesList(int page, int perPage)
        {
            var security = db.AyondoSecurities.Where(o => o.AssetClass == "Commodities").OrderBy(o => o.Symbol).Skip((page - 1)*perPage).Take(perPage).ToList();
            return security.Select(o => Mapper.Map<SecurityDTO>(o)).ToList();
        }
    }
}