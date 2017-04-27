using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using Newtonsoft.Json.Linq;

namespace CFD_COMMON.Service
{
    public class RewardService
    {
        //continuous daily check-in reward (Day 1 - Day 5)
        public const decimal CHECK_IN_DAY_1_TO_5 = 0.5M;
        public const decimal CHECK_IN_DAY_6_TO_10 = 0.6M;
        public const decimal CHECK_IN_DAY_11_TO_X = 0.8M;
        public static readonly decimal REWARD_DEMO_REG = 50m;
        public const decimal REWARD_DEMO_TRADE = 0.5m;

        public CFDEntities db { get; set; }

        public RewardService(CFDEntities db)
        {
            this.db = db;
            int demoAmount = 50;
            try
            {
                var setting = db.Miscs.FirstOrDefault(m => m.Key == "RewardSetting");
                if (setting != null)
                {
                    demoAmount = JObject.Parse(setting.Value)["demoAccount"].Value<int>();
                }
            }
            catch (Exception ex)
            {
                CFDGlobal.LogInformation("模拟盘注册的交易金设置错误:" + ex.Message);
            }
        }

        public bool CheckIn(int userId)
        {
            bool result;

            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions {IsolationLevel = IsolationLevel.Serializable}))
            {
                using (var dbIsol = CFDEntities.Create())
                {
                    var chinaNow = DateTimes.GetChinaNow();
                    var chinaToday = chinaNow.Date;
                    var chinaYesterday = chinaToday.AddDays(-1);

                    var checkIns = dbIsol.DailySigns.Where(o => o.UserId == userId && (o.Date == chinaToday || o.Date == chinaYesterday)).ToList();

                    var newCheckIn = new DailySign
                    {
                        UserId = userId,
                        Date = chinaToday,
                        IsPaid = false,
                        SignAt = chinaNow,
                    };

                    if (checkIns.Count == 0) //no today, no yesterday
                    {
                        newCheckIn.Amount = CHECK_IN_DAY_1_TO_5;
                        newCheckIn.Continuity = 1;
                    }
                    else if (checkIns.Any(o => o.Date == chinaToday)) //today already checked
                    {
                        newCheckIn = null;
                    }
                    else //yesterday checked
                    {
                        var yesterdayCheckIn = checkIns.First(); //should be only 1 record in the list

                        newCheckIn.Continuity = yesterdayCheckIn.Continuity + 1;
                        newCheckIn.Amount = GetRewardAmount(newCheckIn.Continuity);
                    }

                    if (newCheckIn != null)
                    {
                        dbIsol.DailySigns.Add(newCheckIn);
                        dbIsol.SaveChanges();
                        result = true;
                    }
                    else
                        result = false;
                }

                scope.Complete();
            }

            return result;
        }

        private static object locker = new object();

        public void TradeReward(int userId)
        {
            Task.Factory.StartNew(() => {
                lock(locker)
                {
                    bool result;

                    using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.Serializable }))
                    {
                        using (var dbIsol = CFDEntities.Create())
                        {
                            var chinaNow = DateTimes.GetChinaNow();
                            var chinaToday = chinaNow.Date;

                            var any = dbIsol.DailyTransactions.Any(o => o.UserId == userId && o.Date == chinaToday);

                            if (!any)
                            {
                                var newTrade = new DailyTransaction()
                                {
                                    Date = chinaToday,
                                    Amount = REWARD_DEMO_TRADE,
                                    DealAt = chinaNow,
                                    UserId = userId,
                                    IsPaid = false,
                                };

                                dbIsol.DailyTransactions.Add(newTrade);
                                dbIsol.SaveChanges();

                                result = true;
                            }
                            else
                                result = false;
                        }

                        scope.Complete();
                    }

                    return result;
                }
            });
            
        }

        public static decimal GetRewardAmount(int continuity)
        {
            if (continuity <= 5)
                return CHECK_IN_DAY_1_TO_5;

            if (continuity <= 10)
                return CHECK_IN_DAY_6_TO_10;

            return CHECK_IN_DAY_11_TO_X;
        }
    }
}
