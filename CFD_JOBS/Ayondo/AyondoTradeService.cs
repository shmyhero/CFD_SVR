using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CFD_COMMON;

namespace CFD_JOBS.Ayondo
{
    class AyondoTradeService:IAyondoTradeService
    {
        public string Test(string text)
        {
            //CFDGlobal.LogLine("host service thread id " + Thread.CurrentThread.ManagedThreadId.ToString());
            return "You entered: " + text;
        }
    }
}
