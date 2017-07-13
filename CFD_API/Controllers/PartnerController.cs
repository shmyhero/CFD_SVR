using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using CFD_API.Caching;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.DTO.Form;
using CFD_COMMON;
using CFD_COMMON.Azure;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using Newtonsoft.Json.Linq;
using ServiceStack.Redis;
using System.Web;
using System.Drawing;
using System.ServiceModel;
using AyondoTrade.Model;
using CFD_COMMON.Utils.Extensions;
using System.Threading.Tasks;
using AyondoTrade.FaultModel;
using EntityFramework.Extensions;
using Newtonsoft.Json;
using ServiceStack.Text;
using System.Data.SqlTypes;
using CFD_COMMON.IdentityVerify;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/partner")]
    public class PartnerController : CFDController
    {
        private static readonly TimeSpan VERIFY_CODE_PERIOD = TimeSpan.FromHours(1);
        //private string[] codeArray = new string[62] { "0","1","2","3","4","5","6","7","8","9","a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"};
        /// <summary>
        /// 一级合伙人推荐码长度为3
        /// </summary>
        private int FirstLevelCodeLength = 3;
        /// <summary>
        /// 二级合伙人推荐码长度为5
        /// </summary>
        private int SecondLevelCodeLength = 5;
        private int ThirdLevelCodeLength = 7;

        public PartnerController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [HttpPost]
        [Route("login")]
        public PartnerDTO Login(PartnerLoginDTO form)
        {
            PartnerDTO dto = new PartnerDTO() { success = true };
            var dtValidSince = DateTime.UtcNow - VERIFY_CODE_PERIOD;
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince);
            if(verifyCodes.Any())
            {
                var partner = db.Partners.FirstOrDefault(p => p.Phone == form.phone);
                if(partner != null)
                {
                    dto = Mapper.Map<PartnerDTO>(partner);
                }
            }
            else
            {
                dto.success = false;
                dto.message = "验证码错误";
            }

            return dto;
        }

        [HttpPost]
        [Route("signup")]
        public ResultDTO SignUp(PartnerSignUpDTO form)
        {
            var dtValidSince = DateTime.UtcNow - VERIFY_CODE_PERIOD;

            if (!string.IsNullOrEmpty(form.promotionCode) && (form.promotionCode.Length < FirstLevelCodeLength || form.promotionCode.Length > ThirdLevelCodeLength))
            {
                return new ResultDTO() { success = false, message = "推荐码格式错误" };
            }

            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince);
            if(!verifyCodes.Any())
                return new ResultDTO() { success = false, message = "验证码错误" };

            string promotionCode = GetSubPromotionCode(form.promotionCode);
            int count = 0;
            while (db.Partners.Any(p => p.PromotionCode == promotionCode) && count <= 20)
            {
                count++;
                if (count == 20)
                {
                    return new ResultDTO() { success = false, message = "推荐码创建失败" };
                }
                promotionCode = GetSubPromotionCode(form.promotionCode);
            }
            
            //传入的推荐码只能是一级或二级合伙人
            //以传入的推荐码作为上级推荐人，生成下级推荐码
            if(!string.IsNullOrEmpty(form.promotionCode) && form.promotionCode.Length !=FirstLevelCodeLength && form.promotionCode.Length != SecondLevelCodeLength)
            {
                return new ResultDTO() { success = false, message = "推荐码错误" };
            }

            //如果该手机号已经通过App注册过推荐码，并且推荐码和合伙人的不一致，就返回异常
            if(!string.IsNullOrEmpty(form.promotionCode))
            {
                var user = db.Users.FirstOrDefault(u => u.Phone == form.phone);
                if(user!=null && !string.IsNullOrEmpty(user.PromotionCode) && user.PromotionCode != form.promotionCode)
                {
                    return new ResultDTO() { success = false, message = "与App注册的推荐码不一致" };
                }

                //如果注册过App,但没有填过App的推荐码，就填上
                //2，3级推荐人用上级的填。1级推荐人用自己的填
                if (user != null && string.IsNullOrEmpty(user.PromotionCode))
                {
                    if(!string.IsNullOrEmpty(form.promotionCode)) //2，3级用上级推荐码
                    {
                        user.PromotionCode = form.promotionCode;
                    }
                    else //1级用自己的
                    {
                        user.PromotionCode = promotionCode;
                    }
                    db.SaveChanges();
                }
            }

            
            var partner = db.Partners.FirstOrDefault(p => p.Phone == form.phone);
            if (partner == null)
            {
                partner = Mapper.Map<Partner>(form);
                partner.CreatedAt = DateTime.UtcNow;
                partner.RootCode = form.promotionCode.Substring(0, 3);
                partner.ParentCode = form.promotionCode;
                partner.PromotionCode = promotionCode;

                db.Partners.Add(partner);
                db.SaveChanges();
            }
            else
            {
                return new ResultDTO() { success = false, message="该手机号已注册过合作人" };
            }

            return new ResultDTO() { success = true };
        }

        /// <summary>
        /// 根据传入的推荐码，生成下级推荐码
        /// </summary>
        /// <param name="parentCode"></param>
        /// <returns></returns>
        private string GetSubPromotionCode(string parentCode)
        {
            int codeLength = 3;
            string subCode = string.Empty;
            if (string.IsNullOrEmpty(parentCode))
            {
                codeLength = 3;
            }
            else
            {
                switch (parentCode.Length)
                {
                    case 3: codeLength = 5; break;
                    case 5: codeLength = 7; break;
                    default: codeLength = 3; break;
                }
            }

            subCode = parentCode;

            int number;
            Random random = new Random();
            for (int i = 0; i < codeLength - parentCode.Length; i++)
            {
                number = random.Next(100);
                switch (number % 3)
                {
                    case 0:
                        subCode += ((char)('0' + (char)(number % 10))).ToString();
                        break;
                    case 1:
                        subCode += ((char)('a' + (char)(number % 26))).ToString();
                        break;
                    case 2:
                        subCode += ((char)('A' + (char)(number % 26))).ToString();
                        break;
                    default:
                        break;
                }
            }
            
            return subCode;
        }

        [HttpGet]
        [Route("report")]
        public List <PartnerReportDTO> GetPartnerReport(string promotionCode = "", int page = 1, int pageSize = 10)
        {
            IQueryable<PartnerView> query = null;
            if (string.IsNullOrEmpty(promotionCode))
            {
                //get the level 1 partners
                query = db.PartnerViews.Where(pv => pv.ParentCode == null && pv.RootCode == pv.PromotionCode);
            }
            else
            {
                //get the sub level partners
                query = db.PartnerViews.Where(pv => pv.ParentCode == promotionCode);
                  
            }
            query = query.OrderByDescending(pv => pv.PartnerCreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize);

            return Mapper.Map<List<PartnerReportDTO>>(query.ToList());
        }

        [HttpGet]
        [Route("userreport")]
        public List<PartnerUserReportDTO> GetPartnerUserReport(string promotionCode = "", int page = 1, int pageSize = 10)
        {
            IQueryable<PartnerUserView> query = null;
            if (string.IsNullOrEmpty(promotionCode))
            {
                //get all the users who have been promoted
                query = db.PartnerUserViews;
            }
            else
            {
                //get users according promotion code recursively;                  
                query = db.PartnerUserViews.Where(pv => pv.PromotionCode.StartsWith(promotionCode));
            }
            query = query.OrderByDescending(pv => pv.UserCreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize);

            return Mapper.Map<List<PartnerUserReportDTO>>(query.ToList());
        }
    }
}