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
    public class PingDepositReport
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
                    //英国时间14点，上海22点
                    int timeToSendHour = 14;
                    try
                    {
                        using (var db = CFDEntities.Create())
                        {
                            timeToSendHour = int.Parse(db.Miscs.FirstOrDefault(o => o.Key == "PingDepositReport").Value);
                        }
                    }
                    catch
                    {
                        Console.WriteLine("读取发送时间失败");
                    }

                    var timeToSend = DateTime.SpecifyKind(new DateTime(start.Year, start.Month, start.Day, timeToSendHour, 0, 0), DateTimeKind.Utc);
                    if (start < timeToSend && end >= timeToSend)
                    {
                        string fileName = "Ping/" + timeToSend.ToString("yyyy-MM-dd") + ".xls";
                        if (File.Exists(fileName))
                        {
                            File.Delete(fileName);
                        }
                        ExcelExport excel = new ExcelExport();
                        List<PingDepositExportItem> exporItems = new List<PingDepositExportItem>();
                        string refundMailSetting = string.Empty;
                        using (var db = CFDEntities.Create())
                        {
                            DateTime yesterday = timeToSend.AddDays(-1);
                            exporItems = (from p in db.PingOrders
                                          join u in db.Users on p.UserId equals u.Id
                                          where p.AmountCNY.HasValue && p.FxRate.HasValue && p.FxRateAt.HasValue && p.WebHookAt.HasValue && p.WebHookAt > yesterday && p.WebHookAt <= timeToSend && p.WebHookResult == "charge.succeeded"
                                          select new PingDepositExportItem()
                                          {
                                              Account = u.AyLiveAccountId.Value,
                                              AmountCNY = p.AmountCNY.Value,
                                              AmountUSD =  Math.Round(p.AmountCNY.Value / p.FxRate.Value,2),
                                               FxRate = p.FxRate.Value,
                                                DepositTime = p.WebHookAt.Value,
                                                 UserName = u.AyLiveUsername
                                          }).ToList();

                            refundMailSetting = db.Miscs.FirstOrDefault(o => o.Key == "RefundMail").Value;
                        }
                        excel.PingDepositExportItems = exporItems;
                        excel.ExportPingDeposit(fileName);
                        //SendMail(fileName, refundMailSetting);
                        BaseEmailHelper helper = new Mail163Helper();
                        if (!helper.Send(fileName, refundMailSetting, "Daily Deposit Report"))
                        {
                            helper = new MailQQHelper();
                            helper.Send(fileName, refundMailSetting, "Daily Deposit Report");
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
