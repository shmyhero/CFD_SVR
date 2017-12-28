using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using ServiceStack.Redis;
using ServiceStack.Text;

namespace CFD_COMMON
{
    public class CFDGlobal
    {
        public const string CULTURE_CN = "zh-CN";
        public const string CULTURE_EN = "en";

        public static readonly string BLOG_ENDPOINT = GetConfigurationSetting("StorageConnectionString") == null
            ? null
            : CloudStorageAccount.Parse(GetConfigurationSetting("StorageConnectionString")).BlobEndpoint.AbsoluteUri;

        public static string USER_PIC_BLOB_CONTAINER = "user-picture";
        public static string USER_PIC_BLOB_CONTAINER_URL = BLOG_ENDPOINT + USER_PIC_BLOB_CONTAINER+"/";

        public static string HEADLINE_PIC_BLOB_CONTAINER = "headline-img";
        public static string HEADLINE_PIC_BLOB_CONTAINER_URL = BLOG_ENDPOINT + HEADLINE_PIC_BLOB_CONTAINER + "/";

        public static string BANNER_PIC_BLOB_CONTAINER = "banner-img";

        public static string FEEDBACK_PIC_BLOC_CONTAINER = "feedback-img";
        public static string FEEDBACK_PIC_BLOC_CONTAINER_URL = BLOG_ENDPOINT + FEEDBACK_PIC_BLOC_CONTAINER + "/";

        public const string DATETIME_MASK_MILLI_SECOND = "yyyy-MM-dd HH:mm:ss.fff";
        public const string DATETIME_MASK_SECOND = "yyyy-MM-dd HH:mm:ss";

        public const string AYONDO_DATETIME_MASK = "yyyy-MM-dd HH:mm:ss.FFF";

        public static string ASSET_CLASS_STOCK = "Single Stocks";
        public static string ASSET_CLASS_FX = "Currencies";
        public static string ASSET_CLASS_CRYPTO_FX = "Cryptocurrencies";
        public static string ASSET_CLASS_INDEX = "Stock Indices";
        public static string ASSET_CLASS_COMMODITY = "Commodities";

        public static readonly string AMS_HEADER_AUTH = GetConfigurationSetting("AMSHeaderAuth");
        public static readonly string AMS_HOST = GetConfigurationSetting("AMSHost");
        public static readonly string AMS_ORIGIN = GetConfigurationSetting("AMSOrigin");
        public static readonly string AMS_PROXY_HOST = GetConfigurationSetting("AMSProxyHost");
        public const string AMS_CALLBACK_AUTH_TOKEN = "Tj3Id8N7mG6Dyi9Pl1Se4b7dNMik9N0sz1V5sM8cT3we8x9PoqcW3N7dV61cD5J2Ur3Qjf8yTd3EG0UX3";

        public static readonly string TH_WEB_HOST = GetConfigurationSetting("THWebHost");

        public static string OAUTH_TOKEN_PUBLIC_KEY = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAlkG4r0XGWG8DO2043seO
aQnoA426WXTliwAUf9dG7idMyTEcR5jrAY4a2nZtFRj1cstDbxZuSP3Gm2TenDxq
wwPep2eHBsK/7CaS97j1bVu5M0vf1Iu+0qlDWF0SWANcmnAidSQpJsT7qq8XKzcc
wauSosChreJoHdASaeuHN3J3wU9gFCGE08xclorgaKrtbpqS4FkFUQ7UEwjoM1YU
yKPwdieuvwEyfXGCbmbD9uZymiBIIcIxeUasWf667uck6vQMgQTmYNuqi+qkLZIG
hqZlC7NyvDf4xQuKFer4LvZrg6XdakHtLezu7W+ZEx9Vu8UDqQBRUjO3lcCrTfim
BwIDAQAB
-----END PUBLIC KEY-----";

        public static string OAUTH_TOKEN_PUBLIC_KEY_Live = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAjpZnXDpJzWe0nmtFjRrg
Tq1lpGLKLyQ7szBo7NQRH5x3vOob1UpEH/TQ0fpxDQKBaUYAdqlqmmWFX9H5DOtS
xGzjGOUslhpVhXtqwh4gVTFndOYSPDubYwbed2reOzl1oxn2rqP9ww5uynPMbOyf
qvVQulw/EI4E4XW490Hb2YOLghxXN9omIJvNGmEUDXXSR1LwtalvWf8joMZNoH3L
B+cg+7Ook/4zS/atXL1iig1dVkgTRhBcRJvr1tJBwV52raHXTNzYJ9KPU4jvXb2H
57x09NJJ/kfCgJ4gJKTBDnFi0eWydsSgq8fVxbrTW1UeNFyqmGfRWggH7E3XMwy9
1wIDAQAB
-----END PUBLIC KEY-----";

        /// <summary>
        /// the default application-wide BasicRedisClientManager, non-pooled, created for current application
        /// </summary>
        public static IRedisClientsManager BasicRedisClientManager;

        /// <summary>
        /// the default application-wide PooledRedisClientsManager
        /// </summary>
        public static IRedisClientsManager PooledRedisClientsManager;
        public static IRedisClientsManager PooledRedisClientsManager_Live;

        public static string AYONDO_TRADE_SVC_URL = CFDGlobal.GetConfigurationSetting("AyondoTradeSvcUrl");
        public static string AYONDO_TRADE_SVC_URL_Live = CFDGlobal.GetConfigurationSetting("AyondoTradeSvcUrl_Live");

        public static TimeSpan PROD_DEF_ACTIVE_IF_TIME_NOT_OLDER_THAN_TS = TimeSpan.FromDays(14);

