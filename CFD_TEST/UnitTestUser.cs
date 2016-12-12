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
    public class UnitTestUser
    {
        [TestMethod]
        public void GetBalance()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.typhoontechnology.hk/api/user/live/balance");
            try
            {
                request.ContentType = "application/json";
                request.Method = "GET";
                request.Headers.Add("Authorization", "Basic 8_f5c5e882e897462584dc00c16356c975");
                request.Timeout = int.MaxValue;
             
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream streamReceive = response.GetResponseStream();
                Encoding encoding = Encoding.UTF8;

                StreamReader streamReader = new StreamReader(streamReceive, encoding);
                string strResult = streamReader.ReadToEnd();
                Console.WriteLine(strResult);
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

        [TestMethod]
        public void PushToken()
        {
            string requestStr = "{\"deviceToken\":\"f60c5d5a898b1c19cb6e5d58520c8906\",\"deviceType\":1}";
            byte[] binaryData = Encoding.UTF8.GetBytes(requestStr);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.typhoontechnology.hk/api/user/pushtokenauth");
            //HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:11033/api/user/pushtokenauth");

            try
            {
                request.ContentType = "application/json";
                request.Method = "POST";
                request.Headers.Add("Authorization", "Basic 2031_b58b17d610c747d5a03356b03565719d");
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
    }
}
