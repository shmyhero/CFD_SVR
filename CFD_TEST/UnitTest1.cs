using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
using System.Text;
using CFD_API.DTO;
using CFD_JOBS;
using Newtonsoft.Json.Linq;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void Test1()
        {
            using (var db = CFDEntities.Create())
            {
                CFD_COMMON.Models.Entities.Message msg = new CFD_COMMON.Models.Entities.Message();
                msg.Title = "测试";
                msg.Body = "测试内容";
                msg.CreatedAt = DateTime.UtcNow;
                msg.IsReaded = false;

                db.Messages.Add(msg);
                db.SaveChanges();
                int id = msg.Id;
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
            list.Add(new KeyValuePair<string, string>("f60c5d5a898b1c19cb6e5d58520c8906", "{\"type\":\"1\", \"title\":\"盈交易\", \"StockID\":34847, \"CName\":\"白银\", \"message\":\"cfd://page/1\"}"));
            //list.Add(new KeyValuePair<string, string>("6a67a54402ca0a4c755ebda7b754ab32", "{\"type\":\"1\", \"title\":\"盈交易\", \"StockID\":34847, \"CName\":\"白银\", \"message\":\"白银于2016/09/06 10:19平仓，价格为200.00美元,已亏损100美元\"}"));
            //list.Add(new KeyValuePair<string, string>("749f7136cf13c8669ef97222557ba982", "{\"type\":\"1\", \"title\":\"盈交易\", \"StockID\":34847, \"CName\":\"白银\", \"message\":\"白银于2016/09/06 10:19平仓，价格为200.00美元,已亏损100美元\"}"));

            var push = new GeTui();
            var response = push.PushBatch(list);
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
            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");

            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient();
            //using (OperationContextScope scope = new OperationContextScope(clientHttp.InnerChannel))
            //{
            //    MessageHeader myHeader = MessageHeader.CreateHeader(
            //        "token", "TH", Guid.NewGuid().ToString());
            //    OperationContext.Current.OutgoingMessageHeaders.Add(myHeader);

            //    var positionReport = clientHttp.Test("jiangyi1985");
            //}

            var positionReport = clientHttp.Test("jiangyi1985");

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
            var sendSms = YunPianMessenger.SendSms("【盈交易】陛下，您在盈交易平台“比收益”活动中名列前茅，奉上影券1张，请查收。"+
                "券号：G1608150944636360 密码：83WN85A6E861" +
                "（请在格瓦拉生活网兑换使用，全国通兑，2D和3D场次均可使用，具体使用规则以格瓦拉平台为准）。", "18516539018");
            CFDGlobal.LogLine(sendSms);
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
            var db = CFDEntities.Create();
            var ivan = db.Users.FirstOrDefault(o => o.Id == 1);
            var userController = new UserController(db, MapperConfig.GetAutoMapperConfiguration().CreateMapper(), CFDGlobal.BasicRedisClientManager.GetClient());
            userController.CreateAyondoAccount(ivan);
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

            //德国30
            //for (int i = 0; i < 3; i++)
            //{
            user = db.Users.FirstOrDefault(o => o.Id == 1);
                pos = XiaDan_SheZhiYing(user, 34820, true, 100);
                user = db.Users.FirstOrDefault(o => o.Id == 3277);
                pos = XiaDan_SheZhiYing(user, 34820, false, 100);
            //}

            //华尔街
            user = db.Users.FirstOrDefault(o => o.Id == 3219);
            pos = XiaDan_SheZhiYing(user, 34864, true, 100);
            user = db.Users.FirstOrDefault(o => o.Id == 3220);
            pos = XiaDan_SheZhiYing(user, 34864, false, 100);

            ////华尔街
            //for (int i = 0; i < 1; i++)
            //{
            //    user = db.Users.FirstOrDefault(o => o.Id == 3281);
            //    pos = XiaDan_SheZhiYing(user, 34864, true);
            //    user = db.Users.FirstOrDefault(o => o.Id == 3218);
            //    pos = XiaDan_SheZhiYing(user, 34864, false);
            //}

            //欧元对英镑
            for (int i = 0; i < 1; i++)
            {
                user = db.Users.FirstOrDefault(o => o.Id == 3281);
                pos = XiaDan_SheZhiYing(user, 34803, true, 125);
                user = db.Users.FirstOrDefault(o => o.Id == 3218);
                pos = XiaDan_SheZhiYing(user, 34803, false, 125);
            }
        }

        private static PositionDTO SheZhiYing(User user, PositionDTO pos)
        {
            var takePx = pos.isLong ? pos.settlePrice * 1.008m : pos.settlePrice * 0.992m;

            string jsonData = "{\"posId\":" + pos.id + ",\"securityId\":" + pos.security.id + ",\"price\":"+ takePx+"}";
            var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position/order/take");
            request.Headers["Authorization"] = string.Format("Basic {0}_{1}", user.Id, user.Token);
            request.Method = "post";
            request.ContentType = "application/json";
            byte[] datas = Encoding.UTF8.GetBytes(jsonData);
            request.ContentLength = datas.Length;
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(datas, 0, datas.Length);

            var responseStream = request.GetResponse().GetResponseStream();
            var readToEnd = new StreamReader(responseStream).ReadToEnd();
            var dto = JsonConvert.DeserializeObject<PositionDTO>(readToEnd);
            return dto;
        }

        private static PositionDTO XiaDan_SheZhiYing(User user,int secId,bool isLong,int leverage)
        {
            string jsonData = "{\"securityId\":" + secId + ",\"isLong\":"+ isLong.ToString().ToLower()+",\"invest\":100,\"leverage\":"+leverage+"}";
            var request = HttpWebRequest.Create("http://cfd-webapi.chinacloudapp.cn/api/position");
            request.Headers["Authorization"] = string.Format("Basic {0}_{1}", user.Id, user.Token);
            request.Method = "post";
            request.ContentType = "application/json";
            byte[] datas = Encoding.UTF8.GetBytes(jsonData);
            request.ContentLength = datas.Length;
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(datas, 0, datas.Length);

            var responseStream = request.GetResponse().GetResponseStream();
            var readToEnd = new StreamReader(responseStream).ReadToEnd();
            var dto = JsonConvert.DeserializeObject<PositionDTO>(readToEnd);

            var dto2 = SheZhiYing(user, dto);

            return dto2;
        }
    }
}