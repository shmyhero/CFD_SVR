using CFD_COMMON;
using CFD_COMMON.Models.Context;
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

namespace CFD_JOBS
{
    /// <summary>
    /// 首日入金奖励金报表
    /// </summary>
    public class DepositRewardReport
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

                        SendMail(fileName, refundMailSetting);
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
