using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using CFD_COMMON;
using Newtonsoft.Json.Linq;
using CFD_API.DTO;
using CFD_COMMON.Utils;
using Newtonsoft.Json;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/proxy")]
    public class ProxyController : CFDController
    {
        [HttpGet]
        [Route("check-username")]
        public JObject CheckUsername(string AccountType, string UserName)
        {
            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.AMS_HOST + "check-username?AccountType=" + AccountType + "&UserName=" + UserName);
            httpWebRequest.Headers["Authorization"] = CFDGlobal.AMS_HEADER_AUTH;
            httpWebRequest.Proxy = null;

            var dtBegin = DateTime.UtcNow;

            var webResponse = httpWebRequest.GetResponse();
            var responseStream = webResponse.GetResponseStream();
            var sr = new StreamReader(responseStream);

            var str = sr.ReadToEnd();
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("AMS check-username called. Time: " + ts.TotalMilliseconds + "ms Url: " + httpWebRequest.RequestUri + " Response: " + str);

            var jObject = JObject.Parse(str);
            return jObject;
        }

        [HttpPost]
        [Route("DemoAccount")]
        public JToken DemoAccount(string username, string password)
        {
            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.AMS_HOST + "demo-account");
            httpWebRequest.Headers["Authorization"] = CFDGlobal.AMS_HEADER_AUTH;
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json; charset=UTF-8";
            httpWebRequest.Proxy = null;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);

            //Escape the "{", "}" (by duplicating them) in the format string:
            var json =
                @"{{
'addressCity': 'TestCity',
'addressCountry': 'CN',
'addressLine1': 'Teststr. 123',
'addressLine2': '',
'addressZip': '12345',
'clientIP': '127.0.0.1',
'currency': 'USD',
'email': '{2}@tradehero.mobi',
'firstname': 'User',
'gender': 'Male',
'isTestRecord': true,
'language': 'EN',
'lastname': 'THCN',
'password': '{1}',
'phonePrimary': '0044 123445',
'phonePrimaryIso2': 'CN',
'username': '{0}',
'productType': 'CFD'
}}";

            var s = string.Format(json, username,
                Encryption.GetCypherText_3DES_CBC_MD5ofPW_IVPrefixed(password, Encryption.SHARED_SECRET_Ayondo),
                username);
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
            CFDGlobal.LogInformation("AMS demo called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            var jToken = JToken.Parse(str);
            return jToken;
        }

        [HttpPost]
        [Route("refaccount")]
        public JToken ReferenctAccount(LiveUserBankCardFormDTO form)
        {
            if(!Request.Headers.Contains("accountGuid"))
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "missing account guid.")) ;
            }
            string accountGuid = Request.Headers.GetValues("accountGuid").FirstOrDefault();
            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.AMS_HOST+"live-account/"+ accountGuid + "/reference-account");
            httpWebRequest.Headers["Authorization"] = CFDGlobal.AMS_HEADER_AUTH;
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
            CFDGlobal.LogInformation("AMS reference-account called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            var json = JToken.Parse(str);

            return json;
        }

        [HttpPost]
        [Route("document/{accountGuid}")]
        public JToken Document(string accountGuid, AMSLiveUserDocumentFormDTO form)
        {
            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.AMS_HOST + "live-account/" + accountGuid + "/document");
            httpWebRequest.Headers["Authorization"] = CFDGlobal.AMS_HEADER_AUTH;
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json; charset=UTF-8";
            httpWebRequest.Proxy = null;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);

            var s = JsonConvert.SerializeObject(form, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
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
            CFDGlobal.LogInformation("AMS live live-account/document called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            var result = JToken.Parse(str);

            return result;
        }
    }
}
