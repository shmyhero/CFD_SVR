using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.Web;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServiceStack.Redis;

namespace CFD_API.Controllers
{
    public class CFDController : ApiController
    {
        public CFDEntities db { get; protected set; }
        public IMapper Mapper { get; protected set; }
        public IRedisClient RedisClient { get; protected set; }

        public CFDController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
        {
            this.db = db;
            this.Mapper = mapper;
            this.RedisClient = redisClient;
        }

        public CFDController(CFDEntities db, IMapper mapper)
        {
            this.db = db;
            this.Mapper = mapper;
        }

        protected CFDController(CFDEntities db)
        {
            this.db = db;
        }

        public int UserId
        {
            get
            {
                return Convert.ToInt32(HttpContext.Current.User.Identity.Name);
            }

            ////for unit testing
            //get; set;
        }

        public DateTime RequestStartAt { get; set; }

        /// <summary>
        /// localization
        /// </summary>
        /// <param name="transKey"></param>
        /// <returns></returns>
        public string __(TransKey transKey)
        {
            return Translator.Translate(transKey);
        }

        public User GetUser()
        {
            return db.Users.FirstOrDefault(o => o.Id == UserId);
        }

        public void CheckAndCreateAyondoAccount(User user)
        {
            if (string.IsNullOrEmpty(user.AyondoUsername))
            {
                CFDGlobal.LogWarning("No Ayondo Account. Try registing... userId: " + user.Id);

                CreateAyondoAccount(user);

                if (string.IsNullOrEmpty(user.AyondoUsername))
                {
                    CFDGlobal.LogWarning("Still No Ayondo Account. userId: " + user.Id);

                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        __(TransKey.NO_AYONDO_ACCOUNT)));
                }
            }
        }

        private const string AMS_HEADER_AUTH = "Bearer RDFFMzY2NDktMDlDRC00OTg4LUEwNjAtRUM0NDIxMTNDMDBCMDQ5QUU3NjgtRTUyMy00RkE0LTk5MTQtNTMwQUM1RjY5MDY5";
        private const string AMS_HOST = "https://www.ayondo-ams.com/tradeherocn/";

        private static ConcurrentDictionary<int, DateTime> _ayondoRegisteringUsers = new ConcurrentDictionary<int, DateTime>();

        public void CreateAyondoAccount(User user)
        {
            if (_ayondoRegisteringUsers.ContainsKey(user.Id))
            {
                var time = _ayondoRegisteringUsers[user.Id];
                var ts = DateTime.UtcNow - time;
                if (ts < TimeSpan.FromSeconds(8))//last request sent in less than ...
                {
                    CFDGlobal.LogInformation("Ayondo Registration Skipped: userId: " + user.Id + ". only " + ts.TotalSeconds +
                                             "s from last one");
                    return;
                }
            }

            CFDGlobal.LogInformation("Ayondo Registration Start: userId: " + user.Id);
            _ayondoRegisteringUsers.AddOrUpdate(user.Id, DateTime.UtcNow, (key, value) => DateTime.UtcNow);

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
                var jObject = AMSCheckUsername(username);

                tryCount++;

                if (jObject["Error"] != null)
                {
                    CFDGlobal.LogInformation("AMS check-username error: " + jObject["Error"].Value<string>());
                }
                else
                {
                    isAvailable = jObject["IsAvailable"].Value<bool>();
                    bool isValid = jObject["IsValid"].Value<bool>();

                    if (!isAvailable || !isValid)
                    {
                        CFDGlobal.LogInformation("Ayondo check-user: " + username + " isAvailable:" + isAvailable + " isValid:" + isValid);

                        //generate new username for next attempt
                        username = username_base + Randoms.GetRandomAlphabeticString(4);
                    }
                }
            } while (!isAvailable && tryCount < 3); // retry if unavailable and tryCount < 3

            if (isAvailable)
            {
                var httpWebRequest = WebRequest.CreateHttp(AMS_HOST + "DemoAccount");
                httpWebRequest.Headers["Authorization"] = AMS_HEADER_AUTH;
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentType = "application/json; charset=UTF-8";
                httpWebRequest.Proxy = null;
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
'FirstName': 'User',
'Gender': 'Male',
'IsTestRecord': true,
'Language': 'EN',
'LastName': 'THCN',
'Password': '{1}',
'PhonePrimary': '0044 123445',
'SalesRepGuid':null,
'UserName': '{0}',
'ProductType': 'CFD'
}}";

                var s = string.Format(json, username, password);
                sw.Write(s);
                sw.Flush();
                sw.Close();

                var dtBegin = DateTime.UtcNow;

                var webResponse = httpWebRequest.GetResponse();
                var responseStream = webResponse.GetResponseStream();
                var sr = new StreamReader(responseStream);

                var str = sr.ReadToEnd();
                var ts = DateTime.UtcNow - dtBegin;
                CFDGlobal.LogInformation("AMS called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                         httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

                var jObject = JObject.Parse(str);

                if (jObject["Error"] != null)
                {
                    CFDGlobal.LogWarning("AMS create account error: " + jObject["Error"].Value<string>() + " userId:" + user.Id + " ayondoUsername:" + username);
                }
                else
                {
                    var guid = jObject["Guid"].Value<string>();
                    CFDGlobal.LogInformation("Ayondo user created: userId:" + user.Id + " username:" + username + " password:" + password + " guid:" + guid);

                    user.AyondoUsername = username;
                    user.AyondoPassword = password;
                    db.SaveChanges();
                }
            }
        }

        protected static JObject AMSCheckUsername(string username, bool isLive = false)
        {
            var accountType = isLive ? "Live" : "Demo";

            var httpWebRequest = WebRequest.CreateHttp(AMS_HOST + "check-username?AccountType="+ accountType+"&UserName=" + username);
            httpWebRequest.Headers["Authorization"] = AMS_HEADER_AUTH;
            httpWebRequest.Proxy = null;

            var dtBegin = DateTime.UtcNow;

            var webResponse = httpWebRequest.GetResponse();
            var responseStream = webResponse.GetResponseStream();
            var sr = new StreamReader(responseStream);

            var str = sr.ReadToEnd();
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("AMS called. Time: " + ts.TotalMilliseconds + "ms Url: " + httpWebRequest.RequestUri + " Response: " + str);

            var jObject = JObject.Parse(str);
            return jObject;
        }

        protected static JObject AMSLiveAccount(LiveSignupFormDTO form, User user)
        {
            var httpWebRequest = WebRequest.CreateHttp(AMS_HOST + "live-account");
            httpWebRequest.Headers["Authorization"] = AMS_HEADER_AUTH;
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json; charset=UTF-8";
            httpWebRequest.Proxy = null;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);

            var amsForm = new AMSLiveUserCreateFormDTO();
            amsForm.AddressCity = "TestCity";
            amsForm.AddressCountry = "CN";
            amsForm.AddressLine1 = form.addr;
            amsForm.AddressLine2 = null;
            amsForm.AddressZip = "12345";
            amsForm.ClientIP = "127.0.0.1";
            amsForm.Currency = "USD";
            amsForm.FirstName = form.firstName;
            amsForm.Gender = form.gender ? "Male" : "Female";
            amsForm.IsTestRecord = false;
            amsForm.Language = "ZH";
            amsForm.LastName = form.lastName;
            amsForm.Password = form.password;
            amsForm.PhonePrimary = user.Phone;
            amsForm.SalesRepGuid = null;
            amsForm.UserName = form.username;
            amsForm.AnnualIncome = form.annualIncome;
            amsForm.DateOfBirth = form.birthday.Replace('.', '-');
            amsForm.Email = form.email;
            amsForm.EmploymentStatus = form.empStatus;
            amsForm.HasAttendedTraining = false;
            amsForm.HasOtherQualification = form.hasOtherQualif;
            amsForm.HasProfessionalExperience = form.hasProExp;
            amsForm.InvestmentPortfolio = form.investPct;
            amsForm.IsIDVerified = true;
            amsForm.JobTitle = "JobTitle";

            string strProducts = string.Empty;
            if (form.expDeriv) strProducts += "Exchange Traded Derivatives,";
            if (form.expOTCDeriv) strProducts += "OTC Derivatives,";
            if (form.expShareBond) strProducts += "Shares and Bonds,";
            if (strProducts.Length > 0) strProducts.Substring(0, strProducts.Length - 1);
            amsForm.LeveragedProducts = strProducts;

            amsForm.Nationality = "CN";
            amsForm.NetWorth = form.netWorth;
            amsForm.Nickname = user.Nickname;
            amsForm.NumberOfMarginTrades = form.investFrq;
            amsForm.PhonePrimaryCountryCode = "CN";
            amsForm.SubscribeTradeNotifications = false;

            var s = JsonConvert.SerializeObject(amsForm); //string.Format(json, username, password);
            sw.Write(s);
            sw.Flush();
            sw.Close();

            var dtBegin = DateTime.UtcNow;

            WebResponse webResponse;
            try
            {
                webResponse = httpWebRequest.GetResponse();
            }
            catch (WebException e)
            {
                webResponse = e.Response;
            }

            var responseStream = webResponse.GetResponseStream();
            var sr = new StreamReader(responseStream);

            var str = sr.ReadToEnd();
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("AMS called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            var jObject = JObject.Parse(str);

            return jObject;
        }

        protected static JObject AMSBindBankCard(LiveUserBankCardFormDTO form)
        {
            var httpWebRequest = WebRequest.CreateHttp(AMS_HOST + "reference-account");
            httpWebRequest.Headers["Authorization"] = AMS_HEADER_AUTH;
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json; charset=UTF-8";
            httpWebRequest.Proxy = null;
            httpWebRequest.Timeout = int.MaxValue;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);
            var s = JsonConvert.SerializeObject(form);
            sw.Write(s);
            sw.Flush();
            sw.Close();

            var dtBegin = DateTime.UtcNow;

            WebResponse webResponse;
            try
            {
                webResponse = httpWebRequest.GetResponse();
            }
            catch (WebException e)
            {
                webResponse = e.Response;
            }

            var responseStream = webResponse.GetResponseStream();
            var sr = new StreamReader(responseStream);

            var str = sr.ReadToEnd();
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("AMS called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            var jObject = JObject.Parse(str);

            return jObject;
        }
    }
}