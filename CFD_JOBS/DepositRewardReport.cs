using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CFD_COMMON.Utils;

namespace CFD_JOBS
{
    /// <summary>
    /// 首日入金奖励金报表
    /// </summary>
    public class DepositRewardReport
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
        private const string PingSucceed = "charge.succeeded";

        public static void Run()
        {
            while (true)
            {
                try
                {
                    var start = DateTime.UtcNow;
                    var end = DateTime.UtcNow.AddMinutes(5);
                    //上海的下午5点、UTC上午9点作为发送时间
                    int timeToSendHour = 9;
                    try
                    {
                        using (var db = CFDEntities.Create())
                        {
                            timeToSendHour = int.Parse(db.Miscs.FirstOrDefault(o => o.Key == "RefundMailTime").Value);
                        }
                    }
                    catch
                    {
                        Console.WriteLine("读取发送时间失败");
                    }

                    var timeToSend = DateTime.SpecifyKind(new DateTime(start.Year, start.Month, start.Day, timeToSendHour, 0, 0), DateTimeKind.Utc);
                    if (start < timeToSend && end >= timeToSend)
                    {
                        string fileName = string.Empty;
                       
                        ExcelExport excel = new ExcelExport();
                        List<RewardExportItem> exporItems = new List<RewardExportItem>();
                        string refundMailSetting = string.Empty;
                        using (var db = CFDEntities.Create())
                        {
                            #region 找出符合首日入金条件的用户，把结果加入DepositReward表
                            RewardService rewardSvc = new RewardService(db);

                            var utc2DaysAgo = DateTime.UtcNow.AddDays(-2);
                            var utc1DayAgo = DateTime.UtcNow.AddDays(-1);
                            //找到所有首次入金时间大于2天前小于1天前的记录
                            var firtDayDepositUsers = (from u in db.Users
                                                       join a in db.AyondoTransferHistory_Live.Where(Transfer.IsDeposit()) on u.AyLiveAccountId equals a.TradingAccountId
                                                       where u.AyLiveAccountId != null //&& Transfer.DepositTypes.Contains(a.TransferType)
                                                       group new { u.Id, a.ApprovalTime } by a.TradingAccountId into g
                                                       let minApproval = g.Min(a => a.ApprovalTime)
                                                       where minApproval > utc2DaysAgo && minApproval < utc1DayAgo
                                                       select new
                                                       {
                                                           g.Key,
                                                           UserID = g.Min(a => a.Id),
                                                           ApprovalTime = g.Min(a => a.ApprovalTime)
                                                       }).Union(
                                                        from u in db.Users
                                                        join p in db.PingOrders on u.Id equals p.UserId
                                                        where u.AyLiveAccountId != null && p.WebHookAt.HasValue && p.WebHookResult == PingSucceed
                                                        group new { u.Id, p.WebHookAt } by u.AyLiveAccountId into g
                                                        let minApproval = g.Min(p => p.WebHookAt)
                                                        where minApproval > utc2DaysAgo && minApproval < utc1DayAgo
                                                        select new
                                                        {
                                                            g.Key,
                                                            UserID = g.Min(a => a.Id),
                                                            ApprovalTime = g.Min(p => p.WebHookAt)
                                                        }
                                                       ).ToList();
                            //遍历这些用户，计算其交易金并保存
                            firtDayDepositUsers.ForEach(u =>
                            {
                                try
                                {
                                    var firstDayEndTime = u.ApprovalTime.Value.AddDays(1);
                                    decimal firstDayDepositAmount = 0;
                                    //因为有两张入金表，存在一个表里是首日，另一个不是首日的情况。因此这里要再去两张表核对一下
                                    bool ayondoDepositBefore = db.AyondoTransferHistory_Live.Where(Transfer.IsDeposit())
                                            .Any(a => //Transfer.DepositTypes.Contains(a.TransferType) && 
                                                a.TradingAccountId == u.Key && a.ApprovalTime < u.ApprovalTime);
                                    if(ayondoDepositBefore)
                                    {
                                        return;
                                    }
                                    bool pingDepositBefore = db.PingOrders.Any(p => p.UserId == u.UserID && p.WebHookResult == PingSucceed && p.WebHookAt < u.ApprovalTime);
                                    if(pingDepositBefore)
                                    {
                                        return;
                                    }

                                    //通过Adyen - Skrill，WeCollect的入金
                                    var ayondoTransferDeposits = db.AyondoTransferHistory_Live.Where(Transfer.IsDeposit())
                                    .Where(a => a.TradingAccountId == u.Key && a.ApprovalTime <= firstDayEndTime //&& Transfer.DepositTypes.Contains(a.TransferType)
                                    ).ToList();//.Sum(a => a.Amount);
                                    if(ayondoTransferDeposits!=null)
                                    {
                                        firstDayDepositAmount = ayondoTransferDeposits.Sum(a => a.Amount).Value;
                                    }
                                    ////通过Ping++的入金
                                    //var pingDeposits = db.PingOrders.Where(p => p.UserId == u.UserID && p.WebHookAt.HasValue && p.WebHookResult == PingSucceed && p.WebHookAt <= firstDayEndTime).ToList();
                                    //if (pingDeposits != null)
                                    //{
                                    //    firstDayDepositAmount = pingDeposits.Sum(p => p.AmountUSD).Value;
                                    //}

                                    decimal rewardAmount = firstDayDepositAmount * rewardSvc.GetFirstDayRewadRate(firstDayDepositAmount);
                                    rewardAmount = rewardAmount > 10000 ? 10000 : rewardAmount;
                                    rewardAmount = rewardAmount * RewardService.ExchangeRate;

                                    if (rewardAmount > 0)
                                    {
                                        Message_Live msg1stDayDeposit = new Message_Live();
                                        msg1stDayDeposit.UserId = u.UserID;
                                        msg1stDayDeposit.Title = "首日入金赠金";
                                        msg1stDayDeposit.Body = string.Format("您的首日入金赠金{0}元已自动转入您的交易金账号", rewardAmount);
                                        msg1stDayDeposit.CreatedAt = DateTime.UtcNow;
                                        msg1stDayDeposit.IsReaded = false;
                                        db.Message_Live.Add(msg1stDayDeposit);

                                        DepositReward dr = new DepositReward();
                                        dr.Amount = rewardAmount;
                                        dr.UserId = u.UserID;
                                        dr.DepositAmount = firstDayDepositAmount;
                                        dr.CreatedAt = DateTime.Now;
                                        db.DepositRewards.Add(dr);

                                        var user = db.Users.FirstOrDefault(u1 => u1.Id == u.UserID);
                                        if (!user.FirstDayRewarded.HasValue) //App首页提示用户拿到首日交易金。 Null未拿到，False已看过此消息，True已拿到交易金未看过消息
                                        {
                                            user.FirstDayRewarded = true;
                                        }

                                        db.SaveChanges();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                               
                            });


                            #endregion

                            DateTime yesterDay = timeToSend.AddDays(-1);
                            exporItems = (from dr in db.DepositRewards
                                          join u in db.Users on dr.UserId equals u.Id
                                          join ui in db.UserInfos on u.Id equals ui.UserId
                                          where dr.CreatedAt > yesterDay && dr.CreatedAt <= timeToSend
                                          select new RewardExportItem()
                                          {
                                              RealName = ui.LastName + ui.FirstName,
                                              AccountName = u.AyLiveUsername,
                                              DepositAmount = dr.DepositAmount.HasValue ? dr.DepositAmount.Value : 0,
                                              RewardAmount = dr.Amount,
                                              DepositAt = dr.CreatedAt.HasValue ? dr.CreatedAt.Value : DateTime.MinValue
                                          }).ToList();

                            refundMailSetting = db.Miscs.FirstOrDefault(o => o.Key == "RefundMail").Value;
                        }
                        excel.DepositRewardExportItems = exporItems;
                        if(exporItems.Count > 0)
                        {
                            fileName = "Reward/" + timeToSend.ToString("yyyy-MM-dd") + ".xls";
                            if (File.Exists(fileName))
                            {
                                File.Delete(fileName);
                            }
                            excel.ExportDepositReward(fileName);
                        }

                        //SendMail(fileName, refundMailSetting);
                        BaseEmailHelper helper = new Mail163Helper();
                        if(!helper.Send(fileName, refundMailSetting, "First Day Deposit Reward Report"))
                        {
                            helper = new MailQQHelper();
                            helper.Send(fileName, refundMailSetting, "First Day Deposit Reward Report");
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

        public static void SendMail(string fileName, string refundSetting)
        {
            string from = JObject.Parse(refundSetting)["from"].Value<string>();
            string to = JObject.Parse(refundSetting)["to"].Value<string>();
            string cc = JObject.Parse(refundSetting)["cc"].Value<string>();
            string smtp = JObject.Parse(refundSetting)["smtp"].Value<string>();
            string account = JObject.Parse(refundSetting)["account"].Value<string>();
            string password = JObject.Parse(refundSetting)["password"].Value<string>();

            try
            {
                MailMessage mm = new MailMessage();
                mm.From = new MailAddress(from);
                if (!string.IsNullOrEmpty(to))
                {
                    foreach (string t in to.Split(';'))
                    {
                        mm.To.Add(t);
                    }
                }

                if (!string.IsNullOrEmpty(cc))
                {
                    foreach (string c in cc.Split(';'))
                    {
                        mm.CC.Add(c);
                    }
                }

                if(!string.IsNullOrEmpty(fileName))
                {
                    var attach = new Attachment(fileName);
                    mm.Attachments.Add(attach);
                }

                mm.Subject = "First Day Deposit Reward Report";
                if (!string.IsNullOrEmpty(fileName))
                    mm.Body = "Please find the enclosed. This mail is sent automatically. ";
                else
                    mm.Body = "No items found";
                SmtpClient sc = new SmtpClient(smtp);
                sc.Credentials = new NetworkCredential(account, password);

                sc.Send(mm);
            }
            catch (Exception ex)
            {
                CFDGlobal.LogException(ex);
            }
        }
    }

}
