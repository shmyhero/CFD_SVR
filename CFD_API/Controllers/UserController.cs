using System;
using System.IO;
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
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using Newtonsoft.Json.Linq;

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

                    var nickname = "u" + user.Id.ToString("00000000");
                    user.Nickname = nickname;

                    //check duplicate nickname and generate random suffix
                    while (db.Users.Any(o => o.Id != user.Id && o.Nickname == user.Nickname))
                    {
                        user.Nickname = nickname + (new Random().Next(10000));

                        //todo: prevent dead loop
                    }

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

                if (user.AyondoUsername == null)
                    CreateAyondoAccount(user);
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

                    //todo: prevent dead loop
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

                //todo:if user is from wechat but user.picurl is null, reload img?
            }

            if (user.AyondoUsername == null)
                CreateAyondoAccount(user);

            return result;
        }

        private void CreateAyondoAccount(User user)
        {
            //Must be 5-20 alphanumeric characters (letter and numerals only).
            //Usernames cannot be purely numeric.
            var username_base = "thcn" + user.Id;

            //At least 4 chars. Allowed chars: [0-9a-zA-Z\!\#\*\$\-\/\=\?\@\.\,\:\;]
            var password = Randoms.GetRandomAlphanumericString(6);

            bool isAvailable = false;
            int tryCount = 0;
            string username = username_base;
            do
            {
                var httpWebRequest = HttpWebRequest.CreateHttp("https://lab1-www.ayondo-ams.com/tradeherocn/check-username?AccountType=Demo&UserName=" + username);
                httpWebRequest.Headers["Authorization"] = "Bearer NkJDMUQzNkQtMzg2OS00NEZELUIzOUMtODQ4MkUzMTAyMTk0MzRBNDYyMkQtODQ1MC00MDA4LTlFRUUtMEIwRkFENzQ3QUY4";
                var webResponse = httpWebRequest.GetResponse();
                var responseStream = webResponse.GetResponseStream();
                var sr = new StreamReader(responseStream);

                var jObject = JObject.Parse(sr.ReadToEnd());

                tryCount++;

                if (jObject["Error"] != null)
                {
                    CFDGlobal.LogInformation("AMS check-username error: " + jObject["Error"].Value<string>());

                    isAvailable = false;

                    //generate new username for next attempt
                    username = username_base + Randoms.GetRandomAlphabeticString(4);
                }
                else
                {
                    isAvailable = jObject["IsAvailable"].Value<bool>();
                    bool isValid = jObject["IsValid"].Value<bool>();

                    if (!isAvailable)
                    {
                        CFDGlobal.LogInformation("Ayondo check-user: unavailable: "+username);
                    }
                }
            } while (!isAvailable && tryCount < 3); // retry if unavailable and tryCount < 3

            if (isAvailable)
            {
                var httpWebRequest = HttpWebRequest.CreateHttp("https://lab1-www.ayondo-ams.com/tradeherocn/DemoAccount");
                httpWebRequest.Headers["Authorization"] = "Bearer NkJDMUQzNkQtMzg2OS00NEZELUIzOUMtODQ4MkUzMTAyMTk0MzRBNDYyMkQtODQ1MC00MDA4LTlFRUUtMEIwRkFENzQ3QUY4";
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentType = "application/json; charset=UTF-8";
                var requestStream = httpWebRequest.GetRequestStream();
                var sw = new StreamWriter(requestStream);

                //Escape the "{", "}" (by duplicating them) in the format string:
                var json =
                    @"{{
'AddressCity': 'TestCity',
'AddressCountry': 'CN',
'AddressLine1': 'Teststr. 123',
'AddressLine2': null,
'AddressZip': '12345',
'ClientIP': '127.0.0.1',
'Currency': 'USD',
'Email': 'email@gmail.com',
'FirstName': 'User',
'Gender': 'Male',
'IsTestRecord': true,
'Language': 'EN',
'LastName': 'THCN',
'Password': '{1}',
'PhonePrimary': '0044 123445',
'SalesRepGuid':null,
'UserName': '{0}'
}}";

                var s = string.Format(json, username, password);
                sw.Write(s);
                sw.Flush();
                sw.Close();

                var webResponse = httpWebRequest.GetResponse();
                var responseStream = webResponse.GetResponseStream();
                var sr = new StreamReader(responseStream);

                var jObject = JObject.Parse(sr.ReadToEnd());

                if (jObject["Error"] != null)
                {
                    CFDGlobal.LogWarning("AMS create account error: " + jObject["Error"].Value<string>()+" userId:"+user.Id+" ayondoUsername:"+username);
                }
                else
                {
                    var guid = jObject["Guid"].Value<string>();

                    user.AyondoUsername = username;
                    user.AyondoPassword = password;
                    db.SaveChanges();
                }
            }
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

        //[HttpGet]
        //[ActionName("balancecash")]
        //[BasicAuth]
        //public UserAyondoDTO GetBalanceCash(LoginFormDTO form)
        //{
        //    //var user = GetUser();
        //    //var balanceCash = user.BalanceCash;

        //    var userAyondos = db.UserAyondos.FirstOrDefault();

        //    return new UserAyondoDTO();// { balanceCash = userAyondos.BalanceCash };
        //}
    }
}