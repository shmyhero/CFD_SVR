using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using EntityFramework.Extensions;

namespace CFD_COMMON.Service
{
    public class SecurityService
    {
        public CFDEntities db { get; set; }

        public SecurityService(CFDEntities db)
        {
            this.db = db;
        }

        public void DeleteBookmarks(int userId, IList<int> secIds)
        {
            //var bookmarks = db.Bookmarks.Where(o => ids.Contains(o.AyondoSecurityId));
            //db.Bookmarks.RemoveRange(bookmarks);
            //db.SaveChanges();

            int rowDeleted = db.Bookmarks.Where(o => o.UserId == userId && secIds.Contains(o.AyondoSecurityId)).Delete();
            CFDGlobal.LogLine("Delete bookmarks. " + rowDeleted + " rows deleted. " + "userid: " + userId);
            //no need to db.savechanges()
        }

        public void AddBookmarks(int userId, IList<int> secIds)
        {
            if (secIds.Count == 0) return;

            ////remove non-exist securities
            //var allSecIds = db.AyondoSecurities.Select(o => o.Id).ToList();
            //secIds = secIds.Where(o => allSecIds.Contains(o)).ToList();

            //get my current bookmarks
            var myBookmarks = db.Bookmarks.Where(o => o.UserId == userId).ToList();

            int? maxDisplayOrder = myBookmarks.Max(o => o.DisplayOrder);

            var order = maxDisplayOrder.HasValue ? maxDisplayOrder + 1 : 1;

            foreach (var secId in secIds)
            {
                if (myBookmarks.All(o => o.AyondoSecurityId != secId))//skip if already existed
                    db.Bookmarks.Add(new Bookmark
                    {
                        UserId = userId,
                        AyondoSecurityId = secId,
                        DisplayOrder = order++//setting display order
                    });
            }
            db.SaveChanges();
        }

        public void DeleteBookmarks(int userId)
        {
            int rowDeleted = db.Bookmarks.Where(o => o.UserId == userId).Delete();
            CFDGlobal.LogLine("Delete bookmarks. " + rowDeleted + " rows deleted. " + "userid: " + userId);
        }
    }
}
