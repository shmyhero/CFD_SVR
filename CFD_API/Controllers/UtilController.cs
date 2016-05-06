using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;

namespace CFD_API.Controllers
{
    [RoutePrefix("api")]
    public class UtilController : CFDController
    {
        public UtilController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [Route("sendCode")]
        [HttpPost]
        //[RequireHttps]
        public ResultDTO SendCode(string phone)
        {
            var result = new ResultDTO();

            if (!Phone.IsValidPhoneNumber(phone))
            {
                result.message = __(TransKey.INVALID_PHONE_NUMBER);
                result.success = false;
                return result;
            }

            string code = string.Empty;

            ////send last code instead of regenerating if within ?
            //if (verifyCodes.Any())
            //{
            //    var lastCode = verifyCodes.OrderByDescending(c => c.CreatedAt).First();
            //}

            ////day limit
            //if (verifyCodes.Count() >= 5)
            //{
            //    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKeys.SEND_CODE_LIMIT)));
            //}

            var r = new Random();
            code = r.Next(10000).ToString("0000");

            //TODO: prevent from brute-force attack

            if (!string.IsNullOrWhiteSpace(code))
            {
                CFDGlobal.RetryMaxOrThrow(() => YunPianMessenger.TplSendCodeSms(string.Format("#code#={0}", code), phone), sleepMilliSeconds: 0);

                db.VerifyCodes.Add(new VerifyCode
                {
                    Code = code,
                    SentAt = DateTime.UtcNow,
                    Phone = phone
                });
                db.SaveChanges();
            }

            result.success = true;
            return result;
        }

        [Route("banner")]
        [HttpGet]
        public IList<BannerDTO> GetBanners()
        {
            var banners = db.Banners.OrderBy(o => o.Id).ToList();
            return banners.Select(o => Mapper.Map<BannerDTO>(o)).ToList();
        }

        [Route("feedback")]
        [HttpPost]
        public HttpResponseMessage NewFeedback(FeedbackFormDTO form)
        {
            if (form.text.Trim() == String.Empty)
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "no text");

            db.Feedbacks.Add(new Feedback()
            {
                Phone = form.phone,
                Text = form.text,
                Time = DateTime.UtcNow,
            });
            db.SaveChanges();
            return Request.CreateResponse(HttpStatusCode.OK);
        }
    }
}