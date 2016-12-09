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
    }
}
