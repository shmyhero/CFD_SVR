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
using System.Text;

namespace CFD_API.Controllers
{
    public class CFDController : ApiController
    {
        public CFDEntities db { get; protected set; }
        public IMapper Mapper { get; protected set; }
        public IRedisClient RedisClient { get; protected set; }

        public bool IsLiveUrl
        {
            get { return Request.RequestUri.AbsolutePath.Contains("/live/") || Request.RequestUri.AbsolutePath.EndsWith("/live"); }
        }

        //public CFDController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
        //{
        //    this.db = db;
        //    this.Mapper = mapper;
        //    this.RedisClient = redisClient;
        //}

        public CFDController(CFDEntities db, IMapper mapper)
        {
            this.db = db;
            this.Mapper = mapper;
        }

        protected CFDController(CFDEntities db)
        {
            this.db = db;
        }

        protected CFDController()
        {
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

        public void CheckAndCreateAyondoDemoAccount(User user)
        {
            if (string.IsNullOrEmpty(user.AyondoUsername))
            {
                CFDGlobal.LogWarning("No Ayondo Demo Account. Try registing... userId: " + user.Id);

                CreateAyondoDemoAccount(user);

                if (string.IsNullOrEmpty(user.AyondoUsername))
                {
                    //CFDGlobal.LogWarning("Still No Ayondo Account. userId: " + user.Id);

                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        __(TransKey.NO_AYONDO_ACCOUNT)));
                }
            }
        }

        private static ConcurrentDictionary<int, DateTime> _ayondoRegisteringUsers = new ConcurrentDictionary<int, DateTime>();

        public void CreateAyondoDemoAccount(User user)
        {
            if (_ayondoRegisteringUsers.ContainsKey(user.Id))
            {
                var time = _ayondoRegisteringUsers[user.Id];
                var ts = DateTime.UtcNow - time;
                if (ts < TimeSpan.FromSeconds(8))//last request sent in less than ...
                {
                    CFDGlobal.LogInformation("Ayondo Demo Registration Skipped: userId: " + user.Id + ". only " + ts.TotalSeconds +
                                             "s from last one");
                    return;
                }
            }

            CFDGlobal.LogInformation("Ayondo Demo Registration Start: userId: " + user.Id);
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

                if (jObject["error"] != null)
                {
                    CFDGlobal.LogInformation("AMS check-username error: " + jObject["Error"].Value<string>());
                }
                else
                {
                    isAvailable = jObject["data"]["isAvailable"].Value<bool>();
                    bool isValid = jObject["data"]["isValid"].Value<bool>();

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
                var httpWebRequest =
                    WebRequest.CreateHttp(CFDGlobal.AMS_PROXY_HOST + "DemoAccount" + "?username=" + username + "&password=" + password);
                //httpWebRequest.Headers["Authorization"] = CFDGlobal.AMS_HEADER_AUTH;
                httpWebRequest.Method = "POST";
                //httpWebRequest.ContentType = "application/json; charset=UTF-8";
                httpWebRequest.Proxy = null;
//                var requestStream = httpWebRequest.GetRequestStream();
//                var sw = new StreamWriter(requestStream);

//                //Escape the "{", "}" (by duplicating them) in the format string:
//                var json =
//                    @"{{
//'AddressCity': 'TestCity',
//'AddressCountry': 'CN',
//'AddressLine1': 'Teststr. 123',
//'AddressLine2': null,
//'AddressZip': '12345',
//'ClientIP': '127.0.0.1',
//'Currency': 'USD',
//'FirstName': 'User',
//'Gender': 'Male',
//'IsTestRecord': true,
//'Language': 'EN',
//'LastName': 'THCN',
//'Password': '{1}',
//'PhonePrimary': '0044 123445',
//'SalesRepGuid':null,
//'UserName': '{0}',
//'ProductType': 'CFD'
//}}";

//                var s = string.Format(json, username, password);
//                sw.Write(s);
//                sw.Flush();
//                sw.Close();
                httpWebRequest.ContentLength = 0;

                var dtBegin = DateTime.UtcNow;

                var webResponse = httpWebRequest.GetResponse();
                var responseStream = webResponse.GetResponseStream();
                var sr = new StreamReader(responseStream);

                var str = sr.ReadToEnd();
                var ts = DateTime.UtcNow - dtBegin;
                CFDGlobal.LogInformation("AMS demo proxy called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                         httpWebRequest.RequestUri + " Response: " + str //+ "Request:" + s
                                         );

                var jObject = JObject.Parse(str);

                if (jObject["error"] != null)
                {
                    CFDGlobal.LogWarning("AMS create account error: " + jObject["Error"].Value<string>() + " userId:" + user.Id + " ayondoUsername:" + username);
                }
                else
                {
                    var guid = jObject["data"]["accountGuid"].Value<string>();
                    CFDGlobal.LogInformation("Ayondo user created: userId:" + user.Id + " username:" + username + " password:" + password + " guid:" + guid);

                    user.AyondoUsername = username;
                    user.AyondoPassword = password;
                    db.SaveChanges();
                }
            }
        }

        protected static JToken AMSCheckUsername(string username, bool isLive = false)
        {
            var accountType = isLive ? "Live" : "Demo";

            var httpWebRequest =
                WebRequest.CreateHttp(CFDGlobal.AMS_PROXY_HOST + "check-username?AccountType=" + accountType + "&UserName=" + username);
            //httpWebRequest.Headers["Authorization"] = CFDGlobal.AMS_HEADER_AUTH;
            httpWebRequest.Proxy = null;

            var dtBegin = DateTime.UtcNow;

            var webResponse = httpWebRequest.GetResponse();
            var responseStream = webResponse.GetResponseStream();
            var sr = new StreamReader(responseStream);

            var str = sr.ReadToEnd();
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("AMS check-username proxy called. Time: " + ts.TotalMilliseconds + "ms Url: " + httpWebRequest.RequestUri + " Response: " + str);

            var result = JToken.Parse(str);
            return result;
        }

        protected static JToken AMSLiveAccountComplete(string accountGuid, string mifidGuid, LiveSignupFormDTO form, User user, UserInfo userInfo)
        {
            ////Create Application
            //var initResult = AMSLiveAccountInitiate();
            //var accountGuid = initResult["data"]["accountGuid"].Value<string>();

            ////Mifid Test
            //var mifidResult = DoMifidTest(accountGuid, form);
            //var mifidGuid = mifidResult["data"]["mifidGuid"].Value<string>();
            //var rulesetId = mifidResult["data"]["rulesetId"].Value<string>();
            //var appropriatenessScore = mifidResult["data"]["appropriatenessScore"].Value<decimal>();
            //var appropriatenessResolution = mifidResult["data"]["appropriatenessResolution"].Value<string>();

            //CFDGlobal.LogInformation("MiFID result: account " + accountGuid + " mifid " + mifidGuid + " ruleset " +
            //                         rulesetId + " score " + appropriatenessScore + " resolution " +
            //                         appropriatenessResolution);

            //Complete Application
            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.AMS_HOST + "live-account/" + accountGuid + "/complete");
            httpWebRequest.Headers["Authorization"] = CFDGlobal.AMS_HEADER_AUTH;
            httpWebRequest.Method = "PUT";
            httpWebRequest.ContentType = "application/json; charset=UTF-8";
            httpWebRequest.Proxy = null;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);

            var amsForm = new AMSLiveUserCreateFormDTO();
            //amsForm.AddressCity = "TestCity";
            //amsForm.AddressCountry = "CN";
            //amsForm.AddressLine1 = form.addr;
            //amsForm.AddressLine2 = null;
            //amsForm.AddressZip = "12345";
            //amsForm.ClientIP = "127.0.0.1";
            //amsForm.Currency = "USD";
            //amsForm.FirstName = userInfo.FirstName;// form.firstName;
            //amsForm.Gender = form.gender ? "Male" : "Female";
            //amsForm.IsTestRecord = false;
            //amsForm.Language = "CN";
            //amsForm.LastName = userInfo.LastName;// form.lastName;
            //amsForm.Password = form.password;
            //amsForm.PhonePrimary = user.Phone;
            //amsForm.SalesRepGuid = null;
            //amsForm.UserName = form.username;
            //amsForm.AnnualIncome = form.annualIncome;
            //amsForm.DateOfBirth = form.birthday.Replace('.', '-');
            //amsForm.Email = form.email;
            //amsForm.EmploymentStatus = form.empStatus;
            //amsForm.HasAttendedTraining = false;
            //amsForm.HasOtherQualification = form.hasOtherQualif;
            //amsForm.HasProfessionalExperience = false;// form.hasProExp;
            //amsForm.InvestmentPortfolio = form.investPct;
            //amsForm.IsIDVerified = true;
            //amsForm.JobTitle = "JobTitle";

            //string strProducts = string.Empty;
            //if (form.expDeriv) strProducts += "Exchange Traded Derivatives,";
            //if (form.expOTCDeriv) strProducts += "OTC Derivatives,";
            //if (form.expShareBond) strProducts += "Shares and Bonds,";
            //if (strProducts.Length > 0) strProducts.Substring(0, strProducts.Length - 1);
            //amsForm.LeveragedProducts = strProducts;

            //amsForm.Nationality = "CN";
            //amsForm.NetWorth = form.netWorth > 100 ? 100 : form.netWorth;//form.netWorth;
            //amsForm.Nickname = user.Nickname;
            //amsForm.NumberOfMarginTrades = form.investFrq;
            //amsForm.PhonePrimaryCountryCode = "CN";
            //amsForm.SubscribeTradeNotifications = false;

            amsForm.addressCountry = "CN";
            amsForm.addressLine1 = form.addr;
            amsForm.currency = "USD";
            amsForm.dateOfBirth = form.birthday.Replace('.', '-');
            amsForm.email = form.email;
            amsForm.employmentStatus = form.empStatus;
            amsForm.employerName = form.employerName;
            amsForm.employerSector = form.employerSector;
            amsForm.jobTitle = form.empPosition;
            amsForm.firstname = userInfo.FirstName;
            amsForm.gender = form.gender.Value ? "Male" : "Female";
            amsForm.isIdVerified = true;
            amsForm.isTestRecord = false;
            amsForm.language = "CN";
            amsForm.lastname = userInfo.LastName;
            amsForm.nationality = "CN";
            amsForm.nickname = user.Nickname;
            amsForm.password = form.password;
            amsForm.phonePrimary = user.Phone;
            amsForm.phonePrimaryIso2 = "CN";
            amsForm.sourceOfFunds = form.sourceOfFunds;
            amsForm.subscribeOffers = false;
            amsForm.subscribeTradeNotifications = false;
            amsForm.username = form.username;

            amsForm.mifidGuid = mifidGuid;

            amsForm.origin = CFDGlobal.AMS_ORIGIN;

            amsForm.confirmMifidOverride = form.confirmMifidOverride;

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
            CFDGlobal.LogInformation("AMS live live-account/complete called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            var result = JToken.Parse(str);

            return result;
        }

        protected static JToken AMSLiveAccountInitiate()
        {
            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.AMS_HOST + "live-account");
            httpWebRequest.Headers["Authorization"] = CFDGlobal.AMS_HEADER_AUTH;
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json; charset=UTF-8";
            httpWebRequest.Proxy = null;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);

            var amsForm = new AMSLiveUserCreateFormDTO();

            var s = JsonConvert.SerializeObject(amsForm, new JsonSerializerSettings() {NullValueHandling = NullValueHandling.Ignore}); //string.Format(json, username, password);
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
            CFDGlobal.LogInformation("AMS live live-account called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            var result = JToken.Parse(str);

            return result;
        }

        protected static JToken DoMifidTest(string accountGuid, LiveSignupFormDTO form)
        {
            var mifidInfo = GetMifidInfo();

            var rulesetId = mifidInfo["recommendedRulesetId"].Value<string>();

            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.AMS_HOST + "mifid-test/" + accountGuid + "/" + rulesetId);
            httpWebRequest.Headers["Authorization"] = CFDGlobal.AMS_HEADER_AUTH;
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json; charset=UTF-8";
            httpWebRequest.Proxy = null;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);

            var mifidForm = new AMSLiveUserMifidFormDTO();
            mifidForm.hasAttendedTraining = form.hasTraining;
            mifidForm.hasDemoAccount = form.hasDemoAcc;
            mifidForm.hasOtherQualification = form.hasOtherQualif;
            mifidForm.hasProfessionalExperience = form.hasProExp;

            mifidForm.hasTradedHighLev = form.hasTradedHighLev;
            mifidForm.hasTradedMidLev = form.hasTradedMidLev;
            mifidForm.hasTradedNoLev = form.hasTradedNoLev;
            mifidForm.highLevBalance = form.highLevBalance;
            mifidForm.highLevFrq = form.highLevFrq;
            mifidForm.highLevRisk = form.highLevRisk;
            mifidForm.midLevBalance = form.midLevBalance;
            mifidForm.midLevFrq = form.midLevFrq;
            mifidForm.midLevRisk = form.midLevRisk;
            mifidForm.noLevBalance = form.noLevBalance;
            mifidForm.noLevFrq = form.noLevFrq;
            mifidForm.noLevRisk = form.noLevRisk;

            mifidForm.investments = form.investments;
            mifidForm.monthlyNetIncome = form.monthlyIncome;
            mifidForm.otherQualification = string.IsNullOrEmpty(form.otherQualif) ? null : form.otherQualif.Split(',');

            var s = JsonConvert.SerializeObject(mifidForm, new JsonSerializerSettings() {NullValueHandling = NullValueHandling.Ignore}); //string.Format(json, username, password);
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
            CFDGlobal.LogInformation("AMS live mifid-test called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            var result = JToken.Parse(str);

            return result;
        }

        private static JToken GetMifidInfo()
        {
            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.AMS_HOST + "mifid-info");
            httpWebRequest.Headers["Authorization"] = CFDGlobal.AMS_HEADER_AUTH;
            httpWebRequest.Method = "GET";
            httpWebRequest.ContentType = "application/json; charset=UTF-8";
            httpWebRequest.Proxy = null;

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
            CFDGlobal.LogInformation("AMS live mifid-info called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str );

            var result = JToken.Parse(str);

            return result;
        }

        protected static JToken AMSBindBankCard(LiveUserBankCardFormDTO form, string method = "POST")
        {
            byte[] binaryData = Encoding.UTF8.GetBytes(JObject.FromObject(form).ToString());
            var httpWebRequest =
                WebRequest.CreateHttp(CFDGlobal.AMS_PROXY_HOST + "refaccount");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = method;
            httpWebRequest.Timeout = int.MaxValue;
            httpWebRequest.Proxy = null;

            httpWebRequest.ContentLength = binaryData.Length;
            Stream reqstream = httpWebRequest.GetRequestStream();
            reqstream.Write(binaryData, 0, binaryData.Length);

            var dtBegin = DateTime.UtcNow;

            var webResponse = httpWebRequest.GetResponse();
            var responseStream = webResponse.GetResponseStream();
            var sr = new StreamReader(responseStream);

            var str = sr.ReadToEnd();
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("AMS reference account proxy called. Time: " + ts.TotalMilliseconds + "ms Url: " + httpWebRequest.RequestUri + " Response: " + str);

            var jObject = JToken.Parse(str);
            return jObject;
        }
    }
}