using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Transports;
using Newtonsoft.Json.Linq;
using System.Text;

namespace CFD_JOBS
{
    public class LoadTest
    {
        private static ConcurrentBag<ResponseInfo> _responses = new ConcurrentBag<ResponseInfo>();

        public void Run()
        {
            var defaultConnectionLimit = System.Net.ServicePointManager.DefaultConnectionLimit = 10000;

            var db = CFDEntities.Create();

            var users = db.Users.Where(o => o.AyondoUsername != null && o.Id  >= 2092 && o.Id <= 2141
                //&& o.Id == 1
                ).ToList();

            //IList<Task> tasks = new List<Task>();
            IList<Thread> threads = new List<Thread>();

            for (int i = 0; i < 2; i++)
            {
                foreach (var user in users)
                {
                    //var threadStart = new ParameterizedThreadStart(DoUserOperation);
                    var threadStart = new ParameterizedThreadStart(DoRandomAPI);

                    //var threadStart = new ThreadStart(DoUserOperation);
                    var thread = new Thread(threadStart);
                    thread.Start(user);
                    //thread.Start();
                    threads.Add(thread);

                    //var task = Task.Run(() => { DoUserOperation(user); });
                    //tasks.Add(task);
                }
            }

            //CFDGlobal.LogLine("Thread count: " + tasks.Count + " user count: " + users.Count);
            CFDGlobal.LogLine("Thread count: " + threads.Count + " user count: " + users.Count);
            CFDGlobal.LogLine("");

            //while (tasks.Any(o => !o.IsCompleted))
            //{
            //    Thread.Sleep(TimeSpan.FromSeconds(1));
            //}

            while (threads.Any(o => o.IsAlive))
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            CFDGlobal.LogLine("--------------------------Summary-----------------------");
            var groupBy = _responses.GroupBy(o => o.Url);
            foreach (var group in groupBy)
            {
                var successList = group.Where(o => o.isSuccess).ToList();
                var failCount = group.Count(o => !o.isSuccess);

                CFDGlobal.LogLine(group.Key + " Success:" + successList.Count + " Fail:" + failCount + " AvgTime:" + successList.Average(o => o.TotalMilliSecond) + " AvgLength:" +
                                  successList.Average(o => o.Length));
            }
            CFDGlobal.LogLine("--------------------------------------------------------");

            foreach (var group in groupBy)
            {
                CFDGlobal.LogLine(group.Key + ":");
                foreach (var responseInfo in group)
                {
                    CFDGlobal.LogLine(responseInfo.TotalMilliSecond.ToString());
                }
            }
        }

        private void DoUserOperation(Object obj)
        {
            var user = (User)obj;

            //-----------------Initialize----------------------
            CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " " + user.Id + " Initializing...");
            var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/stock/topGainer");
            var topGainer = GetResponseJArray(GetResponseString(request));
            var topGainerIds = topGainer.Select(o => o["id"]).Aggregate((o, n) => o + "," + n);

            //return;

            request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/index");
            var index = GetResponseJArray(GetResponseString(request));
            var indexIds = index.Select(o => o["id"]).Aggregate((o, n) => o + "," + n);

            request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/fx");
            var fx = GetResponseJArray(GetResponseString(request));
            var fxIds = fx.Select(o => o["id"]).Aggregate((o, n) => o + "," + n);

            request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/futures");
            var futures = GetResponseJArray(GetResponseString(request));
            var futuresIds = futures.Select(o => o["id"]).Aggregate((o, n) => o + "," + n);

            request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/byIds/34821,34804,34768,34858,34847,34817,34763,34864");
            var byIds = GetResponseJArray(GetResponseString(request));
            var byIdsIds = byIds.Select(o => o["id"]).Aggregate((o, n) => o + "," + n);

            request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/banner");
            var banner = GetResponseJArray(GetResponseString(request));

            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/open");
            //request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
            //var open = GetResponseJArray(GetResponseString(request));

            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/closed");
            //request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
            //var closed = GetResponseJArray(GetResponseString(request));

            var hubConnection = new HubConnection("http://cfd-webapi.chinacloudapp.cn/signalR");
            IHubProxy hub = hubConnection.CreateHubProxy("Q");
            hub.On("p", stocks =>
            {
                //foreach (var stock in stocks)
                //{
                //    CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " received " + (string) stock.id + " " + (string) stock.last);
                //}
                //CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " received " + stocks.Count + " stock quotes");
            });

            hubConnection.Start(new LongPollingTransport());

            while (hubConnection.State != ConnectionState.Connected)
            {
                //CFDGlobal.LogLine(hubConnection.State.ToString());
                Thread.Sleep(1000);
            }

            //-----------------Loop----------------------
            var dt = DateTime.UtcNow;
            while (DateTime.UtcNow - dt < TimeSpan.FromMinutes(3)) //////////////////test for how long
            {
                CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " " + user.Id + " Looping...");
                Thread.Sleep(GetRandomIdleTime());

                //自选
                hub.Invoke("S", byIdsIds);
                Thread.Sleep(GetRandomIdleTime());

