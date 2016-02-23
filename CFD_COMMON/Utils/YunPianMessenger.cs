using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils
{
    public class YunPianMessenger
    {
        private const string BASE_URI = "http://yunpian.com";
        private const string VERSION = "v1";
        private const string URI_GET_USER_INFO = BASE_URI + "/" + VERSION + "/user/get.json";
        private const string URI_SEND_SMS = BASE_URI + "/" + VERSION + "/sms/send.json";
        private const string URI_TPL_SEND_SMS = BASE_URI + "/" + VERSION + "/sms/tpl_send.json";

        private const string API_KEY = "YunPianApiKey";
        private const string TEMPLATE_ID = "YunPianTemplateId";

        public static string ApiKey
        {
            get
            {
                //if (RoleEnvironment.IsAvailable)
                //{
                //    return RoleEnvironment.GetConfigurationSettingValue(API_KEY);
                //}

                var appsettings = ConfigurationManager.AppSettings;
                if (appsettings[API_KEY] == null)
                {
                    throw new ApplicationException("Could not obtain AppSettings: " + API_KEY);
                }

                return appsettings[API_KEY];
            }
        }

        public static string TemplateId
        {
            get
            {
                //if (RoleEnvironment.IsAvailable)
                //{
                //    return RoleEnvironment.GetConfigurationSettingValue(TEMPLATE_ID);
                //}

                var appsettings = ConfigurationManager.AppSettings;
                if (appsettings[TEMPLATE_ID] == null)
                {
                    throw new ApplicationException("Could not obtain AppSettings: " + TEMPLATE_ID);
                }

                return appsettings[TEMPLATE_ID];
            }
        }

        //public static string GetUserInfo()
        //{
        //    var req = WebRequest.Create(URI_GET_USER_INFO + "?apikey=" + ApiKey);
        //    var resp = req.GetResponse();
        //    var sr = new StreamReader(resp.GetResponseStream());
        //    return sr.ReadToEnd().Trim();
        //}

        //public static string SendSms(string text, string mobile)
        //{
        //    var parameter = "apikey=" + ApiKey + "&text=" + text + "&mobile=" + mobile;
        //    var req = WebRequest.Create(URI_SEND_SMS);
        //    req.ContentType = "application/x-www-form-urlencoded";
        //    req.Method = "POST";
        //    var bytes = Encoding.UTF8.GetBytes(parameter);
        //    req.ContentLength = bytes.Length;
        //    var os = req.GetRequestStream();
        //    os.Write(bytes, 0, bytes.Length);
        //    os.Close();
        //    var resp = req.GetResponse();
        //    var sr = new StreamReader(resp.GetResponseStream());
        //    return sr.ReadToEnd().Trim();
        //}

        public static string TplSendCodeSms(string tplValue, string mobile)
        {
            var encodedTplValue = Uri.EscapeDataString(tplValue);
            var parameter = "apikey=" + ApiKey + "&tpl_id=" + TemplateId + "&tpl_value=" + encodedTplValue + "&mobile=" +
                            mobile;
            var req = WebRequest.Create(URI_TPL_SEND_SMS);
            req.ContentType = "application/x-www-form-urlencoded";
            req.Method = "POST";
            var bytes = Encoding.UTF8.GetBytes(parameter);
            req.ContentLength = bytes.Length;
            var os = req.GetRequestStream();
            os.Write(bytes, 0, bytes.Length);
            os.Close();
            var resp = req.GetResponse();
            var sr = new StreamReader(resp.GetResponseStream());
            return sr.ReadToEnd().Trim();
        }

        //public static string TplSendPwdSms(string tplValue, string mobile)
        //{
        //    var encodedTplValue = Uri.EscapeDataString(tplValue);
        //    var parameter = "apikey=" + ApiKey + "&tpl_id=495229&tpl_value=" + encodedTplValue + "&mobile=" + mobile;
        //    var req = WebRequest.Create(URI_TPL_SEND_SMS);
        //    req.ContentType = "application/x-www-form-urlencoded";
        //    req.Method = "POST";
        //    var bytes = Encoding.UTF8.GetBytes(parameter);
        //    req.ContentLength = bytes.Length;
        //    var os = req.GetRequestStream();
        //    os.Write(bytes, 0, bytes.Length);
        //    os.Close();
        //    var resp = req.GetResponse();
        //    var sr = new StreamReader(resp.GetResponseStream());
        //    return sr.ReadToEnd().Trim();
        //}
    }
}
