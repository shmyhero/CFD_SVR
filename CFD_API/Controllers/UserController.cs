using System;
using System.Linq;
using System.Net;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.DTO.Form;
using CFD_COMMON;
using CFD_COMMON.Azure;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Service;

namespace CFD_API.Controllers
{
    public class UserController : CFDController
    {
        public UserController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
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
                result.message = __(TransKey.INVALID_VERIFY_CODE);
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

                user.Nickname = form.nickname.Trim();

                //check duplicate nickname and generate random suffix
                while (db.Users.Any(o => o.Id != user.Id && o.Nickname == user.Nickname))
                {
                    user.Nickname = form.nickname + (new Random().Next(10000));
                }

                //save wechat pic to azure storage blob
                try
                {
                    var webClient = new WebClient();
                    var bytes = webClient.DownloadData(form.headimgurl);

                    var picName = Guid.NewGuid().ToString("N");

                    Blob.UploadFromBytes(CFDGlobal.USER_PIC_BLOB_CONTAINER, picName, bytes);

                    user.PicUrl = CFDGlobal.USER_PIC_BLOB_CONTAINER_URL + picName;
                }
                catch (Exception ex)
                {
                    CFDGlobal.LogError("Fail saving wechat picture to azure blob");
                    CFDGlobal.LogException(ex);
                }

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
        [BasicAuth]
        public UserDTO GetMe(LoginFormDTO form)
        {
            var user = GetUser();

            var userDto = Mapper.Map<UserDTO>(user);

            return userDto;
        }

        [HttpPost]
        //[RequireHttps]
        [ActionName("nickname")]
        [BasicAuth]
        public ResultDTO SetNickname(string nickname)
        {
            if (db.Users.Any(o => o.Id != UserId && o.Nickname == nickname))
                return new ResultDTO
                {
                    success = false,
                    message = __(TransKey.NICKNAME_EXISTS)
                };

            var user = GetUser();
            user.Nickname = nickname;
            db.SaveChanges();

            return new ResultDTO {success = true};
        }

        [HttpGet]
        [ActionName("balancecash")]
        [BasicAuth]
        public Result<String> GetBalanceCash(LoginFormDTO form)
        {
            //var user = GetUser();
            //var balanceCash = user.BalanceCash;

            var userAyondoOrders = db.UserAyondoOrders.FirstOrDefault();

            return new Result<string>() { success = true, data = userAyondoOrders.BalanceCash.ToString() };
        }
    }
}