        static CFDGlobal()
        {
            JsConfig.TreatEnumAsInteger = true;
            //JsConfig.DateHandler = JsonDateHandler.ISO8601;

            //create default application-wide BasicRedisClientManager
            BasicRedisClientManager = GetNewBasicRedisClientManager();

            PooledRedisClientsManager = GetNewPooledRedisClientManager();
            PooledRedisClientsManager_Live = GetNewPooledRedisClientManager_Live();
        }

        private static IRedisClientsManager GetNewPooledRedisClientManager()
        {
            var redisConStr = CFDGlobal.GetConfigurationSetting("redisConnectionString");

            if (redisConStr == null) return null;

            return new PooledRedisClientManager(100, 2, redisConStr);
        }

        private static IRedisClientsManager GetNewPooledRedisClientManager_Live()
        {
            var redisConStr = CFDGlobal.GetConfigurationSetting("redisConnectionString_Live");

            if (redisConStr == null) return null;

            return new PooledRedisClientManager(100, 2, redisConStr);
        }

        public static IRedisClientsManager GetDefaultPooledRedisClientsManager(bool isLive)
        {
            return isLive ? PooledRedisClientsManager_Live : PooledRedisClientsManager;
        }

        /// <summary>
        /// get a new BasicRedisClientManager (non-pooled)
        /// </summary>
        /// <returns></returns>
        public static IRedisClientsManager GetNewBasicRedisClientManager()
        {
            var redisConStr = CFDGlobal.GetConfigurationSetting("redisConnectionString");

            if (redisConStr == null) return null;

            return new BasicRedisClientManager(redisConStr);
        }

        public static string GetConfigurationSetting(string key)
        {
            if (RoleEnvironment.IsAvailable)
            {
                ////throw exception if not exist
                //return RoleEnvironment.GetConfigurationSettingValue(key);

                string value=null;
                try
                {
                    value = CloudConfigurationManager.GetSetting(key);
                }
                catch (Exception e)
                {
                }

                //if there's no cloud config, return local config
                return value ?? ConfigurationManager.AppSettings[key];
            }
            else
            {
                return ConfigurationManager.AppSettings[key];
            }
        }

        public static string GetDbConnectionString(string connectStringName)
        {
            if (RoleEnvironment.IsAvailable)
            {
                string value = null;
                try
                {
                    value = RoleEnvironment.GetConfigurationSettingValue(connectStringName);
                }
                catch (Exception e)
                {
                }

                //if there's no cloud config, return local config
                return value ?? ConfigurationManager.ConnectionStrings[connectStringName].ConnectionString;
            }
            else
            {
                return ConfigurationManager.ConnectionStrings[connectStringName].ConnectionString;
            }
        }

        public static T RetryMaxOrThrow<T>(Func<T> p, int sleepMilliSeconds = 10000, int retryMax = 3, bool verboseErrorLog = true)
        {
            int retryCount = 0;
            int currentSleepMilliSeconds = sleepMilliSeconds;
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

                        throw new ApplicationException("exceed retry count: " + ex.Message, ex);
                    }

                    //if (verboseErrorLog)
                    //    Trace.WriteLine("sleep " + currentSleepSeconds + "s...");

                    //#if !DEBUG
                    Thread.Sleep(currentSleepMilliSeconds);
                    //#endif
                }
            }
        }

        public static void LogError(string message)
        {
            Trace.TraceError(GetLogDatetimePrefix() + message);
        }
        public static void LogWarning(string message)
        {
            Trace.TraceWarning(GetLogDatetimePrefix() + message);
        }
        public static void LogInformation(string message)
        {
            Trace.TraceInformation(GetLogDatetimePrefix() + message);
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

            if (exception is FaultException<ExceptionDetail>)
            {
               var detail = ((FaultException<ExceptionDetail>)exception).Detail;

                var d = detail;
                while (d != null)
                {
                    Trace.WriteLine(GetLogDatetimePrefix() + d.Message);
                    Trace.WriteLine(GetLogDatetimePrefix() + d.StackTrace);

                    d = d.InnerException;
                }
            }
        }

        public static void LogExceptionAsInfo(Exception exception)
        {
            var ex = exception;
            while (ex != null)
            {
                Trace.TraceInformation(GetLogDatetimePrefix() + ex.Message);
                Trace.TraceInformation(GetLogDatetimePrefix() + ex.StackTrace);

                ex = ex.InnerException;
            }

            if (exception is FaultException<ExceptionDetail>)
            {
                var detail = ((FaultException<ExceptionDetail>)exception).Detail;

                var d = detail;
                while (d != null)
                {
                    Trace.TraceInformation(GetLogDatetimePrefix() + d.Message);
                    Trace.TraceInformation(GetLogDatetimePrefix() + d.StackTrace);

                    d = d.InnerException;
                }
            }
        }

        public static void LogExceptionAsWarning(Exception exception)
        {
            var ex = exception;
            while (ex != null)
            {
                Trace.TraceWarning(GetLogDatetimePrefix() + ex.Message);
                Trace.TraceWarning(GetLogDatetimePrefix() + ex.StackTrace);

                ex = ex.InnerException;
            }

            if (exception is FaultException<ExceptionDetail>)
            {
                var detail = ((FaultException<ExceptionDetail>)exception).Detail;

                var d = detail;
                while (d != null)
                {
                    Trace.TraceWarning(GetLogDatetimePrefix() + d.Message);
                    Trace.TraceWarning(GetLogDatetimePrefix() + d.StackTrace);

                    d = d.InnerException;
                }
            }
        }

        private static string GetLogDatetimePrefix()
        {
            return DateTime.Now.ToString(DATETIME_MASK_MILLI_SECOND)+" ";
        }

        public static bool? GenderChineseToBool(string gender)
        {
            switch (gender)
            {
                case "男":
                    return true;
                case "女":
                    return false;
                default:
                    return null;
            }
        }
    }
}
