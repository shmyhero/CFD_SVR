using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTestWeCollect
    {
        [TestMethod]
        public void FxTest()
        {
            string symbol = "USDCNY";

            //9a56bc7a-bacb-11e6-a5cc-0211eb00a4cc
            //eccef492-babf-11e6-8b97-0211eb00a4cc
            //79c0a634-bacf-11e6-a3f5-0211eb00a4cc
            string merchantId = "79c0a634-bacf-11e6-a3f5-0211eb00a4cc";
            string url = string.Format("http://fxrate.wecollect.com/service/getrate?symbol={0}&merchantid={1}",symbol,merchantId);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            try
            {
                request.ContentType = "application/json";
                request.Method = "GET";
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
                throw new WebException("");
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
