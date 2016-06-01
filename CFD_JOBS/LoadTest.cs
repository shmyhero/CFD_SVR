using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Transports;
using Newtonsoft.Json.Linq;

namespace CFD_JOBS
{
    public class LoadTest
    {
        private static IDictionary<string, IList<ResponseInfo>> _urlResponseTime = new Dictionary<string, IList<ResponseInfo>>();
        private static IDictionary<string, IList<bool>> _urlIsSuccess = new Dictionary<string, IList<bool>>();

        public static void Run()
        {
            var db = CFDEntities.Create();

            var users = db.Users.Where(o => o.AyondoUsername != null
                //&& o.Id == 1
                ).ToList();

            IList<Task> tasks = new List<Task>();

            for (int i = 0; i < 1; i++)
            {
                foreach (var user in users)
                {
                    var task = Task.Run(() => { DoUserOperation(user); });
                    tasks.Add(task);
                }
            }

            CFDGlobal.LogLine("Thread count: " + tasks.Count + " user count: " + users.Count);
            CFDGlobal.LogLine("");

            while (tasks.Any(o => !o.IsCompleted))
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            foreach (var task in tasks)
            {
                //CFDGlobal.LogLine();
            }

            CFDGlobal.LogLine("Response Time:");
            foreach (var pair in _urlResponseTime)
            {
                CFDGlobal.LogLine(pair.Key + " Average Time:" + pair.Value.Average(o => o.TotalMilliSecond) + " Average Length:" + pair.Value.Average(o => o.Length) + " Count:" +
                                  pair.Value.Count);
            }
            CFDGlobal.LogLine("");

            CFDGlobal.LogLine("Response Success Rate:");
            foreach (var pair in _urlIsSuccess)
            {
                CFDGlobal.LogLine(pair.Key + " Success:" + pair.Value.Count(o => o) + " Fail:" + pair.Value.Count(o => !o));
            }
            CFDGlobal.LogLine("");
        }

        private static void DoUserOperation(User user)
        {
            //-----------------Initialize----------------------
            CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " " + user.Id + " Initializing...");
            var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/security/stock/topGainer");
            var topGainer = GetResponseJArray(GetResponseString(request));
            var topGainerIds = topGainer.Select(o => o["id"]).Aggregate((o, n) => o + "," + n);

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

            request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/open");
            request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
            var open = GetResponseJArray(GetResponseString(request));

            request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/closed");
            request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
            var closed = GetResponseJArray(GetResponseString(request));

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
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/user/balance");
                request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
                var balance = GetResponseJObject(GetResponseString(request));
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

                //持仓
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/open");
                request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
                open = GetResponseJArray(GetResponseString(request));
                Thread.Sleep(GetRandomIdleTime());

                //平仓
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/closed");
                request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
                closed = GetResponseJArray(GetResponseString(request));
                Thread.Sleep(GetRandomIdleTime());

                //统计
                request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/user/plReport");
                request.Headers["Authorization"] = "Basic " + user.Id + "_" + user.Token;
                var plReport = GetResponseJArray(GetResponseString(request));
            }

            //-----------------End----------------------
            hubConnection.Stop();
        }

        private static string GetResponseString(WebRequest request)
        {
            CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " getting " + request.RequestUri.AbsolutePath + "...");

            var isSuccess = true;
            string result = null;
            var dtBegin = DateTime.UtcNow;

            try
            {
                var response = request.GetResponse();
                var responseStream = response.GetResponseStream();
                var sr = new StreamReader(responseStream);
                result = sr.ReadToEnd();
                sr.Close();
                responseStream.Close();
                response.Close();
            }
            catch
            {
                isSuccess = false;
            }

            var dtEnd = DateTime.UtcNow;

            var totalMilliseconds = (dtEnd - dtBegin).TotalMilliseconds;

            var key = request.RequestUri.AbsolutePath;

            if (isSuccess)
            {
                //CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " getting " + request.RequestUri.AbsolutePath + " -> " + totalMilliseconds);

                //lock (_urlResponseTime)
                //{
                if (_urlResponseTime.ContainsKey(key))
                    _urlResponseTime[key].Add(new ResponseInfo()
                    {
                        TotalMilliSecond = totalMilliseconds,
                        Length = result.Length,
                    });
                else
                    _urlResponseTime.Add(key, new List<ResponseInfo>()
                    {
                        new ResponseInfo()
                        {
                            TotalMilliSecond = totalMilliseconds,
                            Length = result.Length,
                        }
                    });
                //}
            }

            //lock (_urlIsSuccess)
            //{
            if (_urlIsSuccess.ContainsKey(key))
                _urlIsSuccess[key].Add(isSuccess);
            else
                _urlIsSuccess.Add(key, new List<bool>() {isSuccess});
            //}

            return result;
        }

        private static JArray GetResponseJArray(string str)
        {
            if (str == null) return null;

            return JArray.Parse(str);
        }

        private static JObject GetResponseJObject(string str)
        {
            if (str == null) return null;

            return JObject.Parse(str);
        }

        private static TimeSpan GetRandomIdleTime()
        {
            var r = new Random();
            return TimeSpan.FromSeconds(r.Next(1, 6)); //1~x second
        }

        private static JToken GetRandomElement(JArray arr)
        {
            var r = new Random();
            return arr[r.Next(0, arr.Count)];
        }
    }

    public class ResponseInfo
    {
        public double TotalMilliSecond { get; set; }
        public int Length { get; set; }
    }
}