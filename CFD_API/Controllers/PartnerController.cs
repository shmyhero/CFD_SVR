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
        private string[] codeArray = new string[62] { "0","1","2","3","4","5","6","7","8","9","a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"};
        /// <summary>
        /// 一级合伙人推荐码长度为3
        /// </summary>
        private int FirstLevelCodeLength = 3;
        /// <summary>
        /// 二级合伙人推荐码长度为5
        /// </summary>
        private int SecondLevelCodeLength = 5;

        public PartnerController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [HttpPost]
        [Route("login")]
        public PartnerDTO Login(PartnerLoginDTO form)
        {
            PartnerDTO dto = new PartnerDTO();
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
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "验证码错误"));
            }
            return dto;
        }

        [HttpPost]
        [Route("signup")]
        public ResultDTO SignUp(PartnerSignUpDTO form)
        {
            var dtValidSince = DateTime.UtcNow - VERIFY_CODE_PERIOD;
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince);
            //传入的推荐码只能是一级或二级合伙人
            //以传入的推荐码作为上级推荐人，生成下级推荐码
            if(!string.IsNullOrEmpty(form.promotionCode) && form.promotionCode.Length !=FirstLevelCodeLength && form.promotionCode.Length != SecondLevelCodeLength)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "推荐码错误"));
            }

            //如果该手机号已经通过App注册过推荐码，并且推荐码和合伙人的不一致，就返回异常
            if(!string.IsNullOrEmpty(form.promotionCode))
            {
                var user = db.Users.FirstOrDefault(u => u.Phone == form.phone);
                if(user!=null && !string.IsNullOrEmpty(user.PromotionCode) && user.PromotionCode != form.promotionCode)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "与App注册的推荐码不一致"));
                }
            }

            if (verifyCodes.Any())
            {
                var partner = db.Partners.FirstOrDefault(p => p.Phone == form.phone);
                if (partner == null)
                {
                    partner = Mapper.Map<Partner>(form);
                    partner.CreatedAt = DateTime.UtcNow;
                    partner.ParentCode = form.promotionCode;
                    partner.PromotionCode = GetSubPromotionCode(form.promotionCode);

                    int count = 0;
                    while(db.Partners.Any(p=>p.PromotionCode == partner.PromotionCode) && count <= 20)
                    {
                        count++;
                        if (count == 20)
                        {
                            throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "推荐码创建失败"));
                        }
                        partner.PromotionCode = GetSubPromotionCode(form.promotionCode);
                    }

                    db.Partners.Add(partner);
                    db.SaveChanges();
                }
                else
                {
                    return new ResultDTO() { success = false, message="该手机号已注册过合作人" };
                }
            }
            else
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "验证码错误"));
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
            if(string.IsNullOrEmpty(parentCode))
            {
                codeLength = 3;
            }
            else
            {
                switch(parentCode.Length)
                {
                    case 3: codeLength = 5;break;
                    case 5: codeLength = 7; break;
                    default: codeLength = 3;break;
                }
            }

            subCode = parentCode;
            for (int x=0; x<codeLength - parentCode.Length; x++)
            {
                //0~9, a~z, A~Z, 共62个
                int seed = DateTime.Now.Millisecond;
                int index = new Random(seed).Next(0, 61);
                subCode += codeArray[index];
            }

            return subCode;
        }
    }
}