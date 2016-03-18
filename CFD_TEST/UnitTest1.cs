using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var strNow = JsonConvert.SerializeObject(DateTime.Now);
            var strUtcNow = JsonConvert.SerializeObject(DateTime.UtcNow);

            var ticks = DateTime.Now.Ticks;
            var ticks2 = DateTime.UtcNow.Ticks;
        }
    }
}
