using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using CFD_API.Controllers.Attributes;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;

namespace CFD_API.Controllers
{
    public class UtilController : CFDController
    {
        public UtilController(CFDEntities db) : base(db)
        {
        }

        [ActionName("sendCode")]
        [HttpPost]
        //[RequireHttps]
        public HttpResponseMessage SendCode(string phone)
        {
            if (!Phone.IsValidPhoneNumber(phone))
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKeys.INVALID_PHONE_NUMBER)));

            ////if (db.Users.Any(u => u.phoneNumber == phoneNumber))
            ////    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, this.LocalizedString(ApiStringKeys.ERR_biz_EXISTING_PHONE_NUMBER)));

            //string code = null;
            var oneDayAgo = DateTime.UtcNow.AddDays(-1);
            var verifyCodes = db.VerifyCodes.Where(c => c.Phone == phone && c.SentAt > oneDayAgo);

            //if (!verifyCodes.Any())
            //{
            //    // No existing verify code within one day, generate one
            //    var ran = new Random((int)(DateTime.UtcNow.Ticks & 0xffffffffL));
            //    code = string.Format("{0:0000}", ran.Next(10000));
            //}

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

            //else if (verifyCodes.Count() < 3)
            //{
            //    // Use newest verify code if exists
            //    var verifyCode = verifyCodes.OrderByDescending(c => c.dateTimeUtc).First();
            //    // only if it's sent more than one minute ago
            //    if (DateTime.UtcNow.Subtract(verifyCode.dateTimeUtc) > TimeSpan.FromMinutes(1))
            //        code = verifyCode.code;
            //}

            if (!string.IsNullOrWhiteSpace(code))
            {
                CFDGlobal.RetryMaxOrThrow(() => YunPianMessenger.TplSendCodeSms(string.Format("#code#={0}", code), phone), sleepSeconds: 0);

                db.VerifyCodes.Add(new VerifyCode
                {
                    Code = code,
                    SentAt = DateTime.UtcNow,
                    Phone = phone
                });
                db.SaveChanges();
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }
    }
}
