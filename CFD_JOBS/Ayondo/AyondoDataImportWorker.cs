using System;
using System.Linq;
using CFD_COMMON;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using Microsoft.Office.Interop.Excel;

namespace CFD_JOBS.Ayondo
{
    internal class AyondoDataImportWorker
    {
        public static int ID_COL = 4;

        public static void Run()
        {
            //ProdDefImport();
            //PriceImport();
            ExcelImport();
        }

        private static void ProdDefImport()
        {
            var db = CFDEntities.Create();

            var basicClientManager = CFDGlobal.GetBasicRedisClientManager();
            var redisTypedClient = basicClientManager.GetClient().As<ProdDef>();

            var prodDefs = redisTypedClient.GetAll();

            var securities = db.AyondoSecurities.ToList();
            foreach (var prodDef in prodDefs)
            {
                var sec = securities.FirstOrDefault(o => o.Id == prodDef.Id);

                if (sec == null)
                    db.AyondoSecurities.Add(new AyondoSecurity
                    {
                        Id = prodDef.Id,
                        Name = prodDef.Name,
                        Symbol = prodDef.Symbol,
                        DefUpdatedAt = prodDef.Time
                    });
                else
                {
                    sec.DefUpdatedAt = prodDef.Time;
                }
            }
            db.SaveChanges();
        }

        private static void PriceImport()
        {
            var db = CFDEntities.Create();

            var basicClientManager = CFDGlobal.GetBasicRedisClientManager();
            var redisTypedClient = basicClientManager.GetClient().As<Quote>();

            var quotes = redisTypedClient.GetAll();

            var securities = db.AyondoSecurities.ToList();
            foreach (var quote in quotes)
            {
                var sec = securities.FirstOrDefault(o => o.Id == quote.Id);

                if (sec == null)
                {
                    CFDGlobal.LogLine("not exist: " + quote.Id + " " + quote.Time);
                    continue;
                }

                sec.Ask = quote.Offer;
                sec.Bid = quote.Bid;
                sec.QuoteUpdatedAt = quote.Time;
            }
            db.SaveChanges();
        }

        private static void ExcelImport()
        {
            var app = new Application();
            var workbook = app.Workbooks.Open(@"C:\Users\peter\Desktop\Ayondo在线产品列表.xlsx");
            //var workbook = app.Workbooks.Open("D:\\Downloads\\ayondo_products_CFD.xlsx");

            var sheet = (Worksheet)workbook.Worksheets["Sheet1"];
            //var sheet = (Worksheet)workbook.Worksheets["ayondo_products_CFD"];
            var range = sheet.UsedRange;

            var db = CFDEntities.Create();
            var securities = db.AyondoSecurities.ToList();

            for (var row = 2; row <= range.Rows.Count; row++)
            {
                //for (var col = 1; col <= range.Columns.Count; col++)
                //{
                //    var value = (range.Cells[row, col] as Range).Value2;
                //    CFDGlobal.LogLine(value.ToString());
                //}

                var id = Convert.ToInt32(range.Cells[row,3].Value);

                if(id==0) continue;

                var cName = (string)range.Cells[row, 14].Value;
                //var name = (string)range.Cells[row, 1].Value;
                //var assetClass = (string)range.Cells[row, 11].Value;
                //var financing = (string)range.Cells[row, 27].Value;

                if(cName=="NULL")
                    continue;

                //var query=new Queryable(
                var ayondoSecurity = securities.FirstOrDefault(o => o.Id == id);

                if (ayondoSecurity == null)
                {
                    CFDGlobal.LogLine("not exist: " + id );
                    continue;
                }

                ayondoSecurity.CName = cName;
                //ayondoSecurity.AssetClass = assetClass;
                //ayondoSecurity.Financing = financing;

                //db.AyondoSecurities.Where(o => o.Id == id).Update(o => o.CName == cName);
                //sec.ExpiryDate=
            }

            db.SaveChanges();
        }
    }
}