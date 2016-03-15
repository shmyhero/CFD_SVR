using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using EntityFramework.Audit;
using EntityFramework.Extensions;
using Microsoft.Office.Interop.Excel;

namespace CFD_JOBS.Ayondo
{
    class AyondoExcelImportWorker
    {
        public static int ID_COL = 4;

        public static void Run()
        {
            var app = new Application();
            var workbook = app.Workbooks.Open("D:\\Downloads\\美股sample.xlsx");
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

                var id =Convert.ToInt32( range.Cells[row, 5].Value);
                var cName = (string)range.Cells[row, 1].Value;

                //var query=new Queryable(
                var ayondoSecurity = securities.Single(o => o.Id == id);
                ayondoSecurity.CName = cName;


                //db.AyondoSecurities.Where(o => o.Id == id).Update(o => o.CName == cName);
                //sec.ExpiryDate=
            }

            
            db.SaveChanges();
        }
    }
}
