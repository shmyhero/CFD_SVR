using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.DTO.Form;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;

namespace CFD_API.Controllers
{
    public class UserController : CFDController
    {
        public UserController(CFDEntities db) : base(db)
        {
        }

        [HttpPost]
        //[RequireHttps]
        [ActionName("signupByPhone")]
        public SignupDTO SignupByPhone(SignupByPhoneFormDTO form)
        {
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > fiveMinutesAgo);

            var result=new SignupDTO();

            if (verifyCodes.Any())
            {
                result.success = true;
            }
            else
            {
                result.success = false;
            }

            return result;
        }

        ////[HttpPost]
        ////[RequireHttps]
        //[ActionName("signupByWechat")]
        //public UserDTO SignupByWeChat(LoginFormDTO userInfo)
        //{
        //    return new UserDTO();
        //}

        //[HttpPost]
        ////[RequireHttps]
        //[ActionName("login")]
        //public UserDTO Login()
        //{
        //    return new UserDTO();
        //}
    }
}
