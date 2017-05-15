using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using EntityFramework.BulkInsert.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CFD_JOBS
{
    /// <summary>
    /// 记录下每天12:00 AM时的产品价格，以便计算用户持仓收益
    /// </summary>
    class QuoteSnapshot
    {
        private static DateTime _lastCalculatedDate = DateTime.MinValue;

        public static void Run(bool isLive)
        {
            if(isLive)
                throw new NotImplementedException();

            while (true)
            {
                var chinaNow = DateTimes.GetChinaNow();
                var chinaToday = chinaNow.Date;

                if (chinaNow.Hour == 0 
                    && chinaToday > _lastCalculatedDate) 
                {
                    try
                    {
                        using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(isLive).GetClient())
                        {
                            using (var db = CFDEntities.Create())
                            {
                                var redisQuoteClient = redisClient.As<Quote>();
                                List<Quote> quotes = redisQuoteClient.GetAll().ToList();
                                List<CFD_COMMON.Models.Entities.QuoteSnapshot> quoteSnapshots = new List<CFD_COMMON.Models.Entities.QuoteSnapshot>();
                                quotes.ForEach(q =>
                                {
                                    quoteSnapshots.Add(new CFD_COMMON.Models.Entities.QuoteSnapshot()
                                    {
                                        SecurityId = q.Id,
                                        Date = chinaToday.AddDays(-1),
                                        QuoteTime = q.Time,
                                        Ask = q.Offer,
                                        Bid = q.Bid
                                    });
                                });

                                //Bulk insert时字段名称要和数据库完全一致，包括大小写
                                //不用Bulk insert时，大小写不影响。
                                db.BulkInsert(quoteSnapshots);
                                db.SaveChanges();
                            }
                        }

                        _lastCalculatedDate = chinaToday;
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogException(e);
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));

            }
        }
    }
}
