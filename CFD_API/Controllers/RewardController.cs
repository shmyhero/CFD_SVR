using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using ServiceStack.Redis;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/reward")]
    public class RewardController : CFDController
    {
        public RewardController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
            : base(db, mapper, redisClient)
        {
        }
        
        [HttpGet]
        [Route("checkIn/share")]
        public RewardIntroDTO GetDailySignRewardIntro()
        {
            return new RewardIntroDTO()
            {
                url = "http://cn.tradehero.mobi/TH_CFD_WEB/checkinRule.php",
                imgUrl = "http://cn.tradehero.mobi/TH_CFD_WEB/images/ShareLogo.png",
                title = "签到赚取实盘资金",
                text = "模拟注册可赚取" + CFDGlobal.REWARD_DEMO_REG + "元；每日签到可赚取" + CFDGlobal.REWARD_DAY_1_TO_5 + "元；每日模拟交易可赚取" + CFDGlobal.REWARD_DEMO_TRADE + "元。",
            };
        }

        [HttpGet]
        [Route("demoReg/share")]
        public RewardIntroDTO GetDemoRegRewardIntro()
        {
            return new RewardIntroDTO()
            {
                url = "http://cn.tradehero.mobi/TH_CFD_WEB/checkinRule.php",
                imgUrl = "http://cn.tradehero.mobi/TH_CFD_WEB/images/ShareLogo.png",
                title = "模拟注册获得"+CFDGlobal.REWARD_DEMO_REG+"元交易金",
                text = "模拟注册可赚取" + CFDGlobal.REWARD_DEMO_REG + "元；每日签到可赚取" + CFDGlobal.REWARD_DAY_1_TO_5 + "元；每日模拟交易可赚取" + CFDGlobal.REWARD_DEMO_TRADE + "元。",
            };
        }

        [HttpGet]
        [Route("checkIn")]
        [BasicAuth]
        public ResultDTO DailySign()
        {
            ResultDTO result = new ResultDTO() { success = true };
            DailySign lastDailySign = db.DailySigns.Where(d => d.UserId == this.UserId).OrderByDescending(d => d.Date).FirstOrDefault();

            var chinaNow = DateTimes.GetChinaNow();

            DailySign todayDailySign = new DailySign();
            todayDailySign.UserId = this.UserId;
            todayDailySign.SignAt = chinaNow;
            todayDailySign.Date = chinaNow.Date;
            todayDailySign.IsPaid = false;

            if (lastDailySign == null) //first time sign in
            {
                todayDailySign.Continuity = 1;
                todayDailySign.Amount = CFDGlobal.REWARD_DAY_1_TO_5;
            }
            else //signed in before
            {
                if (!lastDailySign.Date.HasValue)//should not happen. if happened, continue from day 1
                {
                    todayDailySign.Continuity = 1;
                    todayDailySign.Amount = CFDGlobal.REWARD_DAY_1_TO_5;
                }
                else
                {
                    if (lastDailySign.Date.Value == chinaNow.Date)//already signed in today
                    {
                        result = new ResultDTO() { success = false, message = "Already sign in today." };
                        return result;
                    }
                    else if (lastDailySign.Date.Value.AddDays(1) == chinaNow.Date) //continuous sign 
                    {
                        todayDailySign.Continuity = lastDailySign.Continuity + 1;
                        if (todayDailySign.Continuity <= 5)
                        {
                            todayDailySign.Amount = CFDGlobal.REWARD_DAY_1_TO_5;
                        }
                        else if (todayDailySign.Continuity <= 10)
                        {
                            todayDailySign.Amount = CFDGlobal.REWARD_DAY_6_TO_10;
                        }
                        else
                        {
                            todayDailySign.Amount = CFDGlobal.REWARD_DAY_11_TO_X;
                        }
                    }
                    else //break before
                    {
                        todayDailySign.Continuity = 1;
                        todayDailySign.Amount = CFDGlobal.REWARD_DAY_1_TO_5;
                    }
                }
            }

            db.DailySigns.Add(todayDailySign);
            db.SaveChanges();

            return result;
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
            //reward for daily sign
            decimal totalDailySignReward = db.DailySigns
                .Where(o => o.UserId == UserId && !o.IsPaid.Value)
                .Select(o => o.Amount).DefaultIfEmpty(0).Sum();
            //List <DailySign> dailySignList = db.DailySigns.Where(item => item.UserId == this.UserId && item.IsPaid.HasValue && !item.IsPaid.Value).ToList();
            //if(dailySignList != null && dailySignList.Count > 0)
            //{
            //    totalDailySignReward = dailySignList.Sum(item => item.Amount);
            //}

            //reward for daily demo trasaction
            var totalDemoTransactionReward = db.DailyTransactions
                .Where(o => o.UserId == UserId && !o.IsPaid.Value)
                .Select(o => o.Amount).DefaultIfEmpty(0).Sum();
            //List<DailyTransaction> dailyTransactionsList = db.DailyTransactions.Where(item => item.UserId == this.UserId && item.IsPaid.HasValue && !item.IsPaid.Value).ToList();
            //if(dailyTransactionsList.Count > 0)
            //{
            //    totalDemoTransactionReward = dailyTransactionsList.Sum(item => item.Amount);
            //}

            //reward for demo register
            var reward = db.DemoRegisterRewards.FirstOrDefault(o => o.UserId == UserId);
            decimal demoRegisterReward = reward == null ? 0 : reward.Amount;

            return new RewardDTO() { demoRegister = demoRegisterReward, totalDailySign = totalDailySignReward, totalDemoTransaction = totalDemoTransactionReward }; //totalDailySignReward + totalDemoTransactionReward + demoRegisterReward;
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
                info.AmountToday = CFDGlobal.REWARD_DAY_1_TO_5;
            }
            else if (!lastDailySign.Date.HasValue) //should not happen
            {
                info.AmountToday = CFDGlobal.REWARD_DAY_1_TO_5;
            }
            else
            {
                if (lastDailySign.Date.Value == chinaToday)//already signed in today
                {
                    info.AmountToday = lastDailySign.Amount;
                }
                else if (lastDailySign.Date.Value.AddDays(1) == chinaToday) //continuous sign 
                {
                    if (lastDailySign.Continuity <= 4)
                    {
                        info.AmountToday = CFDGlobal.REWARD_DAY_1_TO_5;
                    }
                    else if (lastDailySign.Continuity <= 9)
                    {
                        info.AmountToday = CFDGlobal.REWARD_DAY_6_TO_10;
                    }
                    else
                    {
                        info.AmountToday = CFDGlobal.REWARD_DAY_11_TO_X;
                    }
                }
                else //break before
                {
                    info.AmountToday = CFDGlobal.REWARD_DAY_1_TO_5;
                }
            }

            info.IsSignedToday = lastDailySign != null && lastDailySign.SignAt.Value.Date == chinaToday;

            return info;
        }
    }
}