                //美股
                hub.Invoke("S", topGainerIds);
                Thread.Sleep(GetRandomIdleTime());

                //美股详情
                var secId = GetRandomElement(topGainer)["id"];
                secId = 34768;
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/" + secId);
                var security = GetResponseJObject(GetResponseString(request));
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/quote/" + secId + "/tick/today");
                var tick = GetResponseJArray(GetResponseString(request));
                //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/user/balance");
                //request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
                //var balance = GetResponseJObject(GetResponseString(request));
                Thread.Sleep(GetRandomIdleTime());

                //返回美股
                hub.Invoke("S", topGainerIds);
                Thread.Sleep(GetRandomIdleTime());

                //外汇
                hub.Invoke("S", fxIds);
                Thread.Sleep(GetRandomIdleTime());

                //外汇详情
                secId = GetRandomElement(fx)["id"];
                secId = 34804;
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/" + secId);
                security = GetResponseJObject(GetResponseString(request));
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/quote/" + secId + "/tick/today");
                tick = GetResponseJArray(GetResponseString(request));
                Thread.Sleep(GetRandomIdleTime());

                //返回外汇
                hub.Invoke("S", fxIds);
                Thread.Sleep(GetRandomIdleTime());

                //指数
                hub.Invoke("S", indexIds);
                Thread.Sleep(GetRandomIdleTime());

                //指数详情
                secId = GetRandomElement(index)["id"];
                secId = 34858;
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/" + secId);
                security = GetResponseJObject(GetResponseString(request));
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/quote/" + secId + "/tick/today");
                tick = GetResponseJArray(GetResponseString(request));
                Thread.Sleep(GetRandomIdleTime());

                //返回指数
                hub.Invoke("S", indexIds);
                Thread.Sleep(GetRandomIdleTime());

                //商品
                hub.Invoke("S", futuresIds);
                Thread.Sleep(GetRandomIdleTime());

                //商品详情
                secId = GetRandomElement(futures)["id"];
                secId = 34821;
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/" + secId);
                security = GetResponseJObject(GetResponseString(request));
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/quote/" + secId + "/tick/today");
                tick = GetResponseJArray(GetResponseString(request));
                Thread.Sleep(GetRandomIdleTime());

                //返回商品
                hub.Invoke("S", futuresIds);
                Thread.Sleep(GetRandomIdleTime());

                ////持仓
                //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/open");
                //request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
                //open = GetResponseJArray(GetResponseString(request));
                //Thread.Sleep(GetRandomIdleTime());

                ////平仓
                //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/closed");
                //request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
                //closed = GetResponseJArray(GetResponseString(request));
                //Thread.Sleep(GetRandomIdleTime());

                ////统计
                //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/user/plReport");
                //request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
                //var plReport = GetResponseJArray(GetResponseString(request));

            }

            //-----------------End----------------------
            hubConnection.Stop();
        }

        private void DoRandomAPI(Object obj)
        {
            var user = (User)obj;

            var dt = DateTime.UtcNow;
            while (DateTime.UtcNow - dt < TimeSpan.FromMinutes(1)) //////////////////test for how long
            {
            //Thread.Sleep(GetRandomIdleTime());
            //var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/stock/topGainer");
            //var topGainer = GetResponseJArray(GetResponseString(request));

            //Thread.Sleep(GetRandomIdleTime());
            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/index");
            //var index = GetResponseJArray(GetResponseString(request));

            //Thread.Sleep(GetRandomIdleTime());
            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/fx");
            //var fx = GetResponseJArray(GetResponseString(request));

            //Thread.Sleep(GetRandomIdleTime());
            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/futures");
            //var futures = GetResponseJArray(GetResponseString(request));

            //Thread.Sleep(GetRandomIdleTime());
            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/byIds/34821,34804,34768,34858,34847,34817,34763,34864");
            //var byIds = GetResponseJArray(GetResponseString(request));

            //Thread.Sleep(GetRandomIdleTime());
            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/banner");
            //var banner = GetResponseJArray(GetResponseString(request));

            var secId = 34804; //fx

            //Thread.Sleep(GetRandomIdleTime());
            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/" + secId);
            //var security = GetResponseJObject(GetResponseString(request));

            Thread.Sleep(GetRandomIdleTime());
            var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/quote/" + secId + "/tick/10m");
            var tick = GetResponseJArray(GetResponseString(request));

            Thread.Sleep(GetRandomIdleTime());
            request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/quote/" + secId + "/tick/2h");
            tick = GetResponseJArray(GetResponseString(request));

            //Thread.Sleep(GetRandomIdleTime());
            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/quote/" + secId + "/tick/week");
            //tick = GetResponseJArray(GetResponseString(request));

            //Thread.Sleep(GetRandomIdleTime());
            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/quote/" + secId + "/tick/month");
            //tick = GetResponseJArray(GetResponseString(request));


            ////持仓
            ////Thread.Sleep(GetRandomIdleTime());
            //var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/open");
            //request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
            //var open = GetResponseJArray(GetResponseString(request));

            ////平仓
            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/closed");
            //request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
            //closed = GetResponseJArray(GetResponseString(request));
            //Thread.Sleep(GetRandomIdleTime());

            ////统计
            //request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/user/plReport");
            //request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
            //var plReport = GetResponseJArray(GetResponseString(request));
            }

            ////建仓
            //Thread.Sleep(GetRandomIdleTime());
            //JObject pos = OpenPosition("34820", 2500, 1, user.Id, user.Token);
            //Thread.Sleep(GetRandomIdleTime());
            ////平仓
            //if(pos != null)
            //{
            //    ClosePosition(pos["id"].Value<string>(), pos["security"]["id"].Value<string>(), pos["quantity"].Value<decimal>(), user.Id, user.Token);
            //}

            //测试
            //TestWCF();

        }

        private string TestWCF()
        {
            var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/misc/wcf");
            request.Method = "get";
            //request.ContentType = "application/json";

            return GetResponseString(request);
        }


        private string GetResponseString(WebRequest request)
        {
            var url = request.RequestUri.AbsolutePath;

            var isSuccess = true;
            string result = string.Empty;
            double totalMilliseconds = 0;

            try
            {
                var dtBegin = DateTime.UtcNow;
                CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " getting " + url + "...");
                using (var response = request.GetResponseAsync().Result)
                {
                    using (var responseStream = response.GetResponseStream())
                    {
                        using (var sr = new StreamReader(responseStream))
                        {
                            result = sr.ReadToEnd();
                            totalMilliseconds = (DateTime.UtcNow - dtBegin).TotalMilliseconds;
                            //CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " end " + url + " -> " + totalMilliseconds);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                isSuccess = false;
            }

            _responses.Add(new ResponseInfo()
            {
                Url = url,
                isSuccess = isSuccess,
                TotalMilliSecond = totalMilliseconds,
                Length = result.Length
            });

            return result;
        }

        private JArray GetResponseJArray(string str)
        {
            if (string.IsNullOrEmpty(str)) return null;

            return JArray.Parse(str);
        }

        private JObject GetResponseJObject(string str)
        {
            if (string.IsNullOrEmpty(str)) return null;

            return JObject.Parse(str);
        }

        private TimeSpan GetRandomIdleTime()
        {
            var r = new Random();
            return TimeSpan.FromSeconds(r.Next(1, 6)); //1~x second
        }

        private JToken GetRandomElement(JArray arr)
        {
            var r = new Random();
            return arr[r.Next(0, arr.Count)];
        }

        /// <summary>
        /// 建仓
        /// </summary>
        /// <param name="securityId"></param>
        /// <param name="amount"></param>
        /// <param name="leverage"></param>
        private JObject OpenPosition(string securityId, int amount, int leverage, int userId, string userToken)
        {
            //德国DAX30
            string jsonData = "{\"securityId\":" + securityId + ",\"isLong\":false,\"invest\":" + amount + ",\"leverage\":" + leverage + "}";
            var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position");
            request.Headers["Authorization"] = string.Format("Basic {0}_{1}", userId, userToken);
            request.Method = "post";
            request.ContentType = "application/json";
            byte[] datas = Encoding.UTF8.GetBytes(jsonData);
            request.ContentLength = datas.Length;
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(datas, 0, datas.Length);

            return GetResponseJObject(GetResponseString(request));

            //var dtBegin = DateTime.UtcNow;

            //Stream requestStream = request.GetRequestStream();
            //try
            //{
            //    requestStream.Write(datas, 0, datas.Length);
            //    Stream streamResponse = request.GetResponse().GetResponseStream();
            //    using (StreamReader sr = new StreamReader(streamResponse))
            //    {
            //        string responseDatas = sr.ReadToEnd();
            //        var serializer = new DataContractJsonSerializer(typeof(Position));
            //        var mStream = new MemoryStream(Encoding.Default.GetBytes(responseDatas));
            //        Position pos = (Position)serializer.ReadObject(mStream);
            //        double totalMilliseconds = (DateTime.UtcNow - dtBegin).TotalMilliseconds;
            //        CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " end " + request.RequestUri.AbsolutePath + " -> " + totalMilliseconds);
            //        return pos;
            //    }
            //}
            //finally
            //{
            //    requestStream.Close();
            //}
        }

        private void ClosePosition(string posID, string securityId, decimal qty, int userId, string userToken)
        {
            //德国DAX30
            string jsonData = "{\"posId\":\"" + posID + "\",\"securityId\":" + securityId + ",\"isPosLong\":false,\"posQty\":" + qty + "}";
            var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/net");
            request.Headers["Authorization"] = string.Format("Basic {0}_{1}", userId, userToken);
            request.Method = "post";
            request.ContentType = "application/json";
            byte[] datas = Encoding.UTF8.GetBytes(jsonData);
            request.ContentLength = datas.Length;
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(datas, 0, datas.Length);

            GetResponseString(request);
        }
    }

    public class ResponseInfo
    {
        public string Url { get; set; }
        public bool isSuccess { get; set; }
        public double TotalMilliSecond { get; set; }
        public int Length { get; set; }
    }
}