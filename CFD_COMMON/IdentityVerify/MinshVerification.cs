using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using CFD_COMMON.Utils;
using System.Net;
using System.IO;
using CFD_COMMON.Utils.Extensions;

namespace CFD_COMMON.IdentityVerify
{
    public class MinshVerification : IProfileVerify
    {
        private const string key = "217af15e";
        private const string mID = "10076";
        private const string account = "maihe";
        public JObject Verify(OcrFaceCheckFormDTO form)
        {
            string template = "{{'result':'{0}','message':'{1}','transaction_id':'XXX','user_check_result':'{2}','verify_result':'0','verify_similarity':'0'}}";
            form.userName = form.lastName + form.firstName;
            DESUtil des = new DESUtil();
            byte[] binaryData = Encoding.UTF8.GetBytes(des.Encrypt(string.Format("{{ 'RealName': '{0}', 'IdentityID':'{1}'}}", form.userName, form.userId), key));
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(CFDGlobal.GetConfigurationSetting("MinshHost"));
            try
            {
                request.ContentType = "application/json";
                request.Method = "POST";
                request.Headers.Add("merchantID", mID);
                request.Headers.Add("account", des.Encrypt(account, key));
                request.Headers.Add("timeStamp", des.Encrypt(DateTime.Now.ToTimeStamp().ToString(), key));
                request.ContentLength = binaryData.Length;
                request.Timeout = int.MaxValue;
                Stream reqstream = request.GetRequestStream();
                reqstream.Write(binaryData, 0, binaryData.Length);

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream streamReceive = response.GetResponseStream();
                Encoding encoding = Encoding.UTF8;

                StreamReader streamReader = new StreamReader(streamReceive, encoding);
                string strResult = streamReader.ReadToEnd();
                strResult = des.Decrypt(strResult, key);
                reqstream.Close();
                JObject obj = JObject.Parse(strResult);

                //1 - 一致，2 - 不一致， 3 - 库中无此号
                //国政通是：1：库中无此号，2：不一致，3：一致，4：核查失败
                int user_check_result = obj["rtn"].Value<int>();
                int result = 0;
                switch(user_check_result)
                {
                    case 1: user_check_result = 3; result = 0; break;
                    case 2: user_check_result = 2; result = 4; break;
                    case 3: user_check_result = 1; result = 4; break;
                    default: user_check_result = 4; result = 4; break;
                }

                string message = string.Empty;
                switch (user_check_result)
                {
                    case 1: message= "库中无此号"; break;
                    case 2: message = "不一致"; break;
                    case 3: message = "一致"; break;
                    default: message = "核查失败"; break;
                }

                return JObject.Parse(string.Format(template, result, message, user_check_result));
            }
            catch (WebException webEx)
            {
                CFDGlobal.LogException(webEx);
                Stream ReceiveStream = webEx.Response.GetResponseStream();
                Encoding encode = Encoding.GetEncoding("utf-8");

                StreamReader readStream = new StreamReader(ReceiveStream, encode);
                string message = readStream.ReadToEnd();
                CFDGlobal.LogError(message);
            }
            catch (Exception ex)
            {
                CFDGlobal.LogException(ex);
            }
            finally
            {
                request.Abort();
            }

            return JObject.Parse(string.Format(template, 4, "用户身份核查错误: 服务异常", 4));
        }
    }
}
