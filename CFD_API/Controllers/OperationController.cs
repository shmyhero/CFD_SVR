using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.DTO.Form;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using Newtonsoft.Json.Linq;
using ServiceStack.Redis;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Web.Http;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/operation")]
    public class OperationController : CFDController
    {
        public OperationController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }

        public string Test()
        {
            return null;
        }

        /// <summary>
        /// 由运营人员发起的消息推送
        /// </summary>
        /// <param name="form"></param>
        /// <returns></returns>
        [HttpPost]
        [AdminAuth]
        public ResultDTO Push(OperationPushDTO form)
        {
            ResultDTO result = new ResultDTO() { success = true };
            var phoneList = form.phone.Split(',').ToList();//requestObj["phone"].Value<string>().Split(',').ToList();

            var tokenListQuery = from u in db.Users
                                             join d in db.Devices on u.Id equals d.userId
                                             where phoneList.Contains(u.Phone) 
                                             select new { d.deviceToken, u.Id, u.AyondoAccountId, u.AutoCloseAlert };

            var tokenList = tokenListQuery.ToList();
            string msg = form.message;
            string pushType = string.IsNullOrEmpty(form.deepLink) ? "0" : "3";

            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            string format = "{{\"type\":\"{1}\", \"title\":\"盈交易测试\", \"StockID\":0, \"CName\":\"\", \"message\":\"{0}\", \"deepLink\":\"{2}\"}}";
            foreach(var token in tokenList)
            {
                list.Add(new KeyValuePair<string, string>(token.deviceToken, string.Format(format,msg, pushType, form.deepLink)));
            }

            var push = new GeTui();
            var response = push.PushBatch(list);
            result.message = response;
            return result;
        }

        [HttpPost]
        [Route("reward/transfer")]
        [AdminAuth]
        public List<RewardTransferDTO> GetRewardTransferHistory(RewardTransferSearchDTO form)
        {
            if (form == null || string.IsNullOrEmpty(form.startTime) || string.IsNullOrEmpty(form.startTime))
            {
                return null;
            }

            DateTime startTime = DateTime.Parse(form.startTime);
            DateTime endTime = DateTime.Parse(form.endTime);

            var rewardTransferHistory = (from x in db.RewardTransfers
                                         join y in db.Users on x.UserID equals y.Id
                                         join z in db.UserInfos on y.Id equals z.UserId
                                         into t1
                                         from t2 in t1.DefaultIfEmpty()
                                         where x.CreatedAt>startTime && x.CreatedAt < endTime
                                         select new RewardTransferDTO() { liveAccount = y.AyLiveUsername, liveAccountID = y.AyLiveAccountId.HasValue? y.AyLiveAccountId.Value.ToString() : string.Empty, name = t2.LastName + t2.FirstName, amount = x.Amount, date = x.CreatedAt }).ToList();
            return rewardTransferHistory;
        }

        /// <summary>
        /// 记录通过活动、渠道的手机号
        /// channelID小于0的是活动，活动之间手机号可以重复登记
        /// channelID大于0的是渠道，渠道之间手机号不可以重复登记
        /// </summary>
        /// <param name="form"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("reward/phone")]
        public ResultDTO RecordPhone(CheckPhoneDTO form)
        {
            if(string.IsNullOrEmpty(form.phone) || string.IsNullOrEmpty(form.verifyCode))
            {
                return new ResultDTO() { success = false, message = "缺少参数" };
            }

            if(form.channelID > 0) //大于0就是渠道过来的登记，各渠道间手机后不能重复
            {
                if (db.RewardPhoneHistorys.Any(o => o.Phone == form.phone && o.ChannelID > 0))
                {
                    return new ResultDTO() { success = false, message = "该手机号已参加过活动" };
                }
            }
            else //小于等于0就是活动过来的登记，各个活动间的手机号可以重复
            {
                if (db.RewardPhoneHistorys.Any(o => o.Phone == form.phone && o.ChannelID == form.channelID))
                {
                    return new ResultDTO() { success = false, message = "该手机号已参加过活动" };
                }
            }
            
            var dtValidSince = DateTime.UtcNow - TimeSpan.FromHours(1);
            if(db.VerifyCodes.Any(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince))
            {
                RewardPhoneHistory rph = new RewardPhoneHistory() {
                     ChannelID = form.channelID,
                     Phone = form.phone,
                     CreatedAt = DateTime.Now
                };
                db.RewardPhoneHistorys.Add(rph);
                db.SaveChanges();
                return new ResultDTO() { success = true, message = "OK" };
            }
            else
            {
                return new ResultDTO() { success = false, message = "验证码校验失败" };
            }

        }

        [HttpGet]
        [Route("demouser")]
        [AdminAuth]
        /// <summary>
        /// 模拟用户信息管理
        /// </summary>
        /// <returns></returns>
        public List<DemoUserDTO> SearchDemoUser(string phone, string start, string end)
        {
            List<DemoUserDTO> result = new List<DemoUserDTO>();

            DateTime startTime = SqlDateTime.MinValue.Value;
            if (!string.IsNullOrEmpty(start))
                DateTime.TryParse(start, out startTime);

            DateTime endTime = SqlDateTime.MaxValue.Value;
            if (!string.IsNullOrEmpty(end))
                DateTime.TryParse(end, out endTime);

            //var users = db.Users.Where(u => u.Phone.Contains(phone)).OrderByDescending(u => u.CreatedAt).ToList();
            List<User> users = new List<User>();
            if (string.IsNullOrEmpty(phone))
            {
                //如果三个查询参数都为空，就返回当日实盘开户的信息
                if(startTime == SqlDateTime.MinValue.Value && endTime == SqlDateTime.MaxValue.Value)
                {
                    DateTime utcToday = DateTime.Today.AddHours(-8);
                    users = db.Users.Where(u => (u.AyLiveApproveAt >= utcToday)).ToList();
                }
                else
                {
                    users = db.Users.Where(u => (u.CreatedAt >= startTime && u.CreatedAt <= endTime)).ToList();
                }
            }
            else
            {
                users = db.Users.Where(u => (u.Phone.Contains(phone) && u.CreatedAt >= startTime && u.CreatedAt <= endTime)).ToList();
            }

            var phoneList = users.Select(u => u.Phone).ToList();

            var referHistorys = db.ReferHistorys.Where(rh => phoneList.Contains(rh.ApplicantNumber)).ToList();

            users.ForEach(u => {
                DemoUserDTO dto = new DemoUserDTO();
                dto.nickName = u.Nickname;
                dto.account = u.AyondoUsername;
                dto.phone = u.Phone;
                dto.demoSignedAt = u.CreatedAt.HasValue? u.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
                dto.demoTransCount = db.AyondoTradeHistories.Count(t => t.AccountId == u.AyondoAccountId);
                dto.lastLoginAt = u.LastHitAt.HasValue? u.LastHitAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
                dto.reward = GetTotalReward(u.Id).Item1 - db.RewardTransfers.Where(o => o.UserID == u.Id).Select(o => o.Amount).DefaultIfEmpty(0).Sum();
                dto.liveSignedAt = u.AyLiveApproveAt.HasValue? u.AyLiveApproveAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
                dto.liveAccountName = u.AyLiveUsername;
                var userInfo = db.UserInfos.FirstOrDefault(ui => ui.UserId == u.Id);
                if(userInfo == null)
                {
                    dto.realName = string.Empty;
                }
                else
                {
                    dto.realName = userInfo.LastName + userInfo.FirstName;
                }
                
                if(referHistorys.Any(r => r.ApplicantNumber == u.Phone))
                {
                    int refereeID = referHistorys.FirstOrDefault(r => r.ApplicantNumber == u.Phone).RefereeID;
                    var referee = db.Users.FirstOrDefault(u1 => u1.Id == refereeID);
                    if (referee != null)
                    {
                        dto.channel = referee.Nickname;
                    }
                }


                result.Add(dto);
            });

            return result;
        }

        [HttpGet]
        [Route("userreward")]
        [AdminAuth]
        public List<UserRewardDTO> SearchUserReward(string phone, string start, string end)
        {
            List<UserRewardDTO> result = new List<UserRewardDTO>();
            if (string.IsNullOrEmpty(phone) && string.IsNullOrEmpty(start) && string.IsNullOrEmpty(end))
            {
                return result;
            }

            DateTime startTime = SqlDateTime.MinValue.Value;
            if(!string.IsNullOrEmpty(start))
                DateTime.TryParse(start, out startTime);

            DateTime endTime = SqlDateTime.MaxValue.Value;
            if (!string.IsNullOrEmpty(end))
                DateTime.TryParse(end, out endTime);

            List<User> users = new List<User>();
            if(string.IsNullOrEmpty(phone))
            {
                users = db.Users.Where(u => (u.CreatedAt >= startTime && u.CreatedAt <= endTime)).ToList();
            }
            else
            {
                users = db.Users.Where(u => (u.Phone.Contains(phone) && u.CreatedAt >= startTime && u.CreatedAt <= endTime)).ToList();
            }

            users.ForEach(u => {
                UserRewardDTO dto = new UserRewardDTO();
                dto.nickName = u.Nickname;
                dto.phone = u.Phone;
                var reward = GetTotalReward(u.Id);
                dto.totalReward = reward.Item1;
                dto.remainingReward = reward.Item1 - db.RewardTransfers.Where(o => o.UserID == u.Id).Select(o => o.Amount).DefaultIfEmpty(0).Sum();
                dto.firstDayReward = reward.Item2.firstDeposit;
                dto.demoProfitReward = reward.Item2.demoProfit;
                dto.dailySignReward = reward.Item2.totalDailySign;
                dto.demoTransReward = reward.Item2.totalDemoTransaction;
                dto.demoRegisterReward = reward.Item2.demoRegister;
                dto.cardReward = reward.Item2.totalCard;
                dto.liveRegisterReward = reward.Item2.liveRegister;
                dto.friendsReward = reward.Item2.referralReward;

                result.Add(dto);
            });

            return result;
        }

        private Tuple<decimal, RewardDTO> GetTotalReward(int userID)
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
            var referReward = db.ReferRewards.FirstOrDefault(o => o.UserID == userID);
            decimal referRewardAmount = referReward == null ? 0 : referReward.Amount;

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

            RewardDTO totalReward = new RewardDTO() { demoProfit = demoProfit, referralReward = referRewardAmount, liveRegister = liveRegisterReward, demoRegister = demoRegisterReward, totalDailySign = totalDailySignReward, totalCard = totalCard.Value, totalDemoTransaction = totalDemoTransactionReward, firstDeposit = firstDepositReward };
            return new Tuple<decimal, RewardDTO>(demoProfit + referRewardAmount + liveRegisterReward + demoRegisterReward + totalDailySignReward + totalCard.Value + totalDemoTransactionReward + firstDepositReward, totalReward) ;
        }
        
    }
}