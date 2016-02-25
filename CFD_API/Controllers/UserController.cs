using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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
            var dtValidSince = DateTime.UtcNow.AddMinutes(-60);
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince);

            var result = new SignupResultDTO();

            if (verifyCodes.Any())
            {
                var user = db.Users.FirstOrDefault(o => o.Phone == form.phone);

                if (user == null) //phone doesn't exist
                {
                    var userService = new UserService(db);
                    userService.CreateUserByPhone(form.phone);

                    //refetch
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
        [ActionName("signupByWeChat")]
        public SignupResultDTO SignupByWeChat(SignupByWeChatFormDTO form)
        {
            var result = new SignupResultDTO();

            var user = db.Users.FirstOrDefault(o => o.WeChatOpenId == form.openid);

            if (user == null) //openid not exist
            {
                var userService = new UserService(db);
                userService.CreateUserByWeChat(form.openid, form.unionid);

                //refetch
                user = db.Users.FirstOrDefault(o => o.WeChatOpenId == form.openid);

                user.Nickname = form.nickname;
                //check duplicate nickname
                while (db.Users.Any(o => o.Id != user.Id && o.Nickname == user.Nickname))
                {
                    user.Nickname = form.nickname + (new Random().Next(10000));
                }

                //user.PicUrl

                db.SaveChanges();

                result.success = true;
                result.isNewUser = true;
                result.userId = user.Id;
                result.token = user.Token;
            }
            else //openid exists
            {
                result.success = true;
                result.isNewUser = false;
                result.userId = user.Id;
                result.token = user.Token;
            }

            return result;
        }

        [HttpGet]
        //[RequireHttps]
        [ActionName("me")]
        //[]
        public UserDTO GetMe(LoginFormDTO form)
        {
            //var user = db.Users.FirstOrDefault(o => o.Id == form.userId && o.Token == form.token);

            return new UserDTO {id = 1234, nickname = "test user"};
        }

        [HttpPost]
        //[RequireHttps]
        [ActionName("nickname")]
        //[]
        public HttpResponseMessage SetNickname(string nickname)
        {
            //var user = db.Users.FirstOrDefault(o => o.Id == form.userId && o.Token == form.token);

            return Request.CreateResponse(HttpStatusCode.OK);
        }
    }
}