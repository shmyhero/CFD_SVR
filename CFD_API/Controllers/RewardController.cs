using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using ServiceStack.Redis;
using Newtonsoft.Json.Linq;
using System.Net;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/reward")]
    public class RewardController : CFDController
    {
        public RewardController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }
        
        [HttpGet]
        [Route("refresh")]
        public ResultDTO RefreshRewardService()
        {
            RewardService.Refresh(db);
            return new ResultDTO() { success = true };
        }

        [HttpGet]
        [Route("checkIn/share")]
        public RewardIntroDTO GetDailySignRewardIntro()
        {
            return new RewardIntroDTO()
            {
                url = CFDGlobal.TH_WEB_HOST + "TH_CFD_WEB/checkinRule.php",
                imgUrl = CFDGlobal.TH_WEB_HOST + "TH_CFD_WEB/images/ShareLogo.png",
                title = "签到赚取实盘资金",
                text = "模拟注册可赚取" + RewardService.REWARD_DEMO_PhoneREG + "元；每日签到可赚取" + RewardService.CHECK_IN_DAY_1_TO_5 + "元；每日模拟交易可赚取" + RewardService.REWARD_DEMO_TRADE + "元。",
            };
        }

        [HttpGet]
        [Route("demoReg/share")]
        public RewardIntroDTO GetDemoRegRewardIntro()
        {
            return new RewardIntroDTO()
            {
                url = CFDGlobal.TH_WEB_HOST + "TH_CFD_WEB/checkinRule.php",
                imgUrl = CFDGlobal.TH_WEB_HOST + "TH_CFD_WEB/images/ShareLogo.png",
                title = "模拟注册获得"+RewardService.REWARD_DEMO_PhoneREG+"元交易金",
                text = "模拟注册可赚取" + RewardService.REWARD_DEMO_PhoneREG + "元；每日签到可赚取" + RewardService.CHECK_IN_DAY_1_TO_5 + "元；每日模拟交易可赚取" + RewardService.REWARD_DEMO_TRADE + "元。",
            };
        }

        [HttpGet]
        [Route("checkIn")]
        [BasicAuth]
        public ResultDTO DailySign()
        {
            //ResultDTO result = new ResultDTO() { success = true };

            var rewardService=new RewardService(db);
            var isOK = rewardService.CheckIn(UserId);
            return new ResultDTO() {success = isOK};

            //DailySign lastDailySign = db.DailySigns.Where(d => d.UserId == this.UserId).OrderByDescending(d => d.Date).FirstOrDefault();

            //var chinaNow = DateTimes.GetChinaNow();

            //DailySign todayDailySign = new DailySign();
            //todayDailySign.UserId = this.UserId;
            //todayDailySign.SignAt = chinaNow;
            //todayDailySign.Date = chinaNow.Date;
            //todayDailySign.IsPaid = false;

            //if (lastDailySign == null) //first time sign in
            //{
            //    todayDailySign.Continuity = 1;
            //    todayDailySign.Amount = RewardService.CHECK_IN_DAY_1_TO_5;
            //}
            //else //signed in before
            //{
            //    if (!lastDailySign.Date.HasValue)//should not happen. if happened, continue from day 1
            //    {
            //        todayDailySign.Continuity = 1;
            //        todayDailySign.Amount = RewardService.CHECK_IN_DAY_1_TO_5;
            //    }
            //    else
            //    {
            //        if (lastDailySign.Date.Value == chinaNow.Date)//already signed in today
            //        {
            //            result = new ResultDTO() { success = false, message = "Already sign in today." };
            //            return result;
            //        }
            //        else if (lastDailySign.Date.Value.AddDays(1) == chinaNow.Date) //continuous sign 
            //        {
            //            todayDailySign.Continuity = lastDailySign.Continuity + 1;
            //            if (todayDailySign.Continuity <= 5)
            //            {
            //                todayDailySign.Amount = RewardService.CHECK_IN_DAY_1_TO_5;
            //            }
            //            else if (todayDailySign.Continuity <= 10)
            //            {
            //                todayDailySign.Amount = RewardService.CHECK_IN_DAY_6_TO_10;
            //            }
            //            else
            //            {
            //                todayDailySign.Amount = RewardService.CHECK_IN_DAY_11_TO_X;
            //            }
            //        }
            //        else //break before
            //        {
            //            todayDailySign.Continuity = 1;
            //            todayDailySign.Amount = RewardService.CHECK_IN_DAY_1_TO_5;
            //        }
            //    }
            //}

            //db.DailySigns.Add(todayDailySign);
            //db.SaveChanges();

            //return result;
        }

        //[HttpGet]
        //[Route("dailysign/month/{month}")]
        //[BasicAuth]
        //public List<DailySignDTO> GetMonthSign(int month)
        //{
        //    if (month < 1 || month > 12)
        //        return new List<DailySignDTO>();

        //    List<DailySignDTO> dayList = new List<DailySignDTO>();
        //    DateTime startDate = new DateTime(DateTime.UtcNow.AddHours(8).Year, month, 1);
        //    DateTime endDate = month == 12? new DateTime(DateTime.UtcNow.AddHours(8).Year +1, 1, 1) : new DateTime(DateTime.UtcNow.AddHours(8).Year, month + 1, 1);
        //    List<DailySign> dailySignList = db.DailySigns.Where(d => d.UserId == this.UserId && d.Date >= startDate && d.Date < endDate).OrderBy(d => d.Date).ToList();

        //    dayList.AddRange(dailySignList.Select(o => {
        //        return new DailySignDTO() { Day = o.SignAt.HasValue ? o.SignAt.Value.Day : 0 };
        //    }));

        //    return dayList;
        //}

        [HttpGet]
        [Route("checkIn/month")]
        [BasicAuth]
        public MonthDailyCheckInDTO GetCurrentMonthSign()
        {
            var chinaNow = DateTimes.GetChinaNow();

            //List<DailySignDTO> dayList = new List<DailySignDTO>();
            DateTime startDate = new DateTime(chinaNow.Year, chinaNow.Month, 1);
            DateTime nextMonthFirstDay = new DateTime(chinaNow.AddMonths(1).Year, chinaNow.AddMonths(1).Month, 1);
            var days = db.DailySigns
                .Where(d => d.UserId == this.UserId && d.Date >= startDate && d.Date < nextMonthFirstDay)
                .OrderBy(d => d.Date)
                .Select(o => new DailySignDTO() { day = o.SignAt.Value.Day})
                .ToList();

            //dayList.AddRange(dailySignList.Select(o => {
            //    return new DailySignDTO() { Day = o.SignAt.HasValue ? o.SignAt.Value.Day : 0 };
            //}));

            return new MonthDailyCheckInDTO()
            {
                days = days,
                month = chinaNow.Month,
                monthDayCount = DateTime.DaysInMonth(chinaNow.Year, chinaNow.Month),
            };
        }

        [HttpGet]
        [Route("unpaid")]
        [BasicAuth]
        public RewardDTO GetTotalUnpaidReward()
        {
            RewardService service = new RewardService(db);
            var rewardDetail = service.GetTotalReward(UserId);

            return new RewardDTO() { demoProfit = rewardDetail.demoProfit, referralReward = rewardDetail.referralReward, liveRegister = rewardDetail.liveRegister, demoRegister = rewardDetail.demoRegister, totalDailySign = rewardDetail.totalDailySign, totalCard = rewardDetail.totalCard, totalDemoTransaction = rewardDetail.totalDemoTransaction, firstDeposit = rewardDetail.firstDeposit, quizReward = rewardDetail.quizReward };
        }

        [HttpGet]
        [Route("total")]
        [BasicAuth]
        public TotalRewardDTO GetTotalReward()
        {
            //所有记录下的交易金
            var reward = GetTotalUnpaidReward();
            //所有已经被转的交易金
            var transfer = db.RewardTransfers.Where(o => o.UserID == UserId).Select(o => o.Amount).DefaultIfEmpty(0).Sum();
            //小于0的竞猜收益作为支出
            transfer += db.QuizBets.Where(o => o.UserID == UserId && o.PL < 0).Select(o => o.PL).DefaultIfEmpty(0).Sum(o => o.Value);


            //用户累计入金
            var user = GetUser();
            //to-do 用户入金的交易类型是不是：WeCollect - CUP 和 Bank Wire
            //Bank Wire可能是运营给的赠金，也可能是用户的入金，需要区分开吗？
            //var totalDeposit = db.AyondoTransferHistory_Live.Where(o => o.TradingAccountId == user.AyLiveAccountId && CFD_COMMON.Utils.Transfer.IsDepositData(o)).Select(o=>o.Amount).Sum();
            var totalDeposit = db.AyondoTransferHistory_Live.Where(CFD_COMMON.Utils.Transfer.IsDeposit(user.AyLiveAccountId)).Select(o => o.Amount).Sum();

            decimal rewardTransferLimit = 60; //转出交易金的限制，必须大于60
            decimal depositLimit = 200; //入金的限制，必须累计大于200
            string rewardLimitMessage = "剩余交易金≥60元, 才能转⼊实盘账户";
            string depositLimitMessage = "累计⼊金≥200美元，才能转⼊实盘账户";
            var rewardSetting = db.Miscs.FirstOrDefault(o => o.Key == "TransferSetting");
            if (rewardSetting != null) //如果数据库有配置，就用数据库的配置
            {
                var setting = JObject.Parse(rewardSetting.Value);
                rewardTransferLimit = setting["rewardLimit"].Value<decimal>();
                depositLimit = setting["depositLimit"].Value<decimal>();
                rewardLimitMessage = setting["rewardLimitMessage"].Value<string>();
                depositLimitMessage = setting["depositLimitMessage"].Value<string>();
            }
            var totalReward = new TotalRewardDTO() {
                total = reward.referralReward + reward.liveRegister + reward.demoRegister + reward.totalCard + reward.totalDailySign + reward.totalDemoTransaction + reward.firstDeposit + reward.demoProfit + reward.quizReward,
                paid = transfer,
                canTransfer = true,
                minTransfer = rewardTransferLimit,
                totalDeposit = totalDeposit,
            };
            if ((totalReward.total - totalReward.paid) < rewardTransferLimit)
            {
                totalReward.message = rewardLimitMessage;
                totalReward.canTransfer = false;
            }
            else if(totalDeposit < depositLimit)
            {
                totalReward.message = depositLimitMessage;
                totalReward.canTransfer = false;
            }

            //竞猜盈利部分统计
            decimal? quizProfit = db.QuizBets.Where(qb => qb.UserID == UserId && qb.PL > 0).Select(qb => qb.PL - qb.BetAmount).DefaultIfEmpty(0).Sum();
            totalReward.quiz = quizProfit ?? 0;

            return totalReward;
        }

        [HttpGet]
        [Route("register")]
        public int GetRegisterReward()
        {
            return 30;
        }

        private static object transferLock = new object();
        /// <summary>
        /// 鼓励金转到实盘账户
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("transfer/{amount}")]
        [BasicAuth]
        public ResultDTO Transfer(decimal amount)
        {
            var forbiddenList = db.Miscs.FirstOrDefault(m => m.Key == "RewardTransferBlackList");
            if(forbiddenList != null && forbiddenList.Value.Split(';').Contains(UserId.ToString()))
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "请联系客服"));
            }

            lock (transferLock)
            {
                var reward = GetTotalReward();
                if (reward == null)
                {
                    return new ResultDTO() { success = false, message = "鼓励金为空" };
                }
                else if ((reward.total - reward.paid) < amount)
                {
                    return new ResultDTO() { success = false, message = "剩余鼓励金不足" };
                }
                else if (amount < 0)
                {
                    return new ResultDTO() { success = false, message = "金额不能为负" };
                }

                RewardTransfer transfer = new RewardTransfer() { UserID = UserId, Amount = amount, CreatedAt = DateTime.UtcNow };
                db.RewardTransfers.Add(transfer);
                db.SaveChanges();
            }
            return new ResultDTO() { success = true };
        }

        /// <summary>
        /// 用户在App上点击过"首日入金奖励"的提示
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("firstday/clicked")]
        [BasicAuth]
        public ResultDTO FirstDayPopupClicked()
        {
            var user = GetUser();
            if(!user.FirstDayClicked.HasValue || !user.FirstDayClicked.Value)
            {
                user.FirstDayClicked = true;
                db.SaveChanges();
            }

            return new ResultDTO() { success = true };
        }

        /// <summary>
        /// 用户在App首页看到过了首日入金奖励的通知
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("firstday/rewarded")]
        [BasicAuth]
        public ResultDTO FirstDayRewarded()
        {
            var user = GetUser();
            if (user.FirstDayRewarded.HasValue && user.FirstDayRewarded.Value)
            {
                user.FirstDayRewarded = false;
                db.SaveChanges();
            }

            return new ResultDTO() { success = true };
        }

        [HttpGet]
        [Route("summary")]
        [BasicAuth]
        public DailySignInfoDTO GetTodaySignInfo()
        {
            DailySignInfoDTO info = new DailySignInfoDTO();

            var reward = GetTotalUnpaidReward();
            info.TotalUnpaidAmount = reward.demoRegister + reward.totalDailySign + reward.totalDemoTransaction;

            info.TotalSignDays = db.DailySigns.Count(item => item.UserId == this.UserId);

            DailySign lastDailySign = db.DailySigns
                .Where(d => d.UserId == this.UserId)
                .OrderByDescending(d => d.Date)
                .FirstOrDefault();

            var chinaToday = DateTimes.GetChinaNow().Date;

            if (lastDailySign == null)
            {
                info.AmountToday = RewardService.CHECK_IN_DAY_1_TO_5;
            }
            else if (!lastDailySign.Date.HasValue) //should not happen
            {
                info.AmountToday = RewardService.CHECK_IN_DAY_1_TO_5;
            }
            else
            {
                if (lastDailySign.Date.Value == chinaToday)//already signed in today
                {
                    info.AmountToday = lastDailySign.Amount;
                }
                else if (lastDailySign.Date.Value.AddDays(1) == chinaToday) //continuous sign 
                {
                    info.AmountToday = RewardService.GetRewardAmount(lastDailySign.Continuity + 1);
                }
                else //break before
                {
                    info.AmountToday = RewardService.CHECK_IN_DAY_1_TO_5;
                }
            }

            info.IsSignedToday = lastDailySign != null && lastDailySign.SignAt.Value.Date == chinaToday;

            return info;
        }
    }
}