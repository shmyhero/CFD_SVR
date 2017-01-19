using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTestRedis
    {
        [TestMethod]
        public void MultiThread()
        {
            IList<Thread> threads = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                var threadStart = new ThreadStart(delegate()
                {
                    using (var client = CFDGlobal.PooledRedisClientsManager.GetClient())
                    {
                        try
                        {
                            CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " begin");
                            var getAll = client.As<Tick>().Lists["tickRaw:10870"].GetAll();
                            CFDGlobal.LogLine(Thread.CurrentThread.ManagedThreadId + " end");
                        }
                        catch (Exception e)
                        {
                            CFDGlobal.LogException(e);
                        }
                    }
                });

                var thread = new Thread(threadStart);
                thread.Start();
                threads.Add(thread);
            }

            while (threads.Any(o => o.IsAlive))
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
    }
}
