using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using ServiceStack.Redis;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/refer")]
    public class ReferController : CFDController
    {
        public ReferController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }
        [HttpGet]
        [Route("add/{phone}")]
        [BasicAuth]
        public ResultDTO Add(string phone)
        {
            if(string.IsNullOrEmpty(phone))
            {
                return new ResultDTO() { success = false, message = "手机号为空" };
            }

            if(db.ReferHistorys.Any(o=>o.ApplicantNumber == phone))
            { 
                return new ResultDTO() { success = false, message = "该手机号已被推荐过" };
            }

            db.ReferHistorys.Add(new ReferHistory() { RefereeID = UserId, ApplicantNumber = phone, CreatedAt = DateTime.UtcNow });
            db.SaveChanges();

            return new ResultDTO() { success = true };
        }

        [HttpGet]
        [Route("")]
        [BasicAuth]
        public List<ReferDTO> GetAll()
        {
            var query = from rh in db.ReferHistorys
                        join u in db.Users on rh.RefereeID equals u.Id
                        join u2 in db.Users on rh.ApplicantNumber equals u2.Phone
                        where u.AyLiveAccountId.HasValue && rh.RefereeID == UserId
                        select new ReferDTO () { picUrl = u2.PicUrl, nickName = u2.Nickname, amount=30 };

            var result = query.ToList();

            return result;
        }
    }
}
