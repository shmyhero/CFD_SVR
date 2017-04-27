using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.Threading;
using AyondoTrade;
using CFD_API;
using CFD_API.Controllers;
using CFD_API.DTO.FormDTO;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using ServiceStack.Redis.Generic;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;
using AutoMapper;
using CFD_API.DTO;
using CFD_JOBS;
using EntityFramework.Extensions;
using Newtonsoft.Json.Linq;
using Pinyin4net;
using Pinyin4net.Format;
using ServiceStack.ServiceHost;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void AMS_ReferenceAccount()
        {
            LiveUserBankCardFormDTO form = new LiveUserBankCardFormDTO()
            {
                //accountHolder = "test",
                //accountNumber = "test",
                //nameOfBank = "test",
                //bankStatementContent = "test",
                //bankStatementFilename = "test",
                //Guid = "test",
                //Branch = "test",
                //Province = "test",
                //City = "test"
            };

            var httpWebRequest = WebRequest.CreateHttp("https://lab1-www.ayondo-ams.com/tradeherocn/" + "live-account/2882e16b-a1a1-11e6-80d9-002590d644df/reference-account");
            httpWebRequest.Headers["Authorization"] = "Bearer NkJDMUQzNkQtMzg2OS00NEZELUIzOUMtODQ4MkUzMTAyMTk0MzRBNDYyMkQtODQ1MC00MDA4LTlFRUUtMEIwRkFENzQ3QUY4";
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json; charset=UTF-8";
            httpWebRequest.Proxy = null;
            httpWebRequest.Timeout = int.MaxValue;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);
            var s = JsonConvert.SerializeObject(form);
            sw.Write(s);
            sw.Flush();
            sw.Close();

            var dtBegin = DateTime.UtcNow;

            WebResponse webResponse;
            try
            {
                webResponse = httpWebRequest.GetResponse();
            }
            catch (WebException e)
            {
                webResponse = e.Response;
            }

            var responseStream = webResponse.GetResponseStream();
            var sr = new StreamReader(responseStream);

            var str = sr.ReadToEnd();
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("AMS reference-account called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            var json = JToken.Parse(str);

        }

        [TestMethod]
        public void Test1()
        {
            //using (var db = CFDEntities.Create())
            //{
            //    CFD_COMMON.Models.Entities.Message msg = new CFD_COMMON.Models.Entities.Message();
            //    msg.Title = "测试";
            //    msg.Body = "测试内容";
            //    msg.CreatedAt = DateTime.UtcNow;
            //    msg.IsReaded = false;

            //    db.Messages.Add(msg);
            //    db.SaveChanges();
            //    int id = msg.Id;
            //} 
        }

        [TestMethod]
        public void DbTest()
        {
            using (var db = CFDEntities.Create())
            {
                //var pos = db.AyondoTradeHistory_Live.FirstOrDefault(o => o.Id == 1);
                //pos.PL = 12345;
                //db.SaveChanges();


                //var a = 1 == 1
                //    ? (AyondoTradeHistoryBase)db.AyondoTradeHistories.FirstOrDefault()
                //    : db.AyondoTradeHistory_Live.FirstOrDefault();
                //a.PL = 11;
                //var aa = 1 == 2
                //    ? (AyondoTradeHistoryBase)db.AyondoTradeHistories.FirstOrDefault()
                //    : db.AyondoTradeHistory_Live.FirstOrDefault();
                //aa.PL = 11;
                //db.SaveChanges();


                //var ooo = new AyondoTradeHistoryBase() { PositionId = 1 };
                //var mapper = CFD_COMMON.MapperConfig.GetAutoMapperConfiguration().CreateMapper();
                //db.AyondoTradeHistories.Add(mapper.Map<AyondoTradeHistory>(ooo));
                //db.AyondoTradeHistory_Live.Add(mapper.Map<AyondoTradeHistory_Live>(ooo));
                //db.SaveChanges();


                //var newPositionHistory = db.NewPositionHistories.FirstOrDefault();
                //var newPositionHistory_Live = db.NewPositionHistory_live.FirstOrDefault();

                //var newPositionHistories = db.NewPositionHistories.Take(1).ToList();
                //var newPositionHistories_Live = db.NewPositionHistory_live.Take(1).ToList();

                //var a = 1 == 1 ? db.NewPositionHistories.FirstOrDefault() : db.NewPositionHistory_live.FirstOrDefault();
                //var aa = 1 == 2 ? db.NewPositionHistories.FirstOrDefault() : db.NewPositionHistory_live.FirstOrDefault();

                var b = 1 == 1 ? db.NewPositionHistories.Take(1).ToList().Select(o=>o as NewPositionHistoryBase).ToList() : db.NewPositionHistory_live.Take(1).ToList().Select(o => o as NewPositionHistoryBase).ToList();
                var bb = 1 == 2 ? db.NewPositionHistories.Take(1).ToList().Select(o => o as NewPositionHistoryBase).ToList() : db.NewPositionHistory_live.Take(1).ToList().Select(o => o as NewPositionHistoryBase).ToList();
                b[0].PL = 22;
                bb[0].PL = 33;
                db.SaveChanges();
            }
        }
        
        [TestMethod]
        public void Import()
        {
            var prodDefClient = CFDGlobal.GetNewBasicRedisClientManager().GetClient().As<ProdDef>();
            var klineClient = CFDGlobal.GetNewBasicRedisClientManager().GetClient().As<KLine>();
            var listProd =  prodDefClient.GetAll();
            var db = CFDEntities.Create();

            foreach( var prod in listProd)
            {
                if(prod.Name.IndexOf(" Outright") > -1 || prod.QuoteType == enmQuoteType.Inactive)
                {
                    continue;
                } 

                if(!prod.LastOpen.HasValue || !prod.LastClose.HasValue)
                {
                    continue;
                }

                var openTime = prod.LastOpen.Value;
                var closeTime = prod.LastClose.Value;

                if (openTime > closeTime)//opening
                    //openTime = openTime.AddDays(-1);
                    closeTime = closeTime.AddDays(1);

                //TimeSpan openPeriod; //开始时常
                //if(openTime > closeTime) //获取开市时常
                //{
                //    openPeriod = (closeTime - openTime).Add(new TimeSpan(24,0,0));
                //}
                //else
                //{
                //    openPeriod = closeTime - openTime;
                //}

                List <KLine> lines = new List<KLine>();
                for (int x = 0; x<35; x++) //到今天总共35天
                {
                    DateTime lastOpenTime = openTime.AddDays(-x);
                    DateTime lastCloseTime = closeTime.AddDays(-x);
                   var quoteList = db.QuoteHistories.Where(o => o.SecurityId == prod.Id 
                   //&& o.Time < prod.LastClose.Value.Subtract(TimeSpan.FromDays(x)) 
                   //&& o.Time > prod.LastClose.Value.Subtract(TimeSpan.FromDays(x)).Subtract(openPeriod)
                   && o.Time> lastOpenTime
                   && o.Time< lastCloseTime
                   ).OrderBy(o => o.Time).ToList();

                    if(quoteList == null || quoteList.Count == 0)
                    {
                        continue;
                    }

                    KLine line = new KLine();
                    line.Open = GetLastPrice(quoteList[0].Ask.Value, quoteList[0].Bid.Value);
                    line.Close = GetLastPrice(quoteList[quoteList.Count - 1].Ask.Value, quoteList[quoteList.Count - 1].Bid.Value);
                    line.High = quoteList.Max(o => GetLastPrice(o.Ask.Value, o.Bid.Value));
                    line.Low = quoteList.Min(o => GetLastPrice(o.Ask.Value, o.Bid.Value));

                    line.Time = openTime.AddDays(-x).AddHours(8).Date;

                    lines.Add(line);
                }

                var list = klineClient.Lists["kline1d:" + prod.Id];
                list.RemoveAll();
                list.AddRange(lines.OrderBy(o => o.Time));
            }
        }

        private decimal GetLastPrice(decimal offer, decimal bid)
        {
            int c1 = BitConverter.GetBytes(decimal.GetBits(offer)[3])[2];
            int c2 = BitConverter.GetBytes(decimal.GetBits(bid)[3])[2];
            int decimalCount = Math.Max(c1, c2);

            return Math.Round((offer + bid) / 2, decimalCount, MidpointRounding.AwayFromZero);
        }
        /// <summary>
        /// Push test on android
        /// </summary>
        [TestMethod]
        public void PushTest()
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            list.Add(new KeyValuePair<string, string>("f60c5d5a898b1c19cb6e5d58520c8906", "{\"type\":\"1\", \"title\":\"盈交易\", \"StockID\":34847, \"CName\":\"白银\", \"message\":\"Andy的推送测试\"}"));
            //list.Add(new KeyValuePair<string, string>("61711bba906ec8723c391172df3850a3", "{\"type\":\"1\", \"title\":\"盈交易\", \"StockID\":34847, \"CName\":\"白银\", \"message\":\"Andy的推送测试\"}"));
           
            //list.Add(new KeyValuePair<string, string>("6a67a54402ca0a4c755ebda7b754ab32", "{\"type\":\"1\", \"title\":\"盈交易\", \"StockID\":34847, \"CName\":\"白银\", \"message\":\"白银于2016/09/06 10:19平仓，价格为200.00美元,已亏损100美元\"}"));
            //list.Add(new KeyValuePair<string, string>("749f7136cf13c8669ef97222557ba982", "{\"type\":\"1\", \"title\":\"盈交易\", \"StockID\":34847, \"CName\":\"白银\", \"message\":\"白银于2016/09/06 10:19平仓，价格为200.00美元,已亏损100美元\"}"));

            var push = new GeTui();
            var response = push.PushBatch(list);
        }

        [TestMethod]
        public void GetDeviceToken()
        {
            using (var db = CFDEntities.Create())
            {
                var query = from u in db.Users
                            join d in db.Devices on u.Id equals d.userId
                            into x
                            from y in x.DefaultIfEmpty()
                            where u.Id == 3256
                            select new { y.deviceToken, UserId = u.Id, u.AyondoAccountId, u.AyLiveAccountId, u.AutoCloseAlert, u.AutoCloseAlert_Live, u.IsOnLive };

                var users = query.ToList();
            }
        }
        
        [TestMethod]
        public void ServiceStackJsonConfig()
        {
            //var basicRedisClientManager = CFDGlobal.GetNewBasicRedisClientManager();
            var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();
            var redisTypedClient = redisClient.As<ProdDef>();

            redisTypedClient.DeleteById(1);
            redisTypedClient.DeleteById(2);

            var nowUTC = DateTime.UtcNow;
            var nowLocal = DateTime.Now;

            redisTypedClient.Store(new ProdDef()
            {
                Id = 1,
                Name = "test",
                QuoteType = enmQuoteType.PhoneOnly,
                Symbol = "symbol",
                Time = nowUTC
            });

            ProdDef prodDef = redisTypedClient.GetById(1);
            Assert.AreEqual(enmQuoteType.PhoneOnly, prodDef.QuoteType);
            Assert.AreEqual(DateTimeKind.Utc, prodDef.Time.Kind);
            Assert.AreEqual(nowUTC, prodDef.Time);

            redisTypedClient.Store(new ProdDef()
            {
                Id = 1,
                Name = "test",
                QuoteType = enmQuoteType.PhoneOnly,
                Symbol = "symbol",
                Time = nowLocal
            });
            prodDef = redisTypedClient.GetById(1);
            Assert.AreEqual(DateTimeKind.Utc, prodDef.Time.Kind);
            Assert.AreEqual(nowLocal, prodDef.Time.ToLocalTime());

            redisTypedClient.DeleteById(1);
            redisTypedClient.DeleteById(2);
        }

        [TestMethod]
        public void JsonDateTimeConvert()
        {
            //JsonConvert.DefaultSettings.
            var utc = JsonConvert.SerializeObject(DateTime.UtcNow);
            var local = JsonConvert.SerializeObject(DateTime.Now);
            var unspecify = JsonConvert.SerializeObject(new DateTime(2008, 12, 28));
        }

        [TestMethod]
        public void RedisTest()
        {
            var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();

            redisClient.SetEntry("key1", "value1");

            var value = redisClient.GetValue("key1");

            Assert.AreEqual("value1", value);

            redisClient.RemoveEntry(new[] {"key1"});
        }

        [TestMethod]
        public void WCFTest()
        {
            //EndpointAddress edpTcp = new EndpointAddress("net.tcp://localhost:38113/ayondotradeservice.svc");
            //EndpointAddress edpHttp = new EndpointAddress("http://localhost:38113/ayondotradeservice.svc");
            //EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");

            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient();
            //using (OperationContextScope scope = new OperationContextScope(clientHttp.InnerChannel))
            //{
            //    MessageHeader myHeader = MessageHeader.CreateHeader(
            //        "token", "TH", Guid.NewGuid().ToString());
            //    OperationContext.Current.OutgoingMessageHeaders.Add(myHeader);

            //    var positionReport = clientHttp.Test("jiangyi1985");
            //}
            try
            {

            clientHttp.TestException();
            }
            catch (Exception e)
            {
                if (e is ExceptionDetail)
                {
                    
                }

                if (e is FaultException)
                {
                    
                }

                var exceptionDetail = ((FaultException<ExceptionDetail>) e).Detail;
            }

            //using (OperationContextScope scope = new OperationContextScope(clientHttp.InnerChannel))
            //{
            //    MessageHeader myHeader = MessageHeader.CreateHeader(
            //            "header", "http://my", Guid.NewGuid().ToString());
            //    OperationContext.Current.OutgoingMessageHeaders.Add(myHeader);
            //}




            //var r1 = clientTcp.Test("haha tcp");
            //var r2 = clientHttp.Test("haha http");


            //// 创建Binding  
            //NetTcpBinding tcpBinding = new NetTcpBinding(SecurityMode.None);
            //BasicHttpBinding httpBinding = new BasicHttpBinding(BasicHttpSecurityMode.None);

            //// 创建通道  
            //ChannelFactory<IAyondoTradeService> factoryTcp = new ChannelFactory<IAyondoTradeService>(tcpBinding);
            //var channelTcp = factoryTcp.CreateChannel(edpTcp);

            //ChannelFactory<IAyondoTradeService> factoryHttp = new ChannelFactory<IAyondoTradeService>(httpBinding);
            //var channelHttp = factoryHttp.CreateChannel(edpHttp);

            //// 调用  
            //var r1 = channelTcp.Test("haha tcp");
            //var r2 = channelHttp.Test("haha http");

            //// 关闭通道  
            //((IClientChannel)channelHttp).Close();  
        }

        [TestMethod]
        public void RemoveInvalidQuotes()
        {
            var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();
            var redisProdDefClient = redisClient.As<ProdDef>();
            var redisTickClient = redisClient.As<Tick>();

            var prodDefs = redisProdDefClient.GetAll();

            foreach (var prodDef in prodDefs)
            {
                CFDGlobal.LogLine(prodDef.Id + "...");

                IRedisList<Tick> redisList = redisTickClient.Lists["tick10m:" + prodDef.Id];

                if (redisList.Count == 0)
                    continue;

                var list = redisList.GetAll();
                var last = list[list.Count - 1];
                var last2 = list[list.Count - 2];

                if (last.Time.Year == 2016 && last.Time.Month == 4 && last.Time.Day == 27 && last.Time.Hour == 2 && last.Time.Minute == 23
                    && last.Time - last2.Time >= TimeSpan.FromHours(1))
                {
                    var totalHours = (last.Time - last2.Time).TotalHours;
                    CFDGlobal.LogLine(prodDef.Id + ":" + totalHours.ToString());

                    var tick = redisTickClient.Lists["tick:" + prodDef.Id].Pop();
                }
            }
        }

        [TestMethod]
        public void YunPianSMS()
        {
            //var sendSms = YunPianMessenger.SendSms("【盈交易】陛下，您在盈交易平台“比收益”活动中名列前茅，奉上影券1张，请查收。"+
            //    "券号：G1609191740235366 密码：FN6AVSND57D7" +
            //    "（请在格瓦拉生活网兑换使用，全国通兑，2D和3D场次均可使用，具体使用规则以格瓦拉平台为准）。", "15821399619");
            //CFDGlobal.LogLine(sendSms);

            List<string> mobiles = new List<string>() { "13764349804", "13524574698", "15021499382" };
            List<string> coupons = new List<string>() { "st7EMP11312348MS", "st7N4QA85812862M", "st9L4XC85805049E" };
            string format = "【盈交易】陛下，您在盈交易平台“比收益”活动中名列前茅，奉上影券1张，请查收。券号：{0}（请在蜘蛛电影app或蜘蛛网官网兑换使用，全国通兑，2D和3D场次均可使用，具体使用规则以蜘蛛网官网为准）。";
            
            for(int x=0; x<3; x++)
            {
                var result = YunPianMessenger.SendSms(string.Format(format, coupons[x]), mobiles[x]);
                CFDGlobal.LogLine(result);
            }
        }

        [TestMethod]
        public void TestAccounts()
        {
            var db = CFDEntities.Create();
            var userService = new UserService(db);
            var users = db.Users.ToList();

            var id = 11144440001;
            for (int i = 0; i < 100; i++)
            {
                if (i < 28) continue;

                var phone = (id + i).ToString();

                //注册cfd账号
                //userService.CreateUserByPhone(phone);

                //注册ayondo账号
                var user = users.FirstOrDefault(o => o.Phone == phone);
                //var userController = new UserController(db, MapperConfig.GetAutoMapperConfiguration().CreateMapper(),
                //    CFDGlobal.BasicRedisClientManager.GetClient());
                //userController.CreateAyondoAccount(user);

                ////购买商品
                //var positionController = new PositionController(db, MapperConfig.GetAutoMapperConfiguration().CreateMapper(), CFDGlobal.BasicRedisClientManager.GetClient());
                //for (int j = 0; j < 100; j++)
                //{
                //    //positionController.ControllerContext.Request=new HttpRequestMessage(HttpMethod.Post,"");
                //    //positionController.ControllerContext.Request.Headers.Authorization = new AuthenticationHeaderValue("Basic", user.Id + "_" + user.Token);

                //    positionController.UserId = user.Id;
                //    positionController.NewPosition(new NewPositionFormDTO() {invest = 100, isLong = true, leverage = 20, securityId = 34804});
                //}
            }
        }

        [TestMethod]
        public void UtilTest()
        {
            var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();
            var redisProdDefClient = redisClient.As<ProdDef>();
            var p = redisProdDefClient.GetById("34824");
            var openPrice = Quotes.GetOpenPrice(p);
        }

        [TestMethod]
        public void UtilTest2()
        {
            Assert.AreEqual(2m, Maths.Ceiling(1.23456789m, 0));
            Assert.AreEqual(1.3m, Maths.Ceiling(1.23456789m, 1));
            Assert.AreEqual(1.24m, Maths.Ceiling(1.23456789m, 2));
            Assert.AreEqual(1.235m, Maths.Ceiling(1.23456789m, 3));
            Assert.AreEqual(1.2346m, Maths.Ceiling(1.23456789m, 4));

            Assert.AreEqual(1m, Maths.Floor(1.23456789m, 0));
            Assert.AreEqual(1.2m, Maths.Floor(1.23456789m, 1));
            Assert.AreEqual(1.23m, Maths.Floor(1.23456789m, 2));
            Assert.AreEqual(1.234m, Maths.Floor(1.23456789m, 3));
            Assert.AreEqual(1.2345m, Maths.Floor(1.23456789m, 4));
        }

        [TestMethod]
        public void CreateAyondoAccount()
        {
            //var db = CFDEntities.Create();
            //var ivan = db.Users.FirstOrDefault(o => o.Id == 1);
            //var userController = new UserController(db, MapperConfig.GetAutoMapperConfiguration().CreateMapper(), CFDGlobal.BasicRedisClientManager.GetClient());
            //userController.CreateAyondoDemoAccount(ivan);
        }

        [TestMethod]
        public void WCFConnectionLimitTest()
        {
            ServicePointManager.DefaultConnectionLimit = 10000;

            CFDGlobal.LogLine("begin");

            IList<Thread> threads = new List<Thread>();

            for (int i = 0; i < 50; i++)
            {
                //var threadStart = new ParameterizedThreadStart(DoUserOperation);
                var threadStart = new ParameterizedThreadStart(ThreadTest);

                //var threadStart = new ThreadStart(DoUserOperation);
                var thread = new Thread(threadStart);
                thread.Start();
                //thread.Start();
                threads.Add(thread);

                //var task = Task.Run(() => { DoUserOperation(user); });
                //tasks.Add(task);
            }


            while (threads.Any(o => o.IsAlive))
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            CFDGlobal.LogLine("end");
        }

        private void ThreadTest(object obj)
        {
            CFDGlobal.LogLine("start "+Thread.CurrentThread.ManagedThreadId);

            //test web role
            //var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/misc/sleep");
            //var webResponse = request.GetResponse();

            //test local threadpool
            //Thread.Sleep(5000);

            //test wcf only
            //var ayondoTradeClient = new AyondoTradeClient();
            //ayondoTradeClient.TestSleep(TimeSpan.FromSeconds(5));

            //test web role via wcf
            var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/misc/wcf");
            var webResponse = request.GetResponse();

            CFDGlobal.LogLine("end " + Thread.CurrentThread.ManagedThreadId);
        }

        [TestMethod]
        public void DianYingPiao()
        {
            var db = CFDEntities.Create();

            User user;
            PositionDTO pos;

            var r = new Random();

            //德国30 34820
            user = db.Users.FirstOrDefault(o => o.Id == 3219);
            pos = XiaDan_SheZhiYing(user, 34820, r.NextDouble() > 0.5, 100);

            //法国40 34811 50
            //英镑/加元 34815 125
            user = db.Users.FirstOrDefault(o => o.Id == 3220);
            pos = XiaDan_SheZhiYing(user, 34815, r.NextDouble() > 0.5, 125);

            //华尔街 34864
            user = db.Users.FirstOrDefault(o => o.Id == 3281);
            pos = XiaDan_SheZhiYing(user, 34864, r.NextDouble() > 0.5, 100);
            //美国标准500 34857
            user = db.Users.FirstOrDefault(o => o.Id == 3218);
            pos = XiaDan_SheZhiYing(user, 34857, r.NextDouble() > 0.5, 100);

            //英国100 34854
            user = db.Users.FirstOrDefault(o => o.Id == 3221);
            pos = XiaDan_SheZhiYing(user, 34854, r.NextDouble() > 0.5, 100);
            //美国科技股100 34858
            user = db.Users.FirstOrDefault(o => o.Id == 3222);
            pos = XiaDan_SheZhiYing(user, 34858, r.NextDouble() > 0.5, 100);

            //欧元对英镑 34803
            user = db.Users.FirstOrDefault(o => o.Id == 1);
            pos = XiaDan_SheZhiYing(user, 34803, r.NextDouble() > 0.5, 125);

            //欧洲50 34801 50
            //美元/日元 34860 100
            user = db.Users.FirstOrDefault(o => o.Id == 3277);
            pos = XiaDan_SheZhiYing(user, 34860, r.NextDouble() > 0.5, 100);
        }

        private static PositionDTO SheZhiYing(User user, PositionDTO pos)
        {
            var takePx = pos.isLong ? pos.settlePrice * 1.0115m : pos.settlePrice * 0.9885m;

            string jsonData = "{\"posId\":" + pos.id + ",\"securityId\":" + pos.security.id + ",\"price\":"+ takePx+"}";
            var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/order/take");
            request.Headers["Authorization"] = string.Format("Basic {0}_{1}", user.Id, user.Token);
            request.Method = "post";
            request.ContentType = "application/json";
            byte[] datas = Encoding.UTF8.GetBytes(jsonData);
            request.ContentLength = datas.Length;
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(datas, 0, datas.Length);

            WebResponse response;
            try
            {
                response = request.GetResponse();
            }
            catch (WebException e)
            {
                response = e.Response;
                CFDGlobal.LogException(e);
            }
            var responseStream = response.GetResponseStream();
            var readToEnd = new StreamReader(responseStream).ReadToEnd();
            var dto = JsonConvert.DeserializeObject<PositionDTO>(readToEnd);
            return dto;
        }

        private static PositionDTO XiaDan_SheZhiYing(User user,int secId,bool isLong,int leverage)
        {
            string jsonData = "{\"securityId\":" + secId + ",\"isLong\":"+ isLong.ToString().ToLower()+",\"invest\":100,\"leverage\":"+leverage+"}";
            //var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position?ignorePriceDelay=true");
            var request = HttpWebRequest.Create("http://300f8c59436243fe920fce09eb87d765.chinacloudapp.cn/api/position?ignorePriceDelay=true");
            //var request = HttpWebRequest.Create("http://localhost:11033/api/position?ignorePriceDelay=true");
            request.Headers["Authorization"] = string.Format("Basic {0}_{1}", user.Id, user.Token);
            request.Method = "post";
            request.ContentType = "application/json";
            byte[] datas = Encoding.UTF8.GetBytes(jsonData);
            request.ContentLength = datas.Length;
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(datas, 0, datas.Length);

            WebResponse response;
            bool isFaulted = false;
            try
            {
                response = request.GetResponse();
            }
            catch (WebException e)
            {
                response = e.Response;
                CFDGlobal.LogException(e);
                isFaulted = true;
            }
            var responseStream = response.GetResponseStream();
            var readToEnd = new StreamReader(responseStream).ReadToEnd();

            if (isFaulted)
            {
                CFDGlobal.LogLine(readToEnd);
                throw new Exception(readToEnd);
            }

            var dto = JsonConvert.DeserializeObject<PositionDTO>(readToEnd);

            var dto2 = SheZhiYing(user, dto);

            return dto2;
        }

        [TestMethod]
        public void PingTest()
        {
            var times = new List<double>();
            for (int i = 0; i < 4; i++)
            {
                var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Blocking = true;

                var stopwatch = new Stopwatch();

                // Measure the Connect call only
                stopwatch.Start();
                sock.Connect("cfd-stunnel-cn2.cloudapp.net",14999);
                stopwatch.Stop();

                double t = stopwatch.Elapsed.TotalMilliseconds;
                Console.WriteLine("{0:0.00}ms", t);
                times.Add(t);

                sock.Close();

                Thread.Sleep(1000);
            }
            Console.WriteLine("{0:0.00} {1:0.00} {2:0.00}", times.Min(), times.Max(), times.Average());


            //System.Net.NetworkInformation.Ping p = new System.Net.NetworkInformation.Ping();
            //System.Net.NetworkInformation.PingOptions options = new System.Net.NetworkInformation.PingOptions();
            //options.DontFragment = true;
            //string data = "Test Data!";
            //byte[] buffer = Encoding.ASCII.GetBytes(data);
            //int timeout = 1000; // Timeout 时间，单位：毫秒  
            //System.Net.NetworkInformation.PingReply reply = p.Send("cfd-stunnel-cn2.cloudapp.net:14999", timeout, buffer, options);
            //var success = (reply.Status == System.Net.NetworkInformation.IPStatus.Success);
        }

        [TestMethod]
        public void PinYin()
        {
            var format = new HanyuPinyinOutputFormat();
            format.ToneType = HanyuPinyinToneType.WITHOUT_TONE;
            format.CaseType = HanyuPinyinCaseType.LOWERCASE;
            format.VCharType = HanyuPinyinVCharType.WITH_V;

            using (var db = CFDEntities.Create())
            {
                var data = db.UserInfos.Where(o=>o.LastName!=null && o.FirstName!=null).Select(o=>new {Last= o.LastName, First=o.FirstName}).ToList();

                foreach (var o in data)
                {
                    var lastPinyin = o.Last.ToCharArray().Select(c => PinyinHelper.ToHanyuPinyinStringArray(c, format))
                        .Where(arr => arr != null && arr.Length > 0)
                        .Select(arr => arr[0]).Aggregate((p, n) => p + n);
                    var firstPinyin = o.First.ToCharArray().Select(c => PinyinHelper.ToHanyuPinyinStringArray(c, format))
                        .Where(arr => arr != null && arr.Length > 0)
                        .Select(arr => arr[0]).Aggregate((p, n) => p + n);

                    CFDGlobal.LogLine(o.Last+" "+o.First+" "+lastPinyin+" "+firstPinyin);
                }
            }
        }
    }
}