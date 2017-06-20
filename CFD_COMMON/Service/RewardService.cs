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
using AutoMapper;

namespace CFD_COMMON.Service
{
    public class RewardService
    {
        //continuous daily check-in reward (Day 1 - Day 5)
        public const decimal CHECK_IN_DAY_1_TO_5 = 0.5M;
        public const decimal CHECK_IN_DAY_6_TO_10 = 0.6M;
        public const decimal CHECK_IN_DAY_11_TO_X = 0.8M;
        /// <summary>
        /// 模拟账号手机注册交易金
        /// </summary>
        public static decimal REWARD_DEMO_PhoneREG = 30m;

        public static decimal REWARD_DEMO_WeChatREG = 5m;

        /// <summary>
        /// 实盘账号注册交易金
        /// </summary>
        public static decimal REWARD_LIVE_REG = 50m;
        /// <summary>
        /// 被推荐人实盘注册交易金
        /// </summary>
        public static decimal REWARD_REFERER = 30m;
        /// <summary>
        /// 推荐人交易金
        /// </summary>
        public static decimal REWARD_REFEREE = 30m;
        public const decimal REWARD_DEMO_TRADE = 0.5m;

        /// <summary>
        /// 送交易金的汇率，6.5
        /// </summary>
        public static decimal ExchangeRate = 6.5M;

        public CFDEntities db { get; set; }

        private static readonly IMapper Mapper = MapperConfig.GetAutoMapperConfiguration().CreateMapper();

        public RewardService(CFDEntities db)
        {
            this.db = db;
            try
            {
                var setting = db.Miscs.FirstOrDefault(m => m.Key == "RewardSetting");
                if (setting != null)
                {
                    REWARD_DEMO_PhoneREG = JObject.Parse(setting.Value)["demoAccount"].Value<decimal>();
                    REWARD_LIVE_REG = JObject.Parse(setting.Value)["liveAccount"].Value<decimal>();
                    REWARD_REFERER = JObject.Parse(setting.Value)["referer"].Value<decimal>();
                    REWARD_REFEREE = JObject.Parse(setting.Value)["referee"].Value<decimal>();
                    ExchangeRate = JObject.Parse(setting.Value)["exchangeRate"].Value<decimal>();
                }
            }
            catch (Exception ex)
            {
                CFDGlobal.LogInformation("模拟盘注册的交易金设置错误:" + ex.Message);
            }
        }

        public static void Refresh(CFDEntities db)
        {
            try
            {
                var setting = db.Miscs.FirstOrDefault(m => m.Key == "RewardSetting");
                if (setting != null)
                {
                    REWARD_DEMO_PhoneREG = JObject.Parse(setting.Value)["demoAccount"].Value<decimal>();
                    REWARD_LIVE_REG = JObject.Parse(setting.Value)["liveAccount"].Value<decimal>();
                    REWARD_REFERER = JObject.Parse(setting.Value)["referer"].Value<decimal>();
                    REWARD_REFEREE = JObject.Parse(setting.Value)["referee"].Value<decimal>();
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

        public decimal DemoRegReward(int userID, string phone)
        {
            decimal amount = 0;
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.Serializable }))
            {
                using (var dbIsol = CFDEntities.Create())
                {
                    if (!dbIsol.DemoRegisterRewards.Any(item => item.UserId == userID))
                    {
                        var reward = new DemoRegisterReward()
                        {
                            //手机注册送30，微信注册送5
                            Amount = string.IsNullOrEmpty(phone) ? RewardService.REWARD_DEMO_WeChatREG : RewardService.REWARD_DEMO_PhoneREG,
                            ClaimedAt = null,
                            UserId = userID,
                        };
                        dbIsol.DemoRegisterRewards.Add(reward);
                        dbIsol.SaveChanges();

                        amount = reward.Amount;
                    }
                }
                scope.Complete();
            }
            return amount;
        }

