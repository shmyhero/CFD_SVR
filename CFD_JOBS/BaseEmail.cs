using CFD_COMMON;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace CFD_JOBS
{
    public abstract class BaseEmailHelper
    {
        public abstract bool Send(string fileName, string refundSetting, string subject);
    }

    public class Mail163Helper : BaseEmailHelper
    {
        public override bool Send(string fileName, string refundSetting, string subject)
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

                if (!string.IsNullOrEmpty(fileName))
                {
                    var attach = new Attachment(fileName);
                    mm.Attachments.Add(attach);
                }

                mm.Subject = subject;
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
                Console.WriteLine("163 Mail Server Failed");
                CFDGlobal.LogException(ex);
                return false;
            }

            Console.WriteLine("Successful with 163");
            return true;
        }
    }

    public class MailQQHelper : BaseEmailHelper
    {
        public override bool Send(string fileName, string refundSetting, string subject)
        {
            string from = "992990831@qq.com";
            string to = JObject.Parse(refundSetting)["to"].Value<string>();
            string cc = JObject.Parse(refundSetting)["cc"].Value<string>();
            string smtp = "smtp.qq.com";
            string account = "992990831@qq.com";
            string password = "yysiuzzabvihbfdb";

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
                mm.Subject = subject;
                if (!string.IsNullOrEmpty(fileName))
                    mm.Body = "Please find the enclosed. This mail is sent automatically. ";
                else
                    mm.Body = "No items found";
                SmtpClient sc = new SmtpClient(smtp);
                sc.Credentials = new NetworkCredential(account, password);
                sc.Port = 587;
                sc.EnableSsl = true;//启用SSL加密  
                sc.Send(mm);
            }
            catch (Exception ex)
            {
                Console.WriteLine("QQ Mail Server Failed");
                CFDGlobal.LogException(ex);
                return false;
            }

            Console.WriteLine("Successful with QQ");

            return true;
        }
    }

}
