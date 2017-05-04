using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.IdentityVerify
{
    public class GuozhengtongVerification : IProfileVerify
    {
        public JObject Verify(OcrFaceCheckFormDTO form)
        {
            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.GetConfigurationSetting("GuoZhengTongHost") + "ocrFaceCheck");
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Proxy = null;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);

            var s = JsonConvert.SerializeObject(form); //string.Format(json, username, password);
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
            //var jObject = JsonConvert.DeserializeObject<VerifyResponse>(str);
            var jObject = JObject.Parse(str);
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("OCR FaceCheck called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            return jObject;
        }
    }
}
