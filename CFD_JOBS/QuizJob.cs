using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using EntityFramework.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CFD_JOBS
{
    public class QuizJob
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        public static void Run()
        {
            while (true)
            {
                try
                {
                    var start = DateTime.UtcNow;
                    var end = DateTime.UtcNow.AddMinutes(5);

                    //北京时间上午6点(UTC 22点)统计前一天的竞猜结果
                    int timeToSendHour = 22;
                    try
                    {
                        using (var db = CFDEntities.Create())
                        {
                            timeToSendHour = int.Parse(db.Miscs.FirstOrDefault(o => o.Key == "QuizJob").Value);
                        }
                    }
                    catch
                    {
                        Console.WriteLine("读取发送时间失败");
                    }

                    var jobTime = DateTime.SpecifyKind(new DateTime(start.Year, start.Month, start.Day, timeToSendHour, 0, 0), DateTimeKind.Utc);
                    if (start < jobTime && end >= jobTime)
                    {
                        using (var db = CFDEntities.Create())
                        {
                            //找到当天交易日对应的竞猜活动
                            DateTime today = DateTime.Now.Date;
                            var quiz = db.Quizzes.FirstOrDefault(q => q.TradeDay.HasValue && q.TradeDay.Value == today);
                            
                            if (quiz != null)
                            {
                                Console.WriteLine("Quiz found with trade day:" + quiz.TradeDay);
                                using (var redisClient = CFDGlobal.GetDefaultPooledRedisClientsManager(true).GetClient())
                                {
                                    var redisKLineClient = redisClient.As<KLine>();
                                    //var redisProdDefClient = redisClient.As<ProdDef>();

                                    var klines = redisKLineClient.Lists[KLines.GetKLineListNamePrefix(KLineSize.Day) + quiz.ProdID];

                                    if (klines.Count == 0)
                                    {
                                        CFDGlobal.LogWarning(string.Format("Kline for Security ID:{0} is empty", quiz.ProdID));
                                    }

                                    //get 20 records at max
                                    //var result = klines.GetRange(beginIndex < 0 ? 0 : beginIndex, klines.Count - 1)
                                    //    .FirstOrDefault(k=>k.Time.Date == quiz.TradeDay.Value.Date);
                                    var result = klines.FirstOrDefault(k => k.Time.Date == quiz.TradeDay.Value.Date);
                                    quiz.SettledAt = DateTime.Now;
                                    if (result != null)
                                    {
                                        if(result.Close > result.Open) //涨
                                        {
                                            //如果有人买涨，就把本金+买跌人的钱平分给买涨的人
                                            //如果没人买涨，不做任何操作
                                            if (db.QuizBets.Any(qb => qb.QuizID == quiz.ID && qb.BetDirection == "long"))
                                            {
                                                decimal? shortAmount = db.QuizBets.Where(qb => qb.QuizID == quiz.ID && qb.BetDirection == "short").Sum(qb => qb.BetAmount);
                                                int longPersons = db.QuizBets.Where(qb => qb.QuizID == quiz.ID && qb.BetDirection == "long").Count();

                                                //把买跌的人的交易金平分给所有买涨的人
                                                db.QuizBets.Where(qb => qb.QuizID == quiz.ID && qb.BetDirection == "long")
                                                    .Update(qb => new QuizBet() { PL = qb.BetAmount + (shortAmount ?? 0 / longPersons), SettledAt = DateTime.Now });

                                                //买跌的人PL清零
                                                db.QuizBets.Where(qb => qb.QuizID == quiz.ID && qb.BetDirection == "short")
                                                    .Update(qb => new QuizBet() { PL = 0, SettledAt = DateTime.Now });
                                            }
                                           
                                            quiz.Result = "long";
                                        }
                                        else//跌
                                        {
                                            //如果有人买跌，就把本金+买涨人的钱平分给买跌的人
                                            //如果没人买跌，不做任何操作
                                            if (db.QuizBets.Any(qb => qb.QuizID == quiz.ID && qb.BetDirection == "short"))
                                            {
                                                decimal? longAmount = db.QuizBets.Where(qb => qb.QuizID == quiz.ID && qb.BetDirection == "long").Sum(qb => qb.BetAmount);
                                                int shortPersons = db.QuizBets.Where(qb => qb.QuizID == quiz.ID && qb.BetDirection == "short").Count();

                                                //把买跌的人的交易金平分给所有买涨的人
                                                db.QuizBets.Where(qb => qb.QuizID == quiz.ID && qb.BetDirection == "short")
                                                    .Update(qb => new QuizBet() { PL = qb.BetAmount + (longAmount ?? 0 / shortPersons), SettledAt = DateTime.Now });

                                                //买涨的人PL清零
                                                db.QuizBets.Where(qb => qb.QuizID == quiz.ID && qb.BetDirection == "long")
                                                    .Update(qb => new QuizBet() { PL = 0, SettledAt = DateTime.Now });
                                            }

                                            quiz.Result = "short";
                                        }
                                    }

                                    db.SaveChanges();
                                }
                            }
                            else
                            {
                                Console.WriteLine("no quiz found");
                            }
                        }
                    }

                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

                Thread.Sleep(Interval);
            }
        }
    }
}
