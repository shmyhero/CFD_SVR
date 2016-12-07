using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CFD_COMMON.Service;
using CFD_COMMON.Models.Context;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTestCard
    {
        [TestMethod]
        public void TestMethod1()
        {
            using (var db = CFDEntities.Create())
            {
                CardService svr = new CardService(db);
                var cardList = svr.GetCard(10M, 20M, 100);
            }
            
        }
    }
}
