//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using com.igetui.api.openservice;
//using com.igetui.api.openservice.igetui;
//using com.igetui.api.openservice.igetui.template;

//namespace CFD_COMMON.Utils
//{
//    public class GeTui
//    {
//        private const String GETUI_APPID = "tuDsXFjpPa6tJNxsWnEkUA";
//        private const String GETUI_APPKEY = "1F6q9VIJrt9n6Hm72VjVe6";
//        private const String GETUI_MASTERSECRET = "9wm17xrCRhAeuTsa8vKyz1";
//        private const String GETUI_HOST = "http://sdk.open.api.igexin.com/apiex.htm";

//        private IGtPush gtPush;

//        public GeTui()
//        {
//            gtPush = new IGtPush(GETUI_HOST, GETUI_APPKEY, GETUI_MASTERSECRET);
//        }

//        public string Push(string token, string text)
//        {
//            var message = CreateMessage(text);

//            var target = CreateTarget(token);

//            var response = gtPush.pushMessageToSingle(message, target);

//            return response;
//        }

//        public string PushBatch(IEnumerable<KeyValuePair<string, string>> lstTokenAndText)
//        {
//            var batch = gtPush.getBatch();

//            foreach (var tokenAndText in lstTokenAndText)
//            {
//                var message = CreateMessage(tokenAndText.Value);

//                var target = CreateTarget(tokenAndText.Key);

//                batch.add(message, target);
//            }

//            var response =  batch.submit();

//            //dynamic result = JsonConvert.DeserializeObject(response);

//            return response;
//        }

//        private SingleMessage CreateMessage(string text)
//        {
//            var template = new NotificationTemplate
//            {
//                AppId = GETUI_APPID,
//                AppKey = GETUI_APPKEY,
//                Title = "全民股神",
//                Text = text,
//                Logo = "",
//                LogoURL = "",
//                TransmissionType = "1",
//                TransmissionContent = "",
//                IsRing = true,
//                IsVibrate = true,
//                IsClearable = true
//            };

//            var message = new SingleMessage
//            {
//                IsOffline = true,
//                OfflineExpireTime = 1000*3600*12,
//                Data = template
//            };

//            return message;
//        }

//        private Target CreateTarget(string token)
//        {
//            var target = new Target
//            {
//                appId = GETUI_APPID,
//                clientId = token
//            };

//            return target;
//        }
//    }
//}
