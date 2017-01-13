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

        public void DeleteBookmarks(int userId, IList<int> secIds, bool isLive)
        {
            //var bookmarks = db.Bookmarks.Where(o => ids.Contains(o.AyondoSecurityId));
            //db.Bookmarks.RemoveRange(bookmarks);
            //db.SaveChanges();

            int rowDeleted = isLive
                ? db.Bookmark_Live.Where(o => o.UserId == userId && secIds.Contains(o.AyondoSecurityId)).Delete()
                : db.Bookmarks.Where(o => o.UserId == userId && secIds.Contains(o.AyondoSecurityId)).Delete();
            CFDGlobal.LogLine("Delete bookmarks. " + rowDeleted + " rows deleted. " + "userid: " + userId);
            //no need to db.savechanges()
        }

        public void AppendBookmarks(int userId, IList<int> secIds, bool isLive)
        {
            if (secIds.Count == 0) return;

            //get my current bookmarks
            var myBookmarks = isLive
                ? db.Bookmark_Live.Where(o => o.UserId == userId).ToList().Select(o => o as BookmarkBase).ToList()
                : db.Bookmarks.Where(o => o.UserId == userId).ToList().Select(o => o as BookmarkBase).ToList();

            int? maxDisplayOrder = myBookmarks.Max(o => o.DisplayOrder);

            var order = maxDisplayOrder.HasValue ? maxDisplayOrder + 1 : 1;

            foreach (var secId in secIds)
            {
                if (myBookmarks.All(o => o.AyondoSecurityId != secId)) //skip if already existed
                {
                    if (isLive)
                        db.Bookmark_Live.Add(new Bookmark_Live()
                        {
                            UserId = userId,
                            AyondoSecurityId = secId,
                            DisplayOrder = order++ //setting display order
                        });
                    else
                        db.Bookmarks.Add(new Bookmark
                        {
                            UserId = userId,
                            AyondoSecurityId = secId,
                            DisplayOrder = order++ //setting display order
                        });
                }
            }
            db.SaveChanges();
        }

        public void PrependBookmarks(int userId, IList<int> secIds, bool isLive)
        {
            if (secIds.Count == 0) return;

            //get my current bookmarks
            var myBookmarks = isLive
                ? db.Bookmark_Live.Where(o => o.UserId == userId).ToList().Select(o => o as BookmarkBase).ToList()
                : db.Bookmarks.Where(o => o.UserId == userId).ToList().Select(o => o as BookmarkBase).ToList();

            int? minDisplayOrder = myBookmarks.Min(o => o.DisplayOrder);

            var order = minDisplayOrder.HasValue ? minDisplayOrder - secIds.Count : 1;

            foreach (var secId in secIds)
            {
                if (myBookmarks.All(o => o.AyondoSecurityId != secId)) //skip if already existed
                {
                    if (isLive)
                        db.Bookmark_Live.Add(new Bookmark_Live()
                        {
                            UserId = userId,
                            AyondoSecurityId = secId,
                            DisplayOrder = order++ //setting display order
                        });
                    else
                        db.Bookmarks.Add(new Bookmark
                        {
                            UserId = userId,
                            AyondoSecurityId = secId,
                            DisplayOrder = order++ //setting display order
                        });
                }
            }
            db.SaveChanges();
        }

        public void DeleteBookmarks(int userId, bool isLive)
        {
            int rowDeleted = isLive
                ? db.Bookmark_Live.Where(o => o.UserId == userId).Delete()
                : db.Bookmarks.Where(o => o.UserId == userId).Delete();
            CFDGlobal.LogLine("Delete bookmarks. " + rowDeleted + " rows deleted. " + "userid: " + userId);
        }
    }
}
