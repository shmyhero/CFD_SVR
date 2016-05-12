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
            ExcelImport();
        }

        private static void ExcelImport()
        {
            var app = new Application();
            var workbook = app.Workbooks.Open(@"D:\Downloads\products_TH_Demo_230316.csv");
            //var workbook = app.Workbooks.Open("D:\\Downloads\\ayondo_products_CFD.xlsx");

            //var sheet = (Worksheet)workbook.Worksheets["Sheet1"];
            var sheet = (Worksheet)workbook.Worksheets["products_TH_Demo_230316"];
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

                var id = Convert.ToInt32(range.Cells[row,4].Value);

                if(id==0) continue;

                //var cName = (string)range.Cells[row, 14].Value;
                //var name = (string)range.Cells[row, 1].Value;
                //var assetClass = (string)range.Cells[row, 11].Value.trim;
                //var financing = (string)range.Cells[row, 27].Value.Trim();

                var baseCcy = ((string) range.Cells[row, 9].Value).Trim();
                var quoteCcy = ((string)range.Cells[row, 10].Value).Trim();

                //decimal lotSize = Convert.ToDecimal(range.Cells[row, 38].Value);
                //decimal baseMargin = Convert.ToDecimal(range.Cells[row, 31].Value);

                //var query=new Queryable(
                var ayondoSecurity = securities.FirstOrDefault(o => o.Id == id);

                if (ayondoSecurity == null)
                {
                    CFDGlobal.LogLine("not exist: " + id );
                    continue;
                }

                //ayondoSecurity.CName = cName;
                //ayondoSecurity.AssetClass = assetClass;
                //ayondoSecurity.Financing = financing;

                //ayondoSecurity.BaseCcy = baseCcy;
                //ayondoSecurity.QuoteCcy = quoteCcy;

                //ayondoSecurity.LotSize = lotSize;
                //ayondoSecurity.BaseMargin = baseMargin;

                //db.AyondoSecurities.Where(o => o.Id == id).Update(o => o.CName == cName);
                //sec.ExpiryDate=
            }

            db.SaveChanges();
        }
    }
}