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
using System.Text.RegularExpressions;

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
                partner.isAdmin = false;
                db.Partners.Add(partner);
                db.SaveChanges();
            }
            else
            {
                return new ResultDTO() { success = false, message="该手机号已注册过合作人" };
            }

            return new ResultDTO() { success = true };
        }

        [HttpGet]
        [Route("refer/{promotionCode}/{phone}/{verifyCode}")]
        public ResultDTO Refer(string promotionCode, string phone, string verifyCode)
        {
            #region 验证参数
            if(string.IsNullOrEmpty(promotionCode))
            {
                return new ResultDTO() { success = false, message = "缺少推荐码" };
            }

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(verifyCode))
            {
                return new ResultDTO() { success = false, message = "手机号为空/验证码" };
            }

            var misc = db.Miscs.FirstOrDefault(m => m.Key == "PhoneRegex");
            if (misc != null)
            {
                Regex regex = new Regex(misc.Value);
                if (!regex.IsMatch(phone))
                {
                    return new ResultDTO() { success = false, message = "手机号格式不正确" };
                }
            }

            if (db.PartnerReferHistorys.Any(o => o.FriendPhone == phone) || db.Users.Any(u=>u.Phone == phone))
            {
                return new ResultDTO() { success = false, message = "该手机号已被邀请/注册过哟！" };
            }

            var dtValidSince = DateTime.UtcNow.AddHours(-1);
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == phone && o.Code == verifyCode && o.SentAt > dtValidSince);
            if (string.IsNullOrEmpty(verifyCode) || !verifyCodes.Any())
            {
                return new ResultDTO() { success = false, message = "输入的验证码不正确" };
            }

            var partner = db.Partners.FirstOrDefault(p=>p.PromotionCode == promotionCode);
            if(partner == null)
            {
                return new ResultDTO() { success = false, message = "推荐码错误" };
            }
            #endregion

            #region 添加到合伙人好友推荐表
            db.PartnerReferHistorys.Add(new PartnerReferHistory() {  RefereePhone = partner.Phone, FriendPhone = phone, CreatedAt = DateTime.UtcNow });
            #endregion

            #region 模拟盘开户
            var userService = new UserService(db);
            userService.CreateUserByPhone(phone);

            var user = db.Users.FirstOrDefault(o => o.Phone == phone);

            var nickname = "u" + user.Id.ToString("000000");
            user.Nickname = nickname;

            //check duplicate nickname and generate random suffix
            int tryCount = 0;
            while (db.Users.Any(o => o.Id != user.Id && o.Nickname == user.Nickname))
            {
                user.Nickname = nickname.TruncateMax(4) + Randoms.GetRandomAlphabeticString(4);

                tryCount++;

                if (tryCount > 10)
                {
                    CFDGlobal.LogWarning("Tryout exceeded: signupByPhone - check duplicate nickname and generate random suffix");
                    break;
                }
            }

            user.PromotionCode = promotionCode;
            #endregion

            db.SaveChanges();
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
        public PartnerReportDTO GetPartnerReport(string partnerCode = "", string from = "", string to = "", string phone = "", int page = 1, int pageSize = 10)
        {
            IQueryable<PartnerView> query = db.PartnerViews;
            if (string.IsNullOrEmpty(partnerCode))
            {
                //get the level 1 partners
                query = query.Where(pv => pv.ParentCode == null && pv.RootCode == pv.PartnerCode);
            }
            else
            {
                //get the sub level partners
                query = query.Where(pv => pv.ParentCode == partnerCode);                  
            }
            //both from and to are provided..
            if ((string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) == false)
            {
                DateTime fromDate = DateTime.Parse(from);
                DateTime toDate = DateTime.Parse(to);
                query = query.Where(pv => pv.PartnerCreatedAt >= fromDate && pv.PartnerCreatedAt <= toDate);
            }
            //if the phone number is provided:
            if ((string.IsNullOrEmpty(phone)) == false)
            {
                query = query.Where(pv => pv.Phone == phone);
            }

            int count = query.Count();

            query = query.OrderByDescending(pv => pv.PartnerCreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize);

            List<PartnerReportRecordDTO>  records =  Mapper.Map<List<PartnerReportRecordDTO>>(query.ToList());
            return new PartnerReportDTO() { TotalCount = count, Records = records };
        }
       

        [HttpGet]
        [Route("userreport")]
        public PartnerUserReportDTO GetPartnerUserReport(string partnerCode = "", string from = "", string to = "", string phone = "", int page = 1, int pageSize = 10)
        {
            IQueryable<PartnerUserView> query = db.PartnerUserViews;
            //if partnerCode is provided
            if ((string.IsNullOrEmpty(partnerCode)) == false)            
            {
                //get users according partner code recursively;                  
                query = query.Where(puv => puv.PartnerCode.StartsWith(partnerCode));
            }

            //both from and to are provided..
            if ((string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) == false)
            {
                DateTime fromDate = DateTime.Parse(from);
                DateTime toDate = DateTime.Parse(to);
                query = query.Where(puv => puv.UserCreatedAt >= fromDate && puv.UserCreatedAt <= toDate);
            }
            //if the phone number is provided:
            if ((string.IsNullOrEmpty(phone)) == false)
            {
                query = query.Where(puv => puv.Phone == phone);
            }

            int count = query.Count();
            query = query.OrderByDescending(puv => puv.UserCreatedAt)
                    .Skip((page - 1) * pageSize).Take(pageSize);

            List<PartnerUserReportRecordDTO> records = Mapper.Map<List<PartnerUserReportRecordDTO>>(query.ToList());
            return new PartnerUserReportDTO() { TotalCount = count, Records = records };
        }

      
    }
}