        public void DemoBindPhoneReward(int userID)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.Serializable }))
            {
                using (var dbIsol = CFDEntities.Create())
                {
                    var demoRegisterReward = dbIsol.DemoRegisterRewards.FirstOrDefault(item => item.UserId == userID);
                    if (demoRegisterReward != null)
                    {
                        if(demoRegisterReward.Amount == REWARD_DEMO_WeChatREG)
                        {
                            demoRegisterReward.Amount = REWARD_DEMO_PhoneREG;
                            dbIsol.SaveChanges();
                        }
                    }
                }
                scope.Complete();
            }
        }


        /// <summary>
        /// 入金的短信、被推荐人首次入金送推荐人30元。首日入金奖励
        /// </summary>
        /// <param name="newTransferHistories"></param>
        public void DepositReward(List<AyondoTransferHistoryBase> newTransferHistories)
        {
            var messages = new List<MessageBase>();
            var referRewards = new List<ReferReward>();
            var depositRewards = new List<DepositReward>();
            foreach (var transfer in newTransferHistories)
            {
                //入金的短信
                if (transfer.TransferType.ToLower() == "WeCollect - CUP".ToLower())
                {
                    try
                    {
                        var query = from u in db.Users
                                    join d in db.Devices on u.Id equals d.userId
                                    into x
                                    from y in x.DefaultIfEmpty()
                                    where u.AyLiveAccountId == transfer.TradingAccountId
                                    select new { y.deviceToken, UserId = u.Id, u.Phone, u.AyondoAccountId, u.AyLiveAccountId, u.AutoCloseAlert, u.AutoCloseAlert_Live, u.IsOnLive, y.UpdateTime };
                        var userInfo = query.FirstOrDefault();
                        if (userInfo != null && !string.IsNullOrEmpty(userInfo.deviceToken) && !string.IsNullOrEmpty(userInfo.Phone))
                        {
                            //短信
                            YunPianMessenger.SendSms(string.Format("【盈交易】您入金的{0}美元已到账", transfer.Amount), userInfo.Phone);

                            //入金信息放到消息中心
                            MessageBase msg = new MessageBase();
                            msg.UserId = userInfo.UserId;
                            msg.Title = "入金消息";
                            msg.Body = string.Format("您入金的{0}元已到账", transfer.Amount);
                            msg.CreatedAt = DateTime.UtcNow;
                            msg.IsReaded = false;
                            messages.Add(msg);

                            var referer = db.Users.FirstOrDefault(u => u.AyLiveAccountId == transfer.TradingAccountId);
                            decimal amount = RewardService.REWARD_REFEREE;
                            if (referer != null && !string.IsNullOrEmpty(referer.Phone))
                            {
                                var referHistory = db.ReferHistorys.FirstOrDefault(r => r.ApplicantNumber == referer.Phone);
                                if (referHistory != null && referHistory.IsRewarded != true)
                                {
                                    referHistory.IsRewarded = true;
                                    referHistory.RewardedAt = DateTime.Now;
                                    referRewards.Add(new ReferReward() { Amount = amount, UserID = referHistory.RefereeID, CreatedAt = DateTime.Now });
                                }
                            }

                            //首日入金交易金
                            decimal firstDepositDayReward = FirstDayDepositReward(transfer);
                            if(firstDepositDayReward > 0)
                            {
                                MessageBase msg1stDayDeposit = new MessageBase();
                                msg1stDayDeposit.UserId = userInfo.UserId;
                                msg1stDayDeposit.Title = "首日入金赠金";
                                msg1stDayDeposit.Body = string.Format("您的首日入金赠金{0}元已自动转入您的交易金账号", transfer.Amount);
                                msg1stDayDeposit.CreatedAt = DateTime.UtcNow;
                                msg1stDayDeposit.IsReaded = false;
                                messages.Add(msg1stDayDeposit);

                                DepositReward dr = new DepositReward();
                                dr.Amount = firstDepositDayReward;
                                dr.UserId = userInfo.UserId;
                                dr.DepositAmount = transfer.Amount;
                                dr.CreatedAt = DateTime.Now;
                                depositRewards.Add(dr);

                                var user = db.Users.FirstOrDefault(u => u.Id == userInfo.UserId);
                                if(!user.FirstDayRewarded.HasValue) //App首页提示用户拿到首日交易金。 Null未拿到，False已看过此消息，True已拿到交易金未看过消息
                                {
                                    user.FirstDayRewarded = true;
                                }
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        CFDGlobal.LogLine("Sending SMS failed for user:" + transfer.TradingAccountId);
                    }
                }
            }

            if (messages.Count > 0 || referRewards.Count > 0 || depositRewards.Count > 0)
            {
                if (messages.Count > 0)
                {
                    db.Message_Live.AddRange(messages.Select(m => Mapper.Map<Message_Live>(m)));
                }
                if (referRewards.Count > 0)
                {
                    db.ReferRewards.AddRange(referRewards);
                }
                if(depositRewards.Count > 0)
                {
                    db.DepositRewards.AddRange(depositRewards);
                }

                CFDGlobal.LogLine(string.Format("Saving message: {0} & refer reward: {1}", messages.Count, referRewards.Count));
                db.SaveChanges();
            }
        }

        /// <summary>
        /// 首日入金奖励
        /// </summary>
        public decimal FirstDayDepositReward(AyondoTransferHistoryBase transferHistory)
        {
            decimal rewardAmount = 0;
            decimal rewardRate = GetFirstDayRewadRate(transferHistory.Amount.Value);
            var firstDeposit = db.AyondoTransferHistory_Live.FirstOrDefault(t => t.TradingAccountId == transferHistory.TradingAccountId);
            if(firstDeposit == null || (firstDeposit.ApprovalTime.HasValue && firstDeposit.ApprovalTime.Value >= DateTime.Now.AddDays(-1))) //首次充值或首次充值的一天以内
            {
                rewardAmount = transferHistory.Amount.Value * rewardRate;
            }

            rewardAmount = rewardAmount > 10000 ? 10000 : rewardAmount;

            return rewardAmount * ExchangeRate;
        }

        public decimal GetFirstDayRewadRate(decimal amount)
        {
            decimal rate = 0;
            if(amount >= 200 && amount < 500)
            {
                rate = 0.1M;
            }
            else if (amount >= 500 && amount < 1000)
            {
                rate = 0.15M;
            }
            else if(amount > 1000)
            {
                rate = 0.2M;
            }

            return rate;
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
