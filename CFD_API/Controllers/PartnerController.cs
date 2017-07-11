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
            if (verifyCodes.Any())
            {
                var partner = db.Partners.FirstOrDefault(p => p.Phone == form.phone);
                if (partner == null)
                {
                    partner = Mapper.Map<Partner>(form);
                    partner.CreatedAt = DateTime.UtcNow;
                    db.Partners.Add(partner);
                    db.SaveChanges();
                }
                else
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "该手机号已注册过合伙人"));
                }
            }

            return new ResultDTO() { success = true };
        }
    }
}