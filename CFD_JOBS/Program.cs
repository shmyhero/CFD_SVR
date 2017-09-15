using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using CFD_COMMON;
using CFD_COMMON.Utils.Extensions;
using CFD_JOBS.Ayondo;
using Elmah;

namespace CFD_JOBS
{
    class Program
    {
        static string exeDir;
        static string exeName;
        static string fullArgs;

        static string logDir;
        static string logFullPath;
        static string logTimeStamp;

        static int Main(string[] args)
        {
            GetProgramVariables(args);
            CreateLogDirectory();

            SetupLogger();

            Environment.CurrentDirectory = exeDir; // set curdir so local ./caches works when running in SQL Agent

            //bool isMonitoring = args.Contains("-Monitor") && args.Length == 1;
            DateTime startupTime = DateTime.Now;
            bool completedWithoutError = true;

            LogJobStartup();//(db, isMonitoring);

            try
            {
                if (args.Contains("-AyondoFixFeed"))
                {
                    AyondoFixFeedWorker.Run();
                }
                if (args.Contains("-AyondoFixFeed_Live"))
                {
                    AyondoFixFeedWorker.Run(true);
                }

                if (args.Contains("-AyondoFixTrade"))
                {
                    AyondoFixTradeWorker.Run();
                }
                if (args.Contains("-AyondoFixTradeClient"))
                {
                    AyondoFixTradeClient.Run();
                }
                if (args.Contains("-AyondoFixTest"))
                {
                    AyondoFixTestWorker.Run();
                }

                if (args.Contains("-AyondoDataImport"))
                {
                    AyondoDataImportWorker.Run();
                }

                if (args.Contains("-AyondoOldProdCleanup"))
                {
                    AyondoOldProdCleanup.Run();
                }

                if (args.Contains("-AyondoTradeHistory"))
                {
                    AyondoTradeHistoryImport.Run();
                }
                if (args.Contains("-AyondoTradeHistory_Live"))
                {
                    AyondoTradeHistoryImport.Run(true);
                }

                if (args.Contains("-AyondoTransferHistory"))
                {
                    AyondoTransferHistoryImport.Run();
                }
                if (args.Contains("-AyondoTransferHistory_Live"))
                {
                    AyondoTransferHistoryImport.Run(true);
                }

                if (args.Contains("-Competition"))
                {
                    Competition2.Run();
                }

                if (args.Contains("-LoadTest"))
                {
                    var loadTest=new LoadTest();
                    loadTest.Run();
                }

                if (args.Contains("-RedisToDb"))
                {
                    RedisToDbWorker.Run();
                }

                if (args.Contains("-StockAlert"))
                {
                    StockAlert.Run();
                }
                if (args.Contains("-StockAlert_Live"))
                {
                    StockAlert.Run(true);
                }

                if (args.Contains("-TickChart"))
                {
                    TickChartWorker.Run();
                }
                if (args.Contains("-TickChart_Live"))
                {
                    TickChartWorker.Run(true);
                }

                if (args.Contains("-RemittanceReport"))
                {
                    RemittanceReport.Run();
                }

                if (args.Contains("-RewardReport"))
                {
                    DepositRewardReport.Run();
                }

                if (args.Contains("-PartnerReport"))
                {
                    PartnerReport.Run();
                }

                if (args.Contains("-QuoteSnap"))
                {
                    QuoteSnapshot.Run(false);
                }

                if (args.Contains("-QuoteSnap_Live"))
                {
                    QuoteSnapshot.Run(true);
                }

                if (args.Contains("-Rank_Live"))
                {
                    RankJob.Run(true);
                }

                if (args.Contains("-Prize"))
                {
                    PrizeReport.Run();
                }

                if (args.Contains("-Ping"))
                {
                    PingDepositReport.Run();
                }
                if (args.Contains("-PingPPCashTransfer"))
                {
                    PingPPCashTransfer.Run();
                }
                return 0;
            }
            catch (Exception ex)
            {
                CFDGlobal.LogLine("EXCEPTION MAIN THREAD ### [" + ex.GetType().ToString() + "] " + ex.Message + " ###");
                CFDGlobal.LogException(ex);
                completedWithoutError = false;
                return 1;
            }
            finally
            {
                TimeSpan execTime = DateTime.Now.Subtract(startupTime);

                //// wait for child foreground threads to end
                //foreach (Thread t in thc.Global.ForegroundThreads)
                //{
                //    //Global.LogLine("joining thread '" + t.Name + "'...");
                //    t.Join();
                //}
                //JobsGlobal.LogLine("all foreground threads join-completed.");

                //if (!isMonitoring)
                //{
                    execTime = LogEndOfJob(execTime);
                //}

                Trace.Close();
                if (completedWithoutError)
                    File.Move(logFullPath, logFullPath.Replace("INPROCESS.", "OK.") + "_" + execTime.TotalMinutes.ToString("0") + ".MINS.LOG");
                else
                    File.Move(logFullPath, logFullPath.Replace("INPROCESS.", "FAILED.") + "_" + execTime.TotalMinutes.ToString("0") + ".MINS.LOG");
            }
        }

        private static void GetProgramVariables(string[] args)
        {
            Assembly exe = System.Reflection.Assembly.GetExecutingAssembly();
            string exeLocation = exe.Location;
            exeDir = System.IO.Path.GetDirectoryName(exeLocation);
            exeName = exe.ManifestModule.Name.ToUpper().Replace(".EXE", "");
            fullArgs = string.Concat(args);
        }

        private static void CreateLogDirectory()
        {
            logDir = exeDir + "\\LOG" + fullArgs.Replace(":", "");
            logDir = logDir.Trim(Path.GetInvalidFileNameChars());
            logDir = logDir.Trim(Path.GetInvalidPathChars());
            logDir = logDir.TruncateMax(150);// windows limitation - yay
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
        }

        private static void SetupLogger()
        {
            logTimeStamp = DateTime.Now.ToFileTime().ToString();
            logFullPath = logDir + "\\INPROCESS.start." + DateTime.Now.ToString("dd.MMM.HH.mm") 
                //+ "_" + "DB." + dbName
                + "_" + exeName + "." + /*fullArgs + "." +*/ logTimeStamp + ".log";
            TextWriterTraceListener twtl = new TextWriterTraceListener(logFullPath);
            Trace.Listeners.Add(twtl);
            TextWriterTraceListener twtl2 = new TextWriterTraceListener(Console.Out);
            Trace.Listeners.Add(twtl2);
            Trace.AutoFlush = true;
        }

        private static void LogJobStartup()//(tradeheroEntities db, bool isMonitoring)
        {
            //if (!isMonitoring)
            //{
                CFDGlobal.LogLine("***************************************************");
                CFDGlobal.LogLine("*** TH_JOBS STARTUP - (local) " + DateTime.Now.ToString("dd MMM yyyy HH:mm") + " ***");
                CFDGlobal.LogLine("***************************************************");
                CFDGlobal.LogLine("-");
                CFDGlobal.LogLine("this host: " + System.Environment.MachineName);
                //CFDGlobal.LogLine("constr: " + db.Database.Connection.ConnectionString);
                CFDGlobal.LogLine("curdir: " + Environment.CurrentDirectory);
                CFDGlobal.LogLine("fullArgs: " + fullArgs);
                CFDGlobal.LogLine("logDir: " + logDir);
                CFDGlobal.LogLine("-");
            //}
        }

        private static TimeSpan LogEndOfJob(TimeSpan execTime)
        {
            CFDGlobal.LogLine("-");
            CFDGlobal.LogLine("................................................................................");
            CFDGlobal.LogLine("   TH_JOBS FINISHED: @ (local) " + DateTime.Now.ToString("dd MMM yyyy HH:mm") + ", execTime was " + execTime.TotalMinutes.ToString("000.00") + " min(s) ...");
            CFDGlobal.LogLine("          (fullArgs: " + fullArgs + ")");
            CFDGlobal.LogLine("................................................................................");
            CFDGlobal.LogLine("-");
            return execTime;
        }
    }

    public class ElmahLogForJOB
    {
        public static ConcurrentDictionary<string,DateTime> ErrorMailExMessages = new ConcurrentDictionary<string, DateTime>();  

        public static void Log(Exception e, bool sendMail = true)
        {
            ErrorLog errorLog = ErrorLog.GetDefault(null);
            errorLog.ApplicationName = "/LM/W3SVC/1273337584/ROOT";
            errorLog.Log(new Error(e));

            if (sendMail)
            {
                DateTime dtLastSent = DateTime.MinValue;
                var tryGetValue = ErrorMailExMessages.TryGetValue(e.Message, out dtLastSent);

                if (!tryGetValue || DateTime.UtcNow - dtLastSent > TimeSpan.FromHours(1))
                {
                    try
                    {
                        var mail = new ElmahMailForJOB();
                        mail.Log(new Error(e));
                    }
                    catch (Exception exMail)
                    {
                        CFDGlobal.LogException(exMail);
                    }

                    ErrorMailExMessages.AddOrUpdate(e.Message, DateTime.UtcNow, (k, v) => DateTime.UtcNow);
                }
            }
        }
    }

    public class ElmahMailForJOB : ErrorMailModule
    {
        public ElmahMailForJOB()
        {
            //this basically just gets config from errorMail  (app.config)
            base.OnInit(new HttpApplication());
        }
        public void Log(Error error)
        {
            //just send the email pls
            base.ReportError(error);
        }
    }
}
