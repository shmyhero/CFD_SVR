using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CFD_API.DTO;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Net;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTestRefund
    {
        /// <summary>
        /// 出金绑银行卡接口
        /// </summary>
        [TestMethod]
        public void ReferenceAccount()
        {
            #region 以下是Richard的注册信息
            //LiveUserBankCardOriginalFormDTO dto = new LiveUserBankCardOriginalFormDTO();
            //dto.AccountHolder = "朱桂鑫";
            //dto.AccountNumber = "6225 2101 1249 1635";
            //dto.AddressOfBank = "上海市浦东新区浦东南路588号2楼";
            //dto.Guid = "5c682e17-abc7-11e6-80d9-002590d644df";
            //dto.NameOfBank = "Shanghai Pudong Development Bank";
            //dto.SwiftCode = "SPDBCNSHXXX";
            //byte[] binaryData = Encoding.UTF8.GetBytes(GetJSON<LiveUserBankCardOriginalFormDTO>(dto));

            //string auth = "Basic 8_5c037cd9e93d48adbf5e8ad4743570b6";
            #endregion

            #region 以下是Windy的注册信息
            LiveUserBankCardOriginalFormDTO dto = new LiveUserBankCardOriginalFormDTO();
            dto.AccountHolder = "胡海平";
            dto.AccountNumber = "1217 3699 8011 0345 268";
            dto.AddressOfBank = "上海市淮海中路200号";
            //dto.Guid = "a15a7422-aba7-11e6-80d9-002590d644df";
            dto.NameOfBank = "建设银行";
            dto.Branch = "建设银行上海分行";
            dto.Province = "上海";
            dto.City = "上海";
            //dto.SwiftCode = "PCBCCNBJSHX";
            byte[] binaryData = Encoding.UTF8.GetBytes(GetJSON<LiveUserBankCardOriginalFormDTO>(dto));

            string auth = "Basic 2030_a1a4961928344332be8346e4cc289b0f";
            #endregion

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.typhoontechnology.hk/api/user/live/refaccount");
            try
            {

                request.ContentType = "application/json";
                request.Method = "POST";
                request.Headers.Add("Authorization", auth);
                request.ContentLength = binaryData.Length;
                request.Timeout = int.MaxValue;
                Stream reqstream = request.GetRequestStream();
                reqstream.Write(binaryData, 0, binaryData.Length);

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream streamReceive = response.GetResponseStream();
                Encoding encoding = Encoding.UTF8;

                StreamReader streamReader = new StreamReader(streamReceive, encoding);
                string strResult = streamReader.ReadToEnd();
                Console.WriteLine(strResult);
                reqstream.Close();
                return;
            }
            catch (WebException webEx)
            {
                throw new WebException("操作超时");
            }
            catch (Exception ex)
            {
                return;
            }
            finally
            {
                request.Abort();
            }

        }

        public void WithDraw()
        {
            LiveUserRefundDTO dto = new LiveUserRefundDTO();
            dto.Amount = 1.00M;
            byte[] binaryData = Encoding.UTF8.GetBytes(GetJSON<LiveUserRefundDTO>(dto));

            string auth = "Basic 2030_a1a4961928344332be8346e4cc289b0f";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.typhoontechnology.hk/api/user/live/withdraw");
            try
            {

                request.ContentType = "application/json";
                request.Method = "POST";
                request.Headers.Add("Authorization", auth);
                request.ContentLength = binaryData.Length;
                request.Timeout = int.MaxValue;
                Stream reqstream = request.GetRequestStream();
                reqstream.Write(binaryData, 0, binaryData.Length);

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream streamReceive = response.GetResponseStream();
                Encoding encoding = Encoding.UTF8;

                StreamReader streamReader = new StreamReader(streamReceive, encoding);
                string strResult = streamReader.ReadToEnd();
                Console.WriteLine(strResult);
                reqstream.Close();
                return;
            }
            catch (WebException webEx)
            {
                throw new WebException("操作超时");
            }
            catch (Exception ex)
            {
                return;
            }
            finally
            {
                request.Abort();
            }
        }


        public static string GetJSON<T>(T jsonObj)
        {
            DataContractJsonSerializer ds = new DataContractJsonSerializer(typeof(T));
            using (MemoryStream ms = new MemoryStream())
            {
                ds.WriteObject(ms, jsonObj);
                string json = Encoding.UTF8.GetString(ms.ToArray());
                return json;
            }
        }
    }
}
