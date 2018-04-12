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
            //var referRewards = new List<ReferReward>();
            var depositRewards = new List<DepositReward>();
            foreach (var transfer in newTransferHistories)
            {
                //入金的短信
                if (Transfer.IsDepositData(transfer))
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

                            //2.1.6 被推荐人注册就送30元交易金，被推荐人入金不再送钱给推荐人
                            //var referer = db.Users.FirstOrDefault(u => u.AyLiveAccountId == transfer.TradingAccountId);
                            //decimal amount = RewardService.REWARD_REFEREE;
                            //if (referer != null && !string.IsNullOrEmpty(referer.Phone))
                            //{
                            //    var referHistory = db.ReferHistorys.FirstOrDefault(r => r.ApplicantNumber == referer.Phone);
                            //    if (referHistory != null && referHistory.IsRewarded != true)
                            //    {
                            //        referHistory.IsRewarded = true;
                            //        referHistory.RewardedAt = DateTime.Now;
                            //        referRewards.Add(new ReferReward() { Amount = amount, UserID = referHistory.RefereeID, CreatedAt = DateTime.Now });
                            //    }
                            //}

                            //由每笔入金判断是否送交易金，改为首日累计，在第二天判断要送多少交易金
                            //代码移动到Job->FirstDayDepositRewardJob
                            ////首日入金交易金
                            //decimal firstDepositDayReward = FirstDayDepositReward(transfer);
                            //if(firstDepositDayReward > 0)
                            //{
                            //    MessageBase msg1stDayDeposit = new MessageBase();
                            //    msg1stDayDeposit.UserId = userInfo.UserId;
                            //    msg1stDayDeposit.Title = "首日入金赠金";
                            //    msg1stDayDeposit.Body = string.Format("您的首日入金赠金{0}元已自动转入您的交易金账号", transfer.Amount);
                            //    msg1stDayDeposit.CreatedAt = DateTime.UtcNow;
                            //    msg1stDayDeposit.IsReaded = false;
                            //    messages.Add(msg1stDayDeposit);

                            //    DepositReward dr = new DepositReward();
                            //    dr.Amount = firstDepositDayReward;
                            //    dr.UserId = userInfo.UserId;
                            //    dr.DepositAmount = transfer.Amount;
                            //    dr.CreatedAt = DateTime.Now;
                            //    depositRewards.Add(dr);

                            //    var user = db.Users.FirstOrDefault(u => u.Id == userInfo.UserId);
                            //    if(!user.FirstDayRewarded.HasValue) //App首页提示用户拿到首日交易金。 Null未拿到，False已看过此消息，True已拿到交易金未看过消息
                            //    {
                            //        user.FirstDayRewarded = true;
                            //    }
                            //}

                        }
                    }
                    catch (Exception ex)
                    {
                        CFDGlobal.LogLine("Sending SMS failed for user:" + transfer.TradingAccountId);
                    }
                }
            }

            //if (messages.Count > 0 || referRewards.Count > 0 || depositRewards.Count > 0)
            if (messages.Count > 0 || depositRewards.Count > 0)
            {
                if (messages.Count > 0)
                {
                    db.Message_Live.AddRange(messages.Select(m => Mapper.Map<Message_Live>(m)));
                }
                //if (referRewards.Count > 0)
                //{
                //    db.ReferRewards.AddRange(referRewards);
                //}
                if(depositRewards.Count > 0)
                {
                    db.DepositRewards.AddRange(depositRewards);
                }

                CFDGlobal.LogLine(string.Format("Saving message: {0}", messages.Count));
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
            if(firstDeposit == null || (firstDeposit.ApprovalTime.HasValue && firstDeposit.ApprovalTime.Value >= DateTime.UtcNow.AddDays(-1))) //首次充值或首次充值的一天以内
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

        public RewardDetail GetTotalReward(int userID)
        {
            //reward for daily sign
            decimal totalDailySignReward = db.DailySigns
                .Where(o => o.UserId == userID && !o.IsPaid.Value)
                .Select(o => o.Amount).DefaultIfEmpty(0).Sum();

            //reward for daily demo trasaction
            var totalDemoTransactionReward = db.DailyTransactions
                .Where(o => o.UserId == userID && !o.IsPaid.Value)
                .Select(o => o.Amount).DefaultIfEmpty(0).Sum();

            var totalCard = db.UserCards_Live.Where(o => (!o.IsPaid.HasValue || !o.IsPaid.Value) && o.UserId == userID).Select(o => o.Reward).DefaultIfEmpty(0).Sum();

            //reward for demo register
            var reward = db.DemoRegisterRewards.FirstOrDefault(o => o.UserId == userID);
            decimal demoRegisterReward = reward == null ? 0 : reward.Amount;

            //实盘账户注册交易金
            var liveReward = db.LiveRegisterRewards.FirstOrDefault(o => o.UserId == userID);
            decimal liveRegisterReward = liveReward == null ? 0 : liveReward.Amount;

            //推荐人奖励
            var referRewardAmount = db.ReferRewards.Where(o => o.UserID == userID).Select(o => o.Amount).DefaultIfEmpty(0).Sum();

            //首日入金交易金
            decimal firstDepositReward = 0;
            var depositRewards = db.DepositRewards.Where(o => o.UserId == userID);
            if (!(depositRewards == null || depositRewards.Count() == 0))
            {
                firstDepositReward = depositRewards.Sum(o => o.Amount);
            }

            //模拟收益交易金
            decimal demoProfit = 0;
            var demoRewards = db.DemoProfitRewards.Where(o => o.UserId == userID);
            if (!(demoRewards == null || demoRewards.Count() == 0))
            {
                demoProfit = demoRewards.Sum(o => o.Amount);
            }

            //竞猜活动
            decimal quizSettled = 0;
            decimal quizUnSettled = 0;
            var quizRewards = db.QuizBets.Where(o => o.UserID == userID).ToList();
            if (!(quizRewards == null || quizRewards.Count() == 0))
            {
                quizRewards.ForEach(q => {
                    if(!q.SettledAt.HasValue) //还出结果的竞猜
                    {
                        quizUnSettled += q.PL?? 0;
                    }
                    else //竞猜有结果的话，PL要减去BetAmount
                    {
                        quizSettled += (q.PL ?? 0) - (q.BetAmount?? 0);
                    }

                });
            }

            return new RewardDetail() { demoProfit = demoProfit, referralReward = referRewardAmount, liveRegister = liveRegisterReward, demoRegister = demoRegisterReward, totalDailySign = totalDailySignReward, totalCard = totalCard.Value, totalDemoTransaction = totalDemoTransactionReward, firstDeposit = firstDepositReward, quizSettled = quizSettled, quizUnSettled = quizUnSettled };
        }

        public Dictionary<int,RewardDetail> GetTotalReward(List<int> userIDList)
        {
            Dictionary<int, RewardDetail> result = new Dictionary<int, RewardDetail>();

            userIDList.ForEach(uID => {
                if(!result.ContainsKey(uID))
                {
                    result.Add(uID, new RewardDetail());
                }
            });

            //reward for daily sign
            var DailySignRewards = db.DailySigns
                .Where(o => userIDList.Contains(o.UserId) && !o.IsPaid.Value).ToList();
            userIDList.ForEach(uID => {
                var rewardDetail = result[uID];
                rewardDetail.totalDailySign = DailySignRewards.Where(d => d.UserId == uID).Select(d => d.Amount).DefaultIfEmpty(0).Sum();
                result[uID] = rewardDetail;
            });


            //reward for daily demo trasaction
            //var totalDemoTransactionReward = db.DailyTransactions
            //    .Where(o => userIDList.Contains(o.UserId) && !o.IsPaid.Value)
            //    .Select(o => o.Amount).DefaultIfEmpty(0).Sum();
            var totalDemoTransactionReward = db.DailyTransactions
               .Where(o => userIDList.Contains(o.UserId) && !o.IsPaid.Value).ToList();
            userIDList.ForEach(uID => {
                var rewardDetail = result[uID];
                rewardDetail.totalDemoTransaction = totalDemoTransactionReward.Where(d => d.UserId == uID).Select(d => d.Amount).DefaultIfEmpty(0).Sum();
                result[uID] = rewardDetail;
            });

            //卡片奖励
            //var totalCard = db.UserCards_Live.Where(o => (!o.IsPaid.HasValue || !o.IsPaid.Value) && userIDList.Contains(o.UserId)).Select(o => o.Reward).DefaultIfEmpty(0).Sum();
            var totalCard = db.UserCards_Live.Where(o => (!o.IsPaid.HasValue || !o.IsPaid.Value) && userIDList.Contains(o.UserId)).ToList();
            userIDList.ForEach(uID => {
                var rewardDetail = result[uID];
                rewardDetail.totalCard = totalCard.Where(d => d.UserId == uID).Select(d => d.Reward).DefaultIfEmpty(0).Sum().Value;
                result[uID] = rewardDetail;
            });

            //reward for demo register
            //var reward = db.DemoRegisterRewards.FirstOrDefault(o => userIDList.Contains(o.UserId));
            //decimal demoRegisterReward = reward == null ? 0 : reward.Amount;
            var demoRegisterReward = db.DemoRegisterRewards.Where(o => userIDList.Contains(o.UserId)).ToList();
            userIDList.ForEach(uID => {
                var rewardDetail = result[uID];
                rewardDetail.demoRegister = demoRegisterReward.Where(d => d.UserId == uID).Select(d => d.Amount).DefaultIfEmpty(0).Sum();
                result[uID] = rewardDetail;
            });

            //实盘账户注册交易金
            //var liveReward = db.LiveRegisterRewards.FirstOrDefault(o => userIDList.Contains(o.UserId));
            //decimal liveRegisterReward = liveReward == null ? 0 : liveReward.Amount;
            var liveRegisterReward = db.LiveRegisterRewards.Where(o => userIDList.Contains(o.UserId)).ToList();
            userIDList.ForEach(uID => {
                var rewardDetail = result[uID];
                rewardDetail.liveRegister = liveRegisterReward.Where(d => d.UserId == uID).Select(d => d.Amount).DefaultIfEmpty(0).Sum();
                result[uID] = rewardDetail;
            });

            //推荐人奖励
            //var referRewardAmount = db.ReferRewards.Where(o => userIDList.Contains(o.UserID)).Select(o => o.Amount).DefaultIfEmpty(0).Sum();
            var referRewardAmount = db.ReferRewards.Where(o => userIDList.Contains(o.UserID)).ToList();
            userIDList.ForEach(uID => {
                var rewardDetail = result[uID];
                rewardDetail.referralReward = referRewardAmount.Where(d => d.UserID == uID).Select(d => d.Amount).DefaultIfEmpty(0).Sum();
                result[uID] = rewardDetail;
            });


            //首日入金交易金
            //decimal firstDepositReward = 0;
            //var depositRewards = db.DepositRewards.Where(o => userIDList.Contains(o.UserId));
            //if (!(depositRewards == null || depositRewards.Count() == 0))
            //{
            //    firstDepositReward = depositRewards.Sum(o => o.Amount);
            //}
            var depositRewards = db.DepositRewards.Where(o => userIDList.Contains(o.UserId)).ToList();
            userIDList.ForEach(uID => {
                var rewardDetail = result[uID];
                rewardDetail.firstDeposit = depositRewards.Where(d => d.UserId == uID).Select(d => d.Amount).DefaultIfEmpty(0).Sum();
                result[uID] = rewardDetail;
            });

            //模拟收益交易金
            //decimal demoProfit = 0;
            //var demoRewards = db.DemoProfitRewards.Where(o => userIDList.Contains(o.UserId));
            //if (!(demoRewards == null || demoRewards.Count() == 0))
            //{
            //    demoProfit = demoRewards.Sum(o => o.Amount);
            //}
            var demoProfitRewards = db.DemoProfitRewards.Where(o => userIDList.Contains(o.UserId)).ToList();
            userIDList.ForEach(uID => {
                var rewardDetail = result[uID];
                rewardDetail.demoProfit = demoProfitRewards.Where(d => d.UserId == uID).Select(d => d.Amount).DefaultIfEmpty(0).Sum();
                result[uID] = rewardDetail;
            });

            //竞猜活动
            var quizRewards = db.QuizBets.Where(o => userIDList.Contains(o.UserID)).ToList();
            userIDList.ForEach(uID => {
                decimal quizSettled = 0;
                decimal quizUnSettled = 0;
                var rewardDetail = result[uID];
                var quizRewardsByUserID = quizRewards.Where(o => o.UserID == uID).ToList();
                if (!(quizRewardsByUserID == null || quizRewardsByUserID.Count() == 0))
                {
                    quizRewardsByUserID.ForEach(q =>
                    {
                        if (!q.SettledAt.HasValue) //还出结果的竞猜
                        {
                            quizUnSettled += q.PL ?? 0;
                        }
                        else //竞猜有结果的话，PL要减去BetAmount
                        {
                            quizSettled += (q.PL ?? 0) - (q.BetAmount ?? 0);
                        }
                    });
                    rewardDetail.quizSettled = quizSettled;
                    rewardDetail.quizUnSettled = quizUnSettled;
                    result[uID] = rewardDetail;
                }
            });
            return result;
        }
    }

    public struct RewardDetail
    {
        public decimal totalDailySign { get; set; }
        /// <summary>
        /// 模拟交易奖励汇总
        /// </summary>
        public decimal totalDemoTransaction { get; set; }
        /// <summary>
        /// 卡牌产生的交易金
        /// </summary>
        public decimal totalCard { get; set; }
        /// <summary>
        /// 模拟盘注册奖励
        /// </summary>
        public decimal demoRegister { get; set; }

        /// <summary>
        /// 实盘注册奖励
        /// </summary>
        public decimal liveRegister { get; set; }

        /// <summary>
        /// 好友推荐奖励
        /// </summary>
        public decimal referralReward { get; set; }
        /// <summary>
        /// 首笔入金的交易金
        /// </summary>
        public decimal firstDeposit { get; set; }
        /// <summary>
        /// 模拟收益交易金
        /// </summary>
        public decimal demoProfit { get; set; }
        /// <summary>
        /// 已结算的竞猜活动
        /// </summary>
        public decimal quizSettled { get; set; }

        //未结算的竞猜活动
        public decimal quizUnSettled { get; set; }

        public decimal GetTotal()
        {
            return totalDailySign + totalDemoTransaction + totalCard + demoRegister + liveRegister + referralReward + firstDeposit + demoProfit + quizSettled + quizUnSettled;
        }
    }
}
