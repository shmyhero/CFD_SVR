using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.DTO.Form;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
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
            string format = "{{\"type\":\"{1}\", \"title\":\"盈交易通知\", \"StockID\":0, \"CName\":\"\", \"message\":\"{0}\", \"deepLink\":\"{2}\"}}";
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
                                         where x.CreatedAt>startTime && x.CreatedAt < endTime && y.AyLiveUsername.Contains(form.name)
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
        [Route("channel/statistics")]
        [AdminAuth]
        public List<ChannelUserDTO> GetChannelStatistics()
        {
            var channelUsers = (from c in db.Channels
                                join r in db.RewardPhoneHistorys
                                on c.ChannelID equals r.ChannelID
                                where c.ExpiredAt == SqlDateTime.MaxValue.Value
                                group new { c.CreatedAt, c.ChannelName, r.ID, r.Phone } by c.ChannelID into g
                                select new ChannelUserDTO()
                                {
                                    channelID = g.Key,
                                     channelName = g.FirstOrDefault().ChannelName,
                                      createdAt = g.FirstOrDefault().CreatedAt?? DateTime.MinValue,
                                       registerCount = g.Count()
                                }).ToList();

            return channelUsers;
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
            var userIDList = users.Select(u => u.Id).ToList();
            var ayAccountList = users.Select(u => u.AyondoAccountId).ToList();
            var ayLiveAccountList = users.Select(u => u.AyLiveAccountId).ToList();

            var referHistorys = db.ReferHistorys.Where(rh => phoneList.Contains(rh.ApplicantNumber)).ToList();
            //避免多次查询数据库，把下面循环里的查询提升到这里
            db.Database.CommandTimeout = 600;//时间跨度长的话会超时
            var userInfos = db.UserInfos.Where(ui => userIDList.Contains(ui.UserId)).ToList();
            var demoTransHistory = (from t in db.AyondoTradeHistories
                                   group t by t.AccountId into h1
                                   let ayAccountID = h1.Key
                                   where ayAccountList.Contains(ayAccountID)
                                   select new
                                   {
                                       AyondoAccountID = h1.Key,
                                       DemoTransCount = h1.Count()
                                   }).ToList();
                                  
            var depositHistory = db.AyondoTransferHistory_Live.Where(CFD_COMMON.Utils.Transfer.IsDeposit(ayLiveAccountList)).ToList();
            RewardService service = new RewardService(db);
            var rewardList = service.GetTotalReward(userIDList);

            var rewardTransferList = (from r in db.RewardTransfers
                                      group r by r.UserID into h1
                                      let userID = h1.Key
                                      where userIDList.Contains(userID)
                                      select new
                                      {
                                          UserID = h1.Key,
                                          Amount = h1.Select(o=>o.Amount).DefaultIfEmpty(0).Sum()
                                      }).ToList();

            users.ForEach(u => {
                DemoUserDTO dto = new DemoUserDTO();
                dto.nickName = u.Nickname;
                dto.account = u.AyondoUsername;
                dto.phone = u.Phone;
                dto.demoSignedAt = u.CreatedAt.HasValue? u.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
                //dto.demoTransCount = db.AyondoTradeHistories.Count(t => t.AccountId == u.AyondoAccountId);
                var demoTrans = demoTransHistory.FirstOrDefault(t => t.AyondoAccountID == u.AyondoAccountId);
                if(demoTrans == null)
                {
                    dto.demoTransCount = 0;
                }
                else
                {
                    dto.demoTransCount = demoTrans.DemoTransCount;
                }
                 
                dto.lastLoginAt = u.LastHitAt.HasValue? u.LastHitAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
                //dto.reward = GetTotalReward(u.Id).Item1 - db.RewardTransfers.Where(o => o.UserID == u.Id).Select(o => o.Amount).DefaultIfEmpty(0).Sum();
                if(rewardList.ContainsKey(u.Id))
                {
                    decimal transferred = 0;
                    var rewardTransfer = rewardTransferList.FirstOrDefault(rt => rt.UserID == u.Id);
                    if(rewardTransfer != null)
                    {
                        transferred = rewardTransfer.Amount;
                    }
                    dto.reward = rewardList[u.Id].GetTotal() - transferred;
                }
                
                dto.liveSignedAt = u.AyLiveApproveAt.HasValue? u.AyLiveApproveAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
                dto.liveAccountName = u.AyLiveUsername;
                //var userInfo = db.UserInfos.FirstOrDefault(ui => ui.UserId == u.Id);
                //if(userInfo == null)
                //{
                //    dto.realName = string.Empty;
                //}
                //else
                //{
                //    dto.realName = userInfo.LastName + userInfo.FirstName;
                //}
                var userInfo = userInfos.FirstOrDefault(ui => ui.UserId == u.Id);
                if (userInfo == null)
                {
                    dto.realName = string.Empty;
                }
                else
                {
                    dto.realName = userInfo.LastName + userInfo.FirstName;
                }

                if (referHistorys.Any(r => r.ApplicantNumber == u.Phone))
                {
                    int refereeID = referHistorys.FirstOrDefault(r => r.ApplicantNumber == u.Phone).RefereeID;
                    var referee = db.Users.FirstOrDefault(u1 => u1.Id == refereeID);
                    if (referee != null)
                    {
                        dto.channel = referee.AyondoUsername;
                    }
                }
                else if(!string.IsNullOrEmpty(u.Phone)) //如果不是推荐过来的，就去RewardHistory表找手机对应的活动号
                {
                    var rewardHistorys = (from r in db.RewardPhoneHistorys
                                         join c in db.Channels
                                         on r.ChannelID equals c.ChannelID
                                         where r.Phone == u.Phone
                                         select new { name = c.ChannelName }).ToList();
                    if(rewardHistorys.Count > 0)
                    {
                        dto.channel = rewardHistorys[0].name;
                    }
                }
                else
                {
                    dto.channel = string.Empty;
                }
                //dto.isDeposited = db.AyondoTransferHistory_Live.Any(CFD_COMMON.Utils.Transfer.IsDeposit(u.AyLiveAccountId));
                dto.isDeposited = depositHistory.Any(d => d.AccountId == u.AyLiveAccountId);

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
                dto.userName = u.AyondoUsername;
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
                dto.quizReward = reward.Item2.quiz;
                result.Add(dto);
            });

            return result;
        }

        private Tuple<decimal, RewardDTO> GetTotalReward(int userID)
        {
            RewardService service = new RewardService(db);
            var rewardDetail = service.GetTotalReward(userID);
            var rewardDTO = new RewardDTO() { demoProfit = rewardDetail.demoProfit, referralReward = rewardDetail.referralReward, liveRegister = rewardDetail.liveRegister, demoRegister = rewardDetail.demoRegister, totalDailySign = rewardDetail.totalDailySign, totalCard = rewardDetail.totalCard, totalDemoTransaction = rewardDetail.totalDemoTransaction, firstDeposit = rewardDetail.firstDeposit, quiz = rewardDetail.quizSettled };
            return new Tuple<decimal, RewardDTO>(rewardDetail.GetTotal(), rewardDTO);
        }
        
        [HttpGet]
        [Route("activity/all")]
        [AdminAuth]
        public List<ActivityChannelDTO> GetAllActivities()
        {
            var activities = db.Activities.Where(a => a.ExpiredAt == SqlDateTime.MaxValue.Value).ToList();
            var channels = db.Channels.Where(c => c.ExpiredAt == SqlDateTime.MaxValue.Value).ToList();

            var result = (from u in db.Users
                                where u.ChannelID.HasValue && u.ActivityID.HasValue
                                group u by new { u.ActivityID, u.ChannelID } into g
                                select new ActivityChannelDTO()
                                {
                                    activityID = g.Key.ActivityID.Value,
                                    //activityName = activities.FirstOrDefault(a=>a.ActivityID == g.Key.ActivityID.Value) == null? string.Empty : activities.FirstOrDefault(a => a.ActivityID == g.Key.ActivityID.Value).Name,
                                    channelID = g.Key.ChannelID.Value,
                                    //channelName = channels.FirstOrDefault(c => c.ChannelID == g.Key.ChannelID.Value) == null ? string.Empty : channels.FirstOrDefault(c => c.ChannelID == g.Key.ChannelID.Value).ChannelName,
                                    personCount = g.Count()
                                }).ToList();

            result.ForEach(r => {
                r.activityName = activities.FirstOrDefault(a => a.ActivityID == r.activityID) == null ? string.Empty : activities.FirstOrDefault(a => a.ActivityID == r.activityID).Name;
                r.channelName = channels.FirstOrDefault(c => c.ChannelID == r.channelID) == null ? string.Empty : channels.FirstOrDefault(c => c.ChannelID == r.channelID).ChannelName;
            });

            return result;
        }
    }
}