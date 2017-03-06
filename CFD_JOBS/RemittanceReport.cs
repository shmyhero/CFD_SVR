using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using ServiceStack.Text;
using System.Threading.Tasks;
using AutoMapper;
using CFD_COMMON.Utils;
using CFD_COMMON.Utils.Extensions;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using System.Data.SqlTypes;
using System.Text;
using System.IO;
using System.Data.OleDb;
using System.Net.Mail;
using Newtonsoft.Json.Linq;

namespace CFD_JOBS
{
    public class RemittanceReport
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
                        string fileName = "Remittance/" + timeToSend.ToString("yyyy-MM-dd") + ".xls";
                        if (File.Exists(fileName))
                        {
                            File.Delete(fileName);
                        }
                        ExcelExport excel = new ExcelExport();
                        List<ExportItem> exporItems = new List<ExportItem>();
                        string refundMailSetting = string.Empty;
                        using (var db = CFDEntities.Create())
                        {
                            DateTime yesterDay = timeToSend.AddDays(-1);
                            exporItems = (from t in db.WithdrawalHistories
                                          join u in db.Users on t.UserId equals u.Id
                                          join u2 in db.UserInfos on u.Id equals u2.UserId
                                          where t.CreateAt > yesterDay && t.CreateAt <= timeToSend
                                          select new ExportItem() {
                                              BeneficiaryName = u2.LastName + u2.FirstName,
                                              UserName = u.AyLiveUsername,
                                              BeneficiaryAccountNo = u.BankCardNumber,
                                              Province = u.Province,
                                              City = u.City,
                                              BankName = u.BankName,
                                              BankBranch = u.Branch,
                                              IdCardNo = u2.IdCode,
                                              Amount = t.RequestAmount.HasValue ? t.RequestAmount.Value : 0
                                          }).ToList();

                            refundMailSetting = db.Miscs.FirstOrDefault(o => o.Key == "RefundMail").Value;
                        }
                        excel.ExportItems = exporItems;
                        excel.Export(fileName);
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
                var attach = new Attachment(fileName);

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

                mm.Attachments.Add(attach);
                mm.Subject = "Daily Remittance Report";
                mm.Body = "Please find the enclosed. This mail is sent automatically. ";
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

    class ExportItem
    {
        //持卡人
        public string BeneficiaryName;
        public string UserName;
        public string BeneficiaryAccountNo;
        public string BankName;
        public string BankBranch;
        public string Province;
        public string City;
        public string IdCardNo;
        public string Currency { get { return "USD"; } }
        public decimal Amount;
    }

    abstract class BaseExport
    {
        public List<ExportItem> ExportItems;
        public abstract void Export(string fileName);
    }

    class CSVExport : BaseExport
    {
        public override void Export(string fileName)
        {
            if (ExportItems == null)
            {
                throw new Exception("列为空");
            }

            StringBuilder sb = new StringBuilder();
            //加标题
            sb.Append("Beneficiary Name,Beneficiary Account No.,Bank Name,Bank Branch,Province,City,ID Card No.,Currency,Transaction Amount Received\n");
            ExportItems.ForEach(item => {
                sb.Append(item.BeneficiaryName);
                sb.Append(",");
                sb.Append(item.BeneficiaryAccountNo);
                sb.Append(",");
                sb.Append(item.BankName);
                sb.Append(",");
                sb.Append(item.BankBranch);
                sb.Append(",");
                sb.Append(item.Province);
                sb.Append(",");
                sb.Append(item.City);
                sb.Append(",");
                sb.Append(item.IdCardNo);
                sb.Append(",");
                sb.Append(item.Currency);
                sb.Append(",");
                sb.Append(item.Amount);
                sb.Append("\n");
            });

            File.WriteAllText(fileName, sb.ToString());
        }
    }

    class ExcelExport : BaseExport
    {
        public override void Export(string fileName)
        {
            if (ExportItems == null)
            {
                throw new Exception("列为空");
            }
            //把模板copy一份
            var templateBytes = File.ReadAllBytes("Template/WeCollect_Remittance_Template.xls");
            File.WriteAllBytes(fileName, templateBytes);

            String sConnectionString = string.Format("Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Extended Properties=Excel 8.0;", fileName);
            using (OleDbConnection oleConn = new OleDbConnection(sConnectionString))
            {
                oleConn.Open();
                using (OleDbCommand ole_cmd = oleConn.CreateCommand())
                {
                    ExportItems.ForEach(item =>
                    {
                        ole_cmd.CommandText = string.Format("insert into [Sheet1$] values('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}')", item.BeneficiaryName, item.UserName, item.BeneficiaryAccountNo,item.BankName, item.BankBranch, item.Province, item.City, item.IdCardNo,item.Currency, item.Amount);
                        ole_cmd.ExecuteNonQuery();
                    });
                }
            }

        
        }

    }

}
