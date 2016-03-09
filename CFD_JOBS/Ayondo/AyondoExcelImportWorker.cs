using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
//using Microsoft.Office.Interop.Excel;

namespace CFD_JOBS.Ayondo
{
    class AyondoExcelImportWorker
    {
        public static void Run()
        {
            //var app = new Application();
            //var workbook = app.Workbooks.Open("D:\\Downloads\\ayondo_products_CFD.xlsx");

            //var sheet = (Worksheet)workbook.Worksheets["ayondo_products_CFD"];
            //var range = sheet.UsedRange;

            //var db = CFDEntities.Create();
            //var securities = db.AyondoSecurities.ToList();

            //for (var row = 2; row <= range.Rows.Count; row++)
            //{
            //    for (var col = 1; col <= range.Columns.Count; col++)
            //    {
            //        var value =(range.Cells[row, col] as Range).Value2;
            //        CFDGlobal.LogLine(value.ToString());
            //    }

            //    var id = range.Cells[row, ID_COL];
            //    var sec = securities.Single(o => o.Id == id);
            //    //sec.ExpiryDate=
            //}
        }

        public static int ID_COL = 4;
    }
}
