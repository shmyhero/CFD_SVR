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

            if (!string.IsNullOrEmpty(form.partnerCode) && (form.partnerCode.Length < FirstLevelCodeLength || form.partnerCode.Length > ThirdLevelCodeLength))
            {
                return new ResultDTO() { success = false, message = "推荐码格式错误" };
            }

            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince);
            if(!verifyCodes.Any())
                return new ResultDTO() { success = false, message = "验证码错误" };

            //传入的合伙人码只能是一级或二级合伙人
            //以传入的合伙人码作为上级合伙人，生成下级合伙人码
            if (!string.IsNullOrEmpty(form.partnerCode) && form.partnerCode.Length != FirstLevelCodeLength && form.partnerCode.Length != SecondLevelCodeLength)
            {
                return new ResultDTO() { success = false, message = "合伙人码格式错误" };
            }

            //获取上级合伙人
            Partner parentPartner = null;
            if(!string.IsNullOrEmpty(form.partnerCode))
            {
                parentPartner = db.Partners.FirstOrDefault(p => p.PartnerCode == form.partnerCode);
                if(parentPartner == null)
                {
                    return new ResultDTO() { success = false, message = "合伙人码不存在" };
                }
            }

            string partnerCode = GetSubPartnerCode(form.partnerCode);
            int count = 0;
            while (db.Partners.Any(p => p.PartnerCode == partnerCode) && count <= 20)
            {
                count++;
                if (count == 20)
                {
                    return new ResultDTO() { success = false, message = "推荐码创建失败" };
                }
                partnerCode = GetSubPartnerCode(form.partnerCode);
            }
            count = 0;
            string promotionCode = GetPromotionCode();
            while(db.Partners.Any(p => p.PartnerCode == partnerCode) && count <= 20)
            {
                count++;
                if (count == 20)
                {
                    return new ResultDTO() { success = false, message = "推荐码创建失败" };
                }
                partnerCode = GetSubPartnerCode(form.partnerCode);
            }
            

            //如果该手机号已经通过App注册过推荐码，并且推荐码和传入的上级合伙人的不一致，就返回异常
            if(!string.IsNullOrEmpty(form.partnerCode))
            {
                var user = db.Users.FirstOrDefault(u => u.Phone == form.phone);
                
                if(user!=null && !string.IsNullOrEmpty(user.PromotionCode) && user.PromotionCode != parentPartner.PromotionCode)
                {
                    return new ResultDTO() { success = false, message = "与App注册的推荐码不一致" };
                }

                //如果注册过App,但没有填过App的推荐码，就填上
                if (user != null && string.IsNullOrEmpty(user.PromotionCode))
                {
                    user.PromotionCode = promotionCode;
                    db.SaveChanges();
                }
            }
            
            var partner = db.Partners.FirstOrDefault(p => p.Phone == form.phone);
            if (partner == null)
            {
                partner = Mapper.Map<Partner>(form);
                partner.CreatedAt = DateTime.UtcNow;
                partner.RootCode = form.partnerCode.Substring(0, 3);
                partner.ParentCode = form.partnerCode;
                partner.PartnerCode = partnerCode;
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
        /// 根据传入的合伙人码，生成下级合伙人码
        /// </summary>
        /// <param name="parentCode"></param>
        /// <returns></returns>
        private string GetSubPartnerCode(string parentCode)
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
                switch (number % 2)
                {
                    case 0:
                        subCode += ((char)('0' + (char)(number % 10))).ToString();
                        break;
                    case 1:
                        subCode += ((char)('A' + (char)(number % 26))).ToString();
                        break;
                    default:
                        break;
                }
            }
            
            return subCode;
        }

        /// <summary>
        /// 生成4位的推荐码
        /// </summary>
        /// <returns></returns>
        private string GetPromotionCode()
        {
            string code = string.Empty;

            int number;
            Random random = new Random();
            for (int i = 0; i < 4; i++)
            {
                number = random.Next(100);
                switch (number % 2)
                {
                    case 0:
                        code += ((char)('0' + (char)(number % 10))).ToString();
                        break;
                    case 1:
                        code += ((char)('A' + (char)(number % 26))).ToString();
                        break;
                    default:
                        break;
                }
            }

            return code;
        }

        [HttpGet]
        [Route("report")]
        public PartnerReportDTO GetPartnerReport(string promotionCode = "", int page = 1, int pageSize = 10)
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
            int count = query.Count();

            query = query.OrderByDescending(pv => pv.PartnerCreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize);

            List<PartnerReportRecordDTO>  records =  Mapper.Map<List<PartnerReportRecordDTO>>(query.ToList());
            return new PartnerReportDTO() { TotalCount = count, Records = records };
        }

        [HttpGet]
        [Route("reportbydate")]
        public PartnerReportDTO GetPartnerReportByDate(string from, string to, int page = 1, int pageSize = 10)
        {                        
            DateTime fromDate = DateTime.Parse(from);
            DateTime toDate = DateTime.Parse(to);           
            var query = db.PartnerViews.Where(pv => pv.PartnerCreatedAt >= fromDate && pv.PartnerCreatedAt <= toDate);            
            int count = query.Count();
            query = query.OrderByDescending(pv => pv.PartnerCreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize);

            List<PartnerReportRecordDTO> records = Mapper.Map<List<PartnerReportRecordDTO>>(query.ToList());
            return new PartnerReportDTO() { TotalCount = count, Records = records };
        }

        [HttpGet]
        [Route("reportbyphone/{phone}")]
        public PartnerReportDTO GetPartnerReportByPhone(string phone)
        {           
            var query = db.PartnerViews.Where(pv => pv.Phone == phone);
            int count = query.Count();          
            List<PartnerReportRecordDTO> records = Mapper.Map<List<PartnerReportRecordDTO>>(query.ToList());
            return new PartnerReportDTO() { TotalCount = count, Records = records };
        }

        [HttpGet]
        [Route("userreport")]
        public PartnerUserReportDTO GetPartnerUserReport(string promotionCode = "", int page = 1, int pageSize = 10)
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
                query = db.PartnerUserViews.Where(puv => puv.PromotionCode.StartsWith(promotionCode));
            }
            int count = query.Count();
            query = query.OrderByDescending(pv => pv.UserCreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize);

            List<PartnerUserReportRecordDTO> records = Mapper.Map<List<PartnerUserReportRecordDTO>>(query.ToList());
            return new PartnerUserReportDTO() { TotalCount = count, Records = records };
        }

        [HttpGet]
        [Route("userreportbydate")]
        public PartnerUserReportDTO GetPartnerUserReportByDate(string from, string to, int page = 1, int pageSize = 10)
        {
            DateTime fromDate = DateTime.Parse(from);
            DateTime toDate = DateTime.Parse(to);
            var query = db.PartnerUserViews.Where(puv => puv.UserCreatedAt >= fromDate && puv.UserCreatedAt <= toDate);
            int count = query.Count();
            query = query.OrderByDescending(puv => puv.UserCreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize);

            List<PartnerUserReportRecordDTO> records = Mapper.Map<List<PartnerUserReportRecordDTO>>(query.ToList());
            return new PartnerUserReportDTO() { TotalCount = count, Records = records };
        }

        [HttpGet]
        [Route("userreportbyphone/{phone}")]
        public PartnerUserReportDTO GetPartneUserrReportByPhone(string phone)
        {
            var query = db.PartnerUserViews.Where(puv => puv.Phone == phone);
            int count = query.Count();
            List<PartnerUserReportRecordDTO> records = Mapper.Map<List<PartnerUserReportRecordDTO>>(query.ToList());
            return new PartnerUserReportDTO() { TotalCount = count, Records = records };
        }
    }
}