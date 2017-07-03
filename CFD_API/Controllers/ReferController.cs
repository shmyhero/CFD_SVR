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
using System.Text.RegularExpressions;

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
        [Route("add/{userId}/{phone}/{verifyCode}")]
        public ResultDTO Add(int userId, string phone, string verifyCode)
        {
            if(string.IsNullOrEmpty(phone))
            {
                return new ResultDTO() { success = false, message = "手机号为空" };
            }

            if(db.ReferHistorys.Any(o=>o.ApplicantNumber == phone))
            { 
                return new ResultDTO() { success = false, message = "该手机号已被邀请过哟！" };
            }

            var misc = db.Miscs.FirstOrDefault(m => m.Key == "PhoneRegex");
            if(misc != null)
            {
                Regex regex = new Regex(misc.Value);
                if(!regex.IsMatch(phone))
                {
                    return new ResultDTO() { success = false, message = "手机号格式不正确" };
                }
            }

            var dtValidSince = DateTime.UtcNow.AddHours(-1);
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == phone && o.Code == verifyCode && o.SentAt > dtValidSince);
            if (string.IsNullOrEmpty(verifyCode) || !verifyCodes.Any())
            {
                return new ResultDTO() { success = false, message = "输入的验证码不正确" };
            }

            db.ReferHistorys.Add(new ReferHistory() { RefereeID = userId, ApplicantNumber = phone, CreatedAt = DateTime.UtcNow });
            db.SaveChanges();

            return new ResultDTO() { success = true };
        }

        [HttpGet]
        [Route("{userId}")]
        public List<ReferDTO> GetAll(int userId)
        {
            var query = from rh in db.ReferHistorys
                        join u in db.Users on rh.RefereeID equals u.Id
                        join u2 in db.Users on rh.ApplicantNumber equals u2.Phone
                        where rh.RefereeID == userId
                        select new ReferDTO () { picUrl = string.IsNullOrEmpty(u2.PicUrl)? string.Empty : u2.PicUrl, nickName = u2.Nickname, amount=30 };

            var result = query.ToList();

            return result;
        }
    }
}
