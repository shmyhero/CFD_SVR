using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CFD_COMMON
{
    public class CFDGlobal
    {
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
    }
}
