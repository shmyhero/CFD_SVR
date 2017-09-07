using CFD_COMMON;
using CFD_COMMON.Models.Context;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CFD_JOBS
{
    /// <summary>
    /// 抽奖邮件
    /// </summary>
    public class PrizeReport
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
                    //上海的下午1点、UTC上午5点作为发送时间
                    int timeToSendHour = 5;
                    try
                    {
                        using (var db = CFDEntities.Create())
                        {
                            timeToSendHour = int.Parse(db.Miscs.FirstOrDefault(o => o.Key == "PrizeMailTime").Value);
                        }
                    }
                    catch
                    {
                        Console.WriteLine("读取发送时间失败");
                    }

                    var timeToSend = DateTime.SpecifyKind(new DateTime(start.Year, start.Month, start.Day, timeToSendHour, 0, 0), DateTimeKind.Utc);
                    if (start < timeToSend && end >= timeToSend)
                    {
                        string fileName = "Prize/" + timeToSend.ToString("yyyy-MM-dd") + ".xls";
                        if (File.Exists(fileName))
                        {
                            File.Delete(fileName);
                        }
                        ExcelExport excel = new ExcelExport();
                        List<PrizeExportItem> exporItems = new List<PrizeExportItem>();
                        string refundMailSetting = string.Empty;
                        using (var db = CFDEntities.Create())
                        {
                            DateTime yesterday = timeToSend.AddDays(-1);
                            exporItems = (from s in db.ScoreConsumptionHistorys
                                          join u in db.Users on s.UserID equals u.Id
                                          where s.CreatedAt > yesterday && s.CreatedAt <= timeToSend
                                          select new PrizeExportItem()
                                          {
                                              PrizeName = s.PrizeName,
                                               ContactPhone = u.Phone,
                                                DeliverAddress = u.DeliveryAddress,
                                                DeliverPhone = u.DeliveryPhone
                                          }).ToList();

                            refundMailSetting = db.Miscs.FirstOrDefault(o => o.Key == "RefundMail").Value;
                        }
                        excel.PrizeExportItems = exporItems;
                        excel.ExportPrize(fileName);
                        //SendMail(fileName, refundMailSetting);
                        BaseEmailHelper helper = new Mail163Helper();
                        if (!helper.Send(fileName, refundMailSetting, "Daily Prize Report"))
                        {
                            helper = new MailQQHelper();
                            helper.Send(fileName, refundMailSetting, "Daily Prize Report");
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
    }
}
