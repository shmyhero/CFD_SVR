using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.igetui.api.openservice;
using com.igetui.api.openservice.igetui;
using com.igetui.api.openservice.igetui.template;
using com.igetui.api.openservice.payload;
using Newtonsoft.Json.Linq;

namespace CFD_COMMON.Utils
{
    public class GeTui
    {
        //全民股神
        //private const String GETUI_APPID = "tuDsXFjpPa6tJNxsWnEkUA";
        //private const String GETUI_APPKEY = "1F6q9VIJrt9n6Hm72VjVe6";
        //private const String GETUI_MASTERSECRET = "9wm17xrCRhAeuTsa8vKyz1";
        //盈交易
        //private const String GETUI_APPID = "v28qIAj6TO6ykm4QYEQFU";
        //private const String GETUI_APPKEY = "wY0MaVLXBjAneFnLwmBUDA";
        //private const String GETUI_MASTERSECRET = "ENk0Dc9Fac5AzZlPV7LSs5";
        private string[] appIdList = new string[] { "v28qIAj6TO6ykm4QYEQFU", "yug3IJK3kh8SKs7FQaSdI3" };
        private string[] appKeyList = new string[] { "wY0MaVLXBjAneFnLwmBUDA", "2ZIYLZjWQy8i4l7jcmdyB3" };
        private string[] masterSecretList = new string[] { "ENk0Dc9Fac5AzZlPV7LSs5", "H5ERSFQeO98OLMWcos6cB" };

        private const String GETUI_HOST = "http://sdk.open.api.igexin.com/apiex.htm";

        private List<IGtPush> gtPushList;

        public GeTui()
        {
            gtPushList = new List<IGtPush>();

            for(int i=0; i< appIdList.Length; i++)
            {
                gtPushList.Add(new IGtPush(GETUI_HOST, appKeyList[i], masterSecretList[i]));
            }
            
        }

        //public string Push(string token, string text)
        //{
        //    var message = CreateMessage(text);

        //    var target = CreateTarget(token);

        //    var response = gtPush.pushMessageToSingle(message, target);

        //    return response;
        //}

        /// <summary>
        /// All json text must include token "message"
        /// </summary>
        /// <param name="lstTokenAndText"></param>
        /// <returns></returns>
        public string PushBatch(IEnumerable<KeyValuePair<string, string>> lstTokenAndText)
        {
            List<string> response = new List<string>();
            for(int x=0; x<gtPushList.Count; x++)
            {
                var batch = gtPushList[x].getBatch();
                string appID = appIdList[x];
                string appKey = appKeyList[x];

                foreach (var tokenAndText in lstTokenAndText)
                {
                    var message = CreateMessage(tokenAndText.Value,appID, appKey);

                    var target = CreateTarget(tokenAndText.Key, appID);

                    batch.add(message, target);
                }

                response.Add(batch.submit());
            }


            //dynamic result = JsonConvert.DeserializeObject(response);

            return string.Join(";", response.ToArray());
        }

        private SingleMessage CreateMessage(string text, string appID, string appKey)
        {
            //推送通知
            //var template = new NotificationTemplate
            //{
            //    AppId = GETUI_APPID,
            //    AppKey = GETUI_APPKEY,
            //    Title = "盈交易",
            //    Text = text,
            //    Logo = "",
            //    LogoURL = "https://cfdstorage.blob.core.chinacloudapi.cn/pushlogo/pushlogo.png",
            //    TransmissionType = "1",
            //    TransmissionContent = "",
            //    IsRing = true,
            //    IsVibrate = true,
            //    IsClearable = true
            //};

            //透传消息
            var template = new TransmissionTemplate()
            {
                AppId = appID,
                AppKey = appKey,
                //应用启动类型，1：强制应用启动 2：等待应用启动
                TransmissionType = "2",
                TransmissionContent = text
            };

            string alertMsg = JObject.Parse(text)["message"].Value<string>();
            //IOS
            APNPayload apnpayload = new APNPayload();
            var msg = new SimpleAlertMsg(alertMsg);
            apnpayload.AlertMsg = msg;
            //for ios, used as unique symbol
            apnpayload.addCustomMsg("payload", text);

            template.setAPNInfo(apnpayload);

            var message = new SingleMessage
            {
                IsOffline = true,
                OfflineExpireTime = 1000 * 3600 * 12,
                Data = template
            };

            return message;
        }

        private Target CreateTarget(string token, string appID)
        {
            var target = new Target
            {
                appId = appID,
                clientId = token
            };

            return target;
        }
    }
}
