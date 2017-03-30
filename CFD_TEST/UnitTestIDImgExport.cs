using CFD_COMMON.Models.Context;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_TEST
{
    [TestClass]
    public class UnitTestIDImgExport
    {
        /// <summary>
        /// 导出身份证照片，给运营的同事用
        /// </summary>
        [TestMethod]
        public void IdImgExport()
        {
            List<string> nickNames = new List<string>();
            nickNames.AddRange(new string[] {"williamz", "Rebecca", "hdabb1350996504", "mrshen", "Lin17", "Ospher",
                "teddyban", "s455294788", "xsj649", "myway1984", "kuokuo2011", "tgwyu116" });

            using (var db = CFDEntities.Create())
            {
                var result = (from u in db.Users
                             join ui in db.UserInfos on u.Id equals ui.UserId
                             into x
                             from y in x.DefaultIfEmpty()
                             where nickNames.Contains(u.AyLiveUsername)
                             select new { y.IdFrontImg, y.IdBackImg, u.AyLiveUsername }).ToList();

                result.ForEach(u => {
                    byte[] bytes = Convert.FromBase64String(u.IdFrontImg);
                    MemoryStream ms = new MemoryStream(bytes);
                    Bitmap bmp = new Bitmap(ms);
                    bmp.Save("idimages/" + u.AyLiveUsername + "_正面" + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);

                    bytes = Convert.FromBase64String(u.IdBackImg);
                    ms = new MemoryStream(bytes);
                    bmp = new Bitmap(ms);
                    bmp.Save("idimages/" + u.AyLiveUsername + "_反面" + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                });


            }
        }
    }
}
