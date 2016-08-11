using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;

namespace CFD_JOBS.Ayondo
{
    public class RedisToDbWorker
    {
        public static void Run()
        {
            var db = CFDEntities.Create();
            //var basicClientManager = CFDGlobal.GetNewBasicRedisClientManager();
            var redisClient = CFDGlobal.BasicRedisClientManager.GetClient();
            var redisProdDefClient = redisClient.As<ProdDef>();
            var redisQuoteClient = redisClient.As<Quote>();

            while (true)
            {
                var prodDefs = redisProdDefClient.GetAll();
                var quotes = redisQuoteClient.GetAll();
                List<AyondoSecurity> securities;

                //save proddefs
                CFDGlobal.LogLine("Saving new product definitions to db...");
                securities = db.AyondoSecurities.ToList();
                foreach (var prodDef in prodDefs)
                {
                    var sec = securities.FirstOrDefault(o => o.Id == prodDef.Id);

                    if (sec == null)
                    {
                        CFDGlobal.LogLine("Creating new: " + prodDef.Id + " " + prodDef.Symbol + " " + prodDef.Name);
                        db.AyondoSecurities.Add(new AyondoSecurity
                        {
                            //basic
                            Id = prodDef.Id,
                            //Name = prodDef.Name,
                            //Symbol = prodDef.Symbol,
                            //AssetClass = prodDef.AssetClass,
                            ////trade
                            //Shortable = prodDef.Shortable,
                            //MaxLeverage = prodDef.MaxLeverage,
                            //MaxSizeShort = prodDef.MaxSizeShort,
                            //MinSizeShort = prodDef.MinSizeShort,
                            //MaxSizeLong = prodDef.MaxSizeLong,
                            //MinSizeLong = prodDef.MinSizeLong,
                            ////update time
                            //DefUpdatedAt = prodDef.Time,
                        });
                    }
                    else
                    {
                        ////basic
                        //sec.Name = prodDef.Name;
                        //sec.Symbol = prodDef.Symbol;
                        //sec.AssetClass = prodDef.AssetClass;
                        ////trade
                        //sec.Shortable = prodDef.Shortable;
                        //sec.MaxLeverage = prodDef.MaxLeverage;
                        //sec.MaxSizeShort = prodDef.MaxSizeShort;
                        //sec.MinSizeShort = prodDef.MinSizeShort;
                        //sec.MaxSizeLong = prodDef.MaxSizeLong;
                        //sec.MinSizeLong = prodDef.MinSizeLong;
                        ////update time
                        //sec.DefUpdatedAt = prodDef.Time;
                    }
                }
                db.SaveChanges();

                //save quotes
                CFDGlobal.LogLine("Saving new quotes to db...");
                securities = db.AyondoSecurities.ToList();
                foreach (var quote in quotes)
                {
                    var sec = securities.FirstOrDefault(o => o.Id == quote.Id);
                    if (sec != null)
                    {
                        //sec.Bid = quote.Bid;
                        //sec.Ask = quote.Offer;
                        //sec.QuoteUpdatedAt = quote.Time;
                    }
                }
                db.SaveChanges();

                Thread.Sleep(TimeSpan.FromMinutes(5));
            }
        }
    }
}