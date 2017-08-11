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

namespace CFD_JOBS
{
    public class PartnerReport
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
                    //上海的下午2点、UTC上午6点作为发送时间
                    int timeToSendHour = 6;
                    try
                    {
                        using (var db = CFDEntities.Create())
                        {
                            timeToSendHour = int.Parse(db.Miscs.FirstOrDefault(o => o.Key == "PartnerMailTime").Value);
                        }
                    }
                    catch
                    {
                        Console.WriteLine("读取发送时间失败");
                    }

                    var timeToSend = DateTime.SpecifyKind(new DateTime(start.Year, start.Month, start.Day, timeToSendHour, 0, 0), DateTimeKind.Utc);
                    if (start < timeToSend && end >= timeToSend)
                    {
                        Console.WriteLine("开始读取");
                        string fileName = string.Empty;

                        ExcelExport excel = new ExcelExport();
                        List<PartnerExportItem> exporItems = new List<PartnerExportItem>();
                        string partnerMailSetting = string.Empty;
                        using (var db = CFDEntities.Create())
                        {
                            //找出24小时内注册的合作伙伴
                            DateTime yesterDay = timeToSend.AddDays(-1);
                            var partners = db.Partners.Where(p => p.CreatedAt >= yesterDay).ToList();

                            partners.ForEach(p =>
                            {
                                exporItems.Add(new PartnerExportItem() {
                                     Email = p.Email,
                                      Name = p.Name,
                                       PartnerCode = p.PartnerCode,
                                        Phone = p.Phone
                                });
                            });
                           
                            partnerMailSetting = db.Miscs.FirstOrDefault(o => o.Key == "PartnerMail").Value;
                        }
                        excel.PartnerExportItems = exporItems;
                        if (exporItems.Count > 0)
                        {
                            fileName = "Partner/" + timeToSend.ToString("yyyy-MM-dd") + ".xls";
                            if (File.Exists(fileName))
                            {
                                File.Delete(fileName);
                            }
                            excel.ExportPartner(fileName);
                        }

                        BaseEmailHelper helper = new Mail163Helper();
                        if (!helper.Send(fileName, partnerMailSetting, "Daily Partner Report"))
                        {
                            helper = new MailQQHelper();
                            helper.Send(fileName, partnerMailSetting, "Daily Partner Report");
                        }
                        //SendMail(fileName, partnerMailSetting);
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
            Console.WriteLine("发送邮件");
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

                if (!string.IsNullOrEmpty(fileName))
                {
                    var attach = new Attachment(fileName);
                    mm.Attachments.Add(attach);
                }

                mm.Subject = "Daily Partner Report";
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
