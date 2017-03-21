using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using EntityFramework.BulkInsert.Extensions;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CFD_JOBS
{
    /// <summary>
    /// 每天12：00计算实盘用户的等级
    /// 
    ///  财富起航 最短交易天数：5天 最低交易量：2       累计交易额：10000       投资回报率：1%        近2周交易活跃度：1 
    ///  点亮勋章 最短交易天数：5天 最低交易量：5       累计交易额：30000       投资回报率：1.5%      近2周交易活跃度：1 
    ///  超越平凡 最短交易天数：5天 最低交易量：10      累计交易额：60000       投资回报率：3.0%      近2周交易活跃度：1  	
    ///  卓尔不群 最短交易天数：5天 最低交易量：15      累计交易额：90000       投资回报率：5.0%      近2周交易活跃度：1 
    ///  我是王者 最短交易天数：5天 最低交易量：30      累计交易额：120000      投资回报率：7.0%      近2周交易活跃度：1 
    /// </summary>
    class RankJob
    {
        private static DateTime _lastCalculatedDate = DateTime.MinValue;

        public static void Run(bool isLive)
        {
            while (true)
            {
                var chinaNow = DateTimes.GetChinaNow();
                var chinaToday = chinaNow.Date;

                if (chinaNow.Hour == 0
                    && chinaToday > _lastCalculatedDate)
                {
                    try
                    {
                        using (var db = CFDEntities.Create())
                        {
                            //先找出所有符合：最短交易天数：5天 、近2周交易活跃度：1 、投资回报率超过1% 的用户
                            /* 参考SQL如下
                            select userid, count(closedat) as TradeCount, sum(investusd * leverage) as TradeAmount, sum(PL) as pl, sum(investusd) as invest, (SUM(PL) / SUM(InvestUSD)) as Rate from cfd.dbo.NewPositionHistory_live
                            group by userid
                            having (max(createtime) > dateadd(DAY, -14, getdate()) or max(closedat) > dateadd(DAY, -14, getdate()))
                            and  sum(DATEDIFF(hh,createtime, closedat)) > 5 * 24 and (SUM(PL) / SUM(InvestUSD)) > 0.03
                            order by userid desc
                             */

                            List<Tuple<int, int, decimal, decimal, decimal, decimal>> rankedUsers = new List<Tuple<int, int, decimal, decimal, decimal, decimal>>();
                            //Linq to SQL写不出这段SQL，还是用SqlCommand实现简单一些
                            using (SqlCommand cmd = new SqlCommand())
                            {
                                cmd.Connection = new SqlConnection(ConfigurationManager.ConnectionStrings["CFDEntities"].ConnectionString);
                                cmd.CommandText = "select userid, count(closedat) as TradeCount, sum(investusd * leverage) as TradeAmount, sum(PL) as pl, sum(investusd) as invest, (SUM(PL) / SUM(InvestUSD)) as Rate from cfd.dbo.NewPositionHistory_live group by userid having(max(createtime) > dateadd(DAY, -14, getdate()) or max(closedat) > dateadd(DAY, -14, getdate())) and sum(DATEDIFF(hh, createtime, closedat)) > 5 * 24 and(SUM(PL) / SUM(InvestUSD)) > 0.03  order by userid desc";
                                try
                                {
                                    cmd.Connection.Open();
                                    var reader = cmd.ExecuteReader();
                                    while (reader.Read())
                                    {
                                        rankedUsers.Add(new Tuple<int, int, decimal, decimal, decimal, decimal>( (int)reader["userid"], (int)reader["TradeCount"], decimal.Parse(reader["TradeAmount"].ToString()), decimal.Parse(reader["pl"].ToString()), decimal.Parse(reader["invest"].ToString()), decimal.Parse(reader["Rate"].ToString())));
                                    }
                                }
                                catch(Exception ex)
                                {
                                    CFDGlobal.LogException(ex);
                                    continue;
                                }
                            }

                                //var twoWeekAgo = DateTime.Now.AddDays(-14);

                                //var rankedUsers = (from h in db.NewPositionHistories
                                //                 group h by h.UserId into h1
                                //                 let maxCreateTime = h1.Max(nph => nph.CreateTime)
                                //                 let maxCloseTime = h1.Max(nph => nph.ClosedAt)
                                //                 //let sumTradeDays = h1.Sum< NewPositionHistory >(nph => nph.ClosedAt.HasValue? (nph.ClosedAt.Value - nph.CreateTime.Value).TotalHours : 0)
                                //                 let sumPL = h1.Sum<NewPositionHistory>(nph => nph.PL.HasValue? nph.PL.Value : 0)
                                //                 let sumInvestUSD = h1.Sum<NewPositionHistory>(nph => nph.InvestUSD.HasValue ? nph.InvestUSD.Value : 0)
                                //                 let TradeCount = h1.Count<NewPositionHistory>()
                                //                 let sumTradeAmount = h1.Sum<NewPositionHistory>(nph => nph.InvestUSD.Value * nph.Leverage.Value)
                                //                 where
                                //                   (maxCreateTime > twoWeekAgo 
                                //                   || maxCloseTime > twoWeekAgo)
                                //                    //&&sumTradeDays > 5 * 24
                                //                   && (sumPL / sumInvestUSD) > 0.01M
                                //                   && TradeCount >= 2
                                //                   && sumTradeAmount >= 10000
                                //                   select new {
                                //                     UserID = h1.Key,
                                //                     //TradeCount = h1.Count<NewPositionHistory>(),
                                //                     //TradeAmount = h1.Sum<NewPositionHistory>(nph => nph.InvestUSD * nph.Leverage),
                                //                     ////PL = h1.Sum<NewPositionHistory>(nph => nph.PL),
                                //                     //InvestUSD = h1.Sum<NewPositionHistory>(nph => nph.InvestUSD),
                                //                 }).ToList();
                                
                                db.Users.Where(u => u.AyLiveAccountId.HasValue).ToList().ForEach(
                                        user =>
                                        {
                                            var rankedUser = rankedUsers.FirstOrDefault(ru => ru.Item1 == user.Id);
                                            if (rankedUser != null)
                                            {
                                                int rank = GetRank(rankedUser.Item2, rankedUser.Item3, rankedUser.Item6);
                                                user.LiveRank = rank;
                                            }
                                        }
                                    );

                            db.SaveChanges();
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

        private static int GetRank(int tradeCount, decimal tradeAmount, decimal rate)
        {
            if (tradeCount >= 30 && tradeAmount >= 500000  && rate >= 0.15M)
            {
                return (int)RankEnum.我是王者;
            }
            else if (tradeCount >= 15 && tradeAmount >= 300000 && rate >= 0.12M)
            {
                return (int)RankEnum.卓尔不群;
            }
            else if (tradeCount >= 10 && tradeAmount >= 200000 && rate >= 0.09M)
            {
                return (int)RankEnum.超越平凡;
            }
            else if (tradeCount >= 5 && tradeAmount >= 100000 && rate >= 0.06M)
            {
                return (int)RankEnum.点亮勋章;
            }
            else if (tradeCount >= 2 && tradeAmount >= 50000 && rate >= 0.03M)
            {
                return (int)RankEnum.财富起航;
            }

            return 0;
        }

    }

    enum RankEnum
    {
        财富起航 = 5,
        点亮勋章 = 4,
        超越平凡 = 3,
        卓尔不群 =2,
        我是王者 = 1
    }
}
