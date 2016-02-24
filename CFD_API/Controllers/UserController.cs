using System;
using System.Linq;
using System.Web.Http;
using CFD_API.DTO;
using CFD_API.DTO.Form;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Service;

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
        public SignupResultDTO SignupByPhone(SignupByPhoneFormDTO form)
        {
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > fiveMinutesAgo);

            var result = new SignupResultDTO();

            if (verifyCodes.Any())
            {
                var user = db.Users.FirstOrDefault(o => o.Phone == form.phone);

                if (user == null) //phone doesn't exist
                {
                    var userService = new UserService(db);
                    userService.CreateUserByPhone(form.phone);

                    user = db.Users.FirstOrDefault(o => o.Phone == form.phone);
                    user.Nickname = "u" + user.Id.ToString("00000000");
                    db.SaveChanges();

                    result.success = true;
                    result.isNewUser = true;
                    result.userId = user.Id;
                    result.token = user.Token;
                }
                else //phone exists
                {
                    result.success = true;
                    result.isNewUser = false;
                    result.userId = user.Id;
                    result.token = user.Token;
                }
            }
            else
            {
                result.success = false;
            }

            return result;
        }

        [HttpPost]
        //[RequireHttps]
        [ActionName("signupByWechat")]
        public SignupResultDTO SignupByWeChat(SignupByWeChatFormDTO form)
        {
            var result = new SignupResultDTO();

            //var user = db.Users.FirstOrDefault(o => o.Phone == form.phone);

            //if (user == null)//phone doesn't exist
            //{
            //    var userService = new UserService(db);
            //    userService.CreateUserByPhone(form.phone);

            //    user = db.Users.FirstOrDefault(o => o.Phone == form.phone);
            //    user.Nickname = "u" + user.Id.ToString("00000000");
            //    db.SaveChanges();

            //    result.success = true;
            //    result.isNewUser = true;
            //    result.token = user.Token;
            //}
            //else//phone exists
            //{
            //    result.success = true;
            //    result.isNewUser = false;
            //    result.token = user.Token;
            //}

            return result;
        }

        [HttpPost]
        //[RequireHttps]
        [ActionName("login")]
        public UserDTO Login(LoginFormDTO form)
        {
            var user = db.Users.FirstOrDefault(o => o.Id == form.userId && o.Token == form.token);

            return new UserDTO();
        }
    }
}