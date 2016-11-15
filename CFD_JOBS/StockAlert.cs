using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoMapper;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
using CFD_COMMON.Utils.Extensions;
using CFD_COMMON.Models.Entities;

namespace CFD_JOBS.Ayondo
{
    public class StockAlert
    {
        private static readonly TimeSpan _sleepInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan _tolerance = TimeSpan.FromMinutes(5);

        private static Dictionary<int,DateTime> _lastFetchTill=new Dictionary<int, DateTime>(); 

        private static string PUSH_TEMP =
            @"{{""id"":{3},  ""type"":""2"", ""title"":""盈交易"", ""StockID"":{0}, ""CName"":""{1}"", ""message"":""{2}""}}"; //{{ as {

        public static void Run(bool isLive = false)
        {
            CFDGlobal.LogLine("Starting...");

            var mapper = MapperConfig.GetAutoMapperConfiguration().CreateMapper();

            while (true)
            {
                try
                {
                    using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(isLive).GetClient())
                    {
                        var redisProdDefClient = redisClient.As<ProdDef>();
                        var redisQuoteClient = redisClient.As<Quote>();
                        var redisTickClient = redisClient.As<Tick>();

                        var prodDefs = redisProdDefClient.GetAll();
                        var quotes = redisQuoteClient.GetAll();

                        using (var db = CFDEntities.Create())
                        {
                            var userAlerts = isLive
                                ? db.UserAlert_Live.Where(o => o.HighEnabled.Value || o.LowEnabled.Value).ToList().Select(o => o as UserAlertBase).ToList()
                                : db.UserAlerts.Where(o => o.HighEnabled.Value || o.LowEnabled.Value).ToList().Select(o => o as UserAlertBase).ToList();

                            CFDGlobal.LogLine("Got " + userAlerts.Count + " alerts.");

                            var groups = userAlerts.GroupBy(o => o.SecurityId).ToList();

                            var newAlertList = new List<KeyValuePair<int, string>>();

                            foreach (var group in groups) //foreach security
                            {
                                var secId = group.Key;

                                //CFDGlobal.LogLine("sec: " + secId + " alert_count: " + group.Count());

                                var prodDef = prodDefs.FirstOrDefault(o => o.Id == secId);
                                var quote = quotes.FirstOrDefault(o => o.Id == secId);

                                if (prodDef == null || quote == null)
                                {
                                    CFDGlobal.LogLine("cannot find prodDef/quote " + secId);
                                    continue;
                                }

                                if (prodDef.QuoteType == enmQuoteType.Closed ||
                                    prodDef.QuoteType == enmQuoteType.Inactive)
                                {
                                    CFDGlobal.LogLine("prod " + prodDef.Id + " quoteType is " + prodDef.QuoteType);
                                    continue;
                                }

                                if (DateTime.UtcNow - quote.Time > _tolerance)
                                {
                                    CFDGlobal.LogLine("quote " + quote.Id + " too old " + quote.Time);
                                    continue;
                                }

                                ////get historical highest and lowest price
                                //decimal highestBid;
                                //decimal lowestAsk;

                                //var ticks = redisTickClient.Lists[Ticks.GetTickListNamePrefix(TickSize.Raw) + prodDef.Id].GetAll();
                                //var dtUtcNow = DateTime.UtcNow;
                                //if (!_lastFetchTill.ContainsKey(prodDef.Id))
                                //{s
                                //    var historyTicks = ticks.Select(o => o.Time > dtUtcNow - _tolerance).ToList();

                                //}

                                var messages = new List<MessageBase>();

                                foreach (var alert in group) //foreach alert belong to a security
                                {
                                    if (alert.HighEnabled.Value && quote.Bid >= alert.HighPrice)
                                    {
                                        var text =
                                            $"{Translator.GetCName(prodDef.Name)}于{quote.Time.AddHours(8).ToString("HH:mm")}价格达到{quote.Bid}，高于您设置的{Math.Round(alert.HighPrice.Value, prodDef.Prec, MidpointRounding.AwayFromZero)}";

                                        alert.HighEnabled = false;
                                        alert.HighPrice = null;

                                        var msg = new MessageBase()
                                        {
                                            UserId = alert.UserId,
                                            Title = "价格消息",
                                            Body = text,
                                            IsReaded = false,
                                            CreatedAt = DateTime.UtcNow
                                        };
                                        
                                        messages.Add(isLive 
                                            ? (MessageBase)mapper.Map<Message_Live>(msg) 
                                            : (MessageBase)mapper.Map<Message>(msg));

                                        //db.Messages.Add(msg);
                                        //db.SaveChanges();
                                        //int msgId = msg.Id;

                                        //newAlertList.Add(new KeyValuePair<int, string>(alert.UserId,
                                        //    string.Format(PUSH_TEMP, prodDef.Id, Translator.GetCName(prodDef.Name), text, msgId)));
                                    }

                                    if (alert.LowEnabled.Value && quote.Offer <= alert.LowPrice)
                                    {
                                        var text =
                                            $"{Translator.GetCName(prodDef.Name)}于{quote.Time.AddHours(8).ToString("HH:mm")}价格跌到{quote.Offer}，低于您设置的{Math.Round(alert.LowPrice.Value, prodDef.Prec, MidpointRounding.AwayFromZero)}";

                                        alert.LowEnabled = false;
                                        alert.LowPrice = null;

                                        var msg = new MessageBase()
                                        {
                                            UserId = alert.UserId,
                                            Title = "价格消息",
                                            Body = text,
                                            IsReaded = false,
                                            CreatedAt = DateTime.UtcNow
                                        };

                                        messages.Add(isLive
                                            ? (MessageBase)mapper.Map<Message_Live>(msg)
                                            : (MessageBase)mapper.Map<Message>(msg));

                                        //db.Messages.Add(msg);
                                        //db.SaveChanges();
                                        //int msgId = msg.Id;

                                        //newAlertList.Add(new KeyValuePair<int, string>(alert.UserId,
                                        //    string.Format(PUSH_TEMP, prodDef.Id, Translator.GetCName(prodDef.Name), text, msgId)));
                                    }
                                }

                                //db.SaveChanges();
                                
                                if (messages.Count > 0)
                                {
                                    if (isLive)
                                        db.Message_Live.AddRange(messages.Select(o => o as Message_Live));
                                    else
                                        db.Messages.AddRange(messages.Select(o => o as Message));

                                    //save to UserAlert: disable triggered alert
                                    //save to Message: save messages
                                    db.SaveChanges();

                                    //Got message auto ID after saving to DB
                                    foreach (var message in messages)
                                    {
                                        newAlertList.Add(new KeyValuePair<int, string>(message.UserId,
                                            string.Format(PUSH_TEMP, prodDef.Id, Translator.GetCName(prodDef.Name), message.Body, message.Id)));//using message id here
                                    }
                                }
                            }

                            if (newAlertList.Count > 0)
                            {
                                ////disable triggered alert
                                //db.SaveChanges();

                                CFDGlobal.LogLine(newAlertList.Count + " alerts to send...");

                                CFDGlobal.LogLine("pushing to GeTui...");
                                var geTuiList = new List<KeyValuePair<string, string>>();

                                var userIds = newAlertList.Select(o => o.Key).Distinct().ToList();
                                var devices =
                                    db.Devices.Where(o => o.userId.HasValue && userIds.Contains(o.userId.Value))
                                        .ToList();

                                foreach (var pair in newAlertList)
                                {
                                    var userDevices = devices.Where(o => o.userId == pair.Key).ToList();

                                    foreach (var userDevice in userDevices)
                                    {
                                        geTuiList.Add(new KeyValuePair<string, string>(userDevice.deviceToken,
                                            pair.Value));
                                    }
                                }

                                if (geTuiList.Count > 0)
                                {
                                    var push = new GeTui();
                                    var chuncks = geTuiList.SplitInChunks(1000);
                                    foreach (var chunck in chuncks)
                                    {
                                        var pushBatch = push.PushBatch(chunck);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                CFDGlobal.LogLine("");
                Thread.Sleep(_sleepInterval);
            }
        }
    }
}