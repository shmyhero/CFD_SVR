using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace CFD_COMMON
{
    public class CFDGlobal
    {
        public static string USER_PIC_BLOB_CONTAINER="user-picture";
        public static string USER_PIC_BLOB_CONTAINER_URL = "https://cfdstorage.blob.core.chinacloudapi.cn/" + USER_PIC_BLOB_CONTAINER+"/";

        public static string DATETIME_MASK_MILLI_SECOND = "yyyy-MM-dd HH:mm:ss.fff";

        public static string GetConfigurationSetting(string key)
        {
            if (RoleEnvironment.IsAvailable)
            {
                ////throw exception if not exist
                //return RoleEnvironment.GetConfigurationSettingValue(key);

                var value = CloudConfigurationManager.GetSetting(key);

                //if there's no cloud config, return local config
                return value ?? ConfigurationManager.AppSettings[key];
            }
            else
            {
                return ConfigurationManager.AppSettings[key];
            }
        }

        public static T RetryMaxOrThrow<T>(Func<T> p, int sleepSeconds = 10, int retryMax = 3, bool verboseErrorLog = true)
        {
            int retryCount = 0;
            int currentSleepSeconds = sleepSeconds;
            for (; ; )
            {
                try
                {
                    return p();
                }
                catch (Exception ex)
                {
                    //if (verboseErrorLog)
                    //    Trace.Write("RetryMaxOrThrow: {" + LogAllExceptionsAndStack(ex) + "}... ");

                    //// backoff exponentially when the DB is under load
                    //if (IsDatabaseLoadRelated(ex))
                    //{
                    //    if (verboseErrorLog)
                    //        Trace.Write("DB load exception detected, will back off exponentially... stack: [" + Global.LogStack() + "] / " + Global.LogAllExceptionsAndStack(ex));
                    //    currentSleepSeconds = Math.Min(currentSleepSeconds * 2, 60 * 10); // cap at max 10 minute wait
                    //    retryMax = 5;
                    //}

                    if (++retryCount == retryMax)
                    {
                        //if (verboseErrorLog)
                        //{
                        //    Global.LogLine(
                        //       "### exceeded retry count; throwing: [(type)=" + ex.GetType().Name + ": " + ex.Message + "] " +
                        //       LogAllExceptionsAndStack(ex) +
                        //       " stack: [" + Global.LogStack() + "]");
                        //}

                        throw new System.ApplicationException("exceed retry count: " + ex.Message, ex);
                    }

                    //if (verboseErrorLog)
                    //    Trace.WriteLine("sleep " + currentSleepSeconds + "s...");

                    //#if !DEBUG
                    Thread.Sleep(1000 * currentSleepSeconds);
                    //#endif
                }
            }
        }

        public static void LogError(string message)
        {
            Trace.TraceError(GetLogDatetimePrefix() + message);
        }

        public static void LogLine(string message)
        {
            Trace.WriteLine(GetLogDatetimePrefix() + message);
        }

        public static void LogException(Exception exception)
        {
            var ex = exception;
            while (ex!=null)
            {
                Trace.WriteLine(GetLogDatetimePrefix() + ex.Message);
                Trace.WriteLine(GetLogDatetimePrefix() + ex.StackTrace);

                ex = ex.InnerException;
            }
        }

        private static string GetLogDatetimePrefix()
        {
            return DateTime.Now.ToString(DATETIME_MASK_MILLI_SECOND)+" ";
        }
    }
}
