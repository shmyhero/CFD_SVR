using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON.Models.Context;
using System.Data.SqlTypes;
using CFD_COMMON.Models.Entities;
using System;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using CFD_COMMON;
using CFD_API.Controllers.Attributes;

namespace CFD_API.Controllers
{
    [RoutePrefix("api")]
    public class BannerController : CFDController
    {
        public BannerController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [Route("banner")]
        [HttpGet]
        public IList<BannerDTO> GetBanners()
        {
            var banners = db.Banners.OrderBy(o => o.Id).ToList();
            return banners.Select(o => Mapper.Map<BannerDTO>(o)).ToList();
        }

        /// <summary>
        /// 以前的逻辑：只返回BannerType为空或者为0或3的，用于实盘
        /// 现在的逻辑：返回DisplayFor=Live或Display=Both的
        /// </summary>
        /// <returns></returns>
        [Route("banner2")]
        [HttpGet]
        public IList<SimpleBannerDTO> GetBanners2()
        {
            int max = 5;
            //get top banner
            //var topBanners = db.Banners2.Where(item => item.IsTop == 1 && item.Expiration.HasValue && (!item.BannerType.HasValue || item.BannerType.Value == 0 || item.BannerType.Value == 3) && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.TopAt).Take(5).ToList();
            var topBanners = db.Banners2.Where(item => item.IsTop == 1 && item.Expiration.HasValue && (item.DisplayFor == DisplayFor.Live || item.DisplayFor == DisplayFor.Both) && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.TopAt).Take(5).ToList();

            if (topBanners.Count < max)
            {
                //var nonTopBanner = db.Banners2.Where(item => (item.IsTop == 0 || !item.IsTop.HasValue) && (!item.BannerType.HasValue || item.BannerType.Value == 0 || item.BannerType.Value == 3) && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.Id).Take(max - topBanners.Count).ToList();
                var nonTopBanner = db.Banners2.Where(item => (item.IsTop == 0 || !item.IsTop.HasValue) && (item.DisplayFor == DisplayFor.Live || item.DisplayFor == DisplayFor.Both) && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.Id).Take(max - topBanners.Count).ToList();
                topBanners.AddRange(nonTopBanner);
            }

            return topBanners.Select(o => Mapper.Map<SimpleBannerDTO>(o)).ToList();
        }

        /// <summary>
        /// 以前的逻辑：返回所有的Banner，用于模拟
        /// 现在的逻辑：返回DisplayFor = Demo或Both的记录
        /// </summary>
        /// <returns></returns>
        [Route("banner/all")]
        [HttpGet]
        public IList<SimpleBannerDTO> GetBannersWithType()
        {
            int max = 5;
            //get top banner
            //var topBanners = db.Banners2.Where(item => item.IsTop == 1 && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.TopAt).Take(5).ToList();
            var topBanners = db.Banners2.Where(item => item.IsTop == 1 && (item.DisplayFor == DisplayFor.Demo || item.DisplayFor == DisplayFor.Both) && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.TopAt).Take(5).ToList();

            if (topBanners.Count < max)
            {
                //var nonTopBanner = db.Banners2.Where(item => (item.IsTop == 0 || !item.IsTop.HasValue) && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.Id).Take(max - topBanners.Count).ToList();
                var nonTopBanner = db.Banners2.Where(item => (item.IsTop == 0 || !item.IsTop.HasValue) && (item.DisplayFor == DisplayFor.Demo || item.DisplayFor == DisplayFor.Both) && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.Id).Take(max - topBanners.Count).ToList();
                topBanners.AddRange(nonTopBanner);
            }

            return topBanners.Select(o => Mapper.Map<SimpleBannerDTO>(o)).ToList();
        }

        /// <summary>
        /// 返回所有的Banner，不论Demo或Live。
        /// 给后台管理员用
        /// </summary>
        /// <returns></returns>
        [Route("banner/admin")]
        [HttpGet]
        public IList<SimpleBannerDTO> GetAllBanners()
        {
            int max = 10;
            //get top banner
            var topBanners = db.Banners2.Where(item => item.IsTop == 1 && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.TopAt).Take(max).ToList();

            if (topBanners.Count < max)
            {
                var nonTopBanner = db.Banners2.Where(item => (item.IsTop == 0 || !item.IsTop.HasValue) && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.Id).Take(max - topBanners.Count).ToList();
                topBanners.AddRange(nonTopBanner);
            }

            return topBanners.Select(o => Mapper.Map<SimpleBannerDTO>(o)).ToList();
        }

        [Route("nextbanner/{id}")]
        [HttpGet]
        public IList<SimpleBannerDTO> NextBanner(int id)
        {
            int max = 10;
            var currentBanner = db.Banners2.Where(item => item.Id == id).FirstOrDefault();
            if (currentBanner == null)
                return null;

            List<Banner2> results = new List<Banner2>();
            if (currentBanner.IsTop.HasValue && currentBanner.IsTop.Value == 1) //last one is top banner
            {
                //get top banner
                results = db.Banners2.Where(item => item.IsTop == 1 && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value && item.TopAt < currentBanner.TopAt).OrderByDescending(o => o.TopAt).Take(max).ToList();

                if (results.Count < max)
                {
                    var nonTopBanner = db.Banners2.Where(item => (item.IsTop == 0 || !item.IsTop.HasValue) && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.Id).Take(max - results.Count).ToList();
                    results.AddRange(nonTopBanner);
                }

            }
            else
            {
                results = db.Banners2.Where(item => (item.IsTop == 0 || !item.IsTop.HasValue) && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value && item.Id < id).OrderByDescending(o => o.Id).Take(max - results.Count).ToList();
            }

            return results.Select(o => Mapper.Map<SimpleBannerDTO>(o)).ToList();
        }

        [Route("getbannerbyid")]
        [HttpGet]
        public BannerDTO GetBannerById(int id)
        {
            var banners = db.Banners2.Where(item => item.Id == id).ToList();
            if (banners != null && banners.Count > 0)
            {
                return Mapper.Map<BannerDTO>(banners.FirstOrDefault());
            }
            else
            {
                return null;
            }
        }

        [Route("deletebanner")]
        [HttpDelete]
        [AdminAuth]
        public HttpResponseMessage DeleteBanner(int id)
        {
            Banner2 banner = null;
            var banners = db.Banners2.Where(item => item.Id == id).ToList();
            if (banners != null && banners.Count > 0)
            {
                banner = banners.FirstOrDefault();
            }
            else
            {
                Request.CreateResponse(HttpStatusCode.OK);
            }
            banner.Expiration = DateTime.UtcNow;
            db.SaveChanges();
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Route("settop")]
        [HttpGet]
        [AdminAuth]
        public HttpResponseMessage SetTop(int id)
        {
            Banner2 banner = null;
            var banners = db.Banners2.Where(item => item.Id == id).ToList();
            if (banners != null && banners.Count > 0)
            {
                banner = banners.FirstOrDefault();
            }
            else
            {
                Request.CreateResponse(HttpStatusCode.OK);
            }
            banner.IsTop = 1;
            banner.TopAt = DateTime.UtcNow;
            db.SaveChanges();
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Route("postbanner")]
        [HttpPost]
        [AdminAuth]
        public async Task<Dictionary<string, string>> PostBanner()
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                Request.CreateResponse(HttpStatusCode.ExpectationFailed);
                return null;
            }

            var provider = new MultipartFormDataStreamProvider(Path.GetTempPath());
            await Request.Content.ReadAsMultipartAsync(provider);

            List<string> imgList = UploadHelper.UploadFiles(provider, CFDGlobal.BANNER_PIC_BLOB_CONTAINER);

            string imgUrl = imgList.Count > 0 ? imgList[0] : string.Empty;
            string imgUrlBig = imgList.Count > 1 ? imgList[1] : string.Empty;

            Dictionary <string, string> formData = UploadHelper.GetFormData(provider);

            try
            {
                //contains "ID" means update
                if (formData.ContainsKey("ID"))
                {
                    UpdateBanner(imgUrl, imgUrlBig, formData);
                }
                else //create banner
                {
                    CreateBanner(imgUrl, imgUrlBig, formData);
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Request.CreateResponse(HttpStatusCode.ExpectationFailed, ex.Message);
            }

            return null;
        }

        private void CreateBanner(string imgUrl, string imgUrlBig, Dictionary<string, string> dicFormData)
        {
            int bannerType = 0;
            if (dicFormData.ContainsKey("BannerType"))
            {
                int.TryParse(dicFormData["BannerType"], out bannerType);
            }

            db.Banners2.Add(new Banner2()
            {
                Url = dicFormData.ContainsKey("Url") ? dicFormData["Url"] : string.Empty,
                Header = dicFormData.ContainsKey("Header") ? dicFormData["Header"] : string.Empty,
                Body = dicFormData.ContainsKey("Body") ? dicFormData["Body"] : string.Empty,
                Digest = dicFormData.ContainsKey("Digest") ? dicFormData["Digest"] : string.Empty,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = dicFormData.ContainsKey("CreatedBy") ? dicFormData["CreatedBy"] : string.Empty,
                BannerType = bannerType,
                Expiration = SqlDateTime.MaxValue.Value,
                ImgUrl = imgUrl,
                ImgUrlBig = imgUrlBig,
                Color = dicFormData.ContainsKey("Color") ? dicFormData["Color"] : string.Empty,
                DisplayFor = dicFormData.ContainsKey("DisplayFor") ? dicFormData["DisplayFor"] : DisplayFor.Both,
            });
        }

        private void UpdateBanner(string imgUrl, string imgUrlBig, Dictionary<string, string> dicFormData)
        {
            Banner2 banner = null;
            int id = 0;
            int.TryParse(dicFormData["ID"], out id);
            var banners = db.Banners2.Where(item => item.Id == id).ToList();
            if (banners != null && banners.Count > 0)
            {
                banner = banners.FirstOrDefault();
                banner.Url = dicFormData.ContainsKey("Url") ? dicFormData["Url"] : string.Empty;
                banner.Header = dicFormData.ContainsKey("Header") ? dicFormData["Header"] : string.Empty;
                banner.Body = dicFormData.ContainsKey("Body") ? dicFormData["Body"] : string.Empty;
                banner.Digest = dicFormData.ContainsKey("Digest") ? dicFormData["Digest"] : string.Empty;
                banner.CreatedBy = dicFormData.ContainsKey("CreatedBy") ? dicFormData["CreatedBy"] : string.Empty;
                banner.Color = dicFormData.ContainsKey("Color")? dicFormData["Color"] : string.Empty;
                banner.DisplayFor = dicFormData.ContainsKey("DisplayFor") ? dicFormData["DisplayFor"] : DisplayFor.Both;
                int bannerType = 0;
                if(dicFormData.ContainsKey("BannerType"))
                {
                    int.TryParse(dicFormData["BannerType"], out bannerType);
                }
                banner.BannerType = bannerType;
                if (!string.IsNullOrEmpty(imgUrl))
                {
                    banner.ImgUrl = imgUrl;
                }
                if (!string.IsNullOrEmpty(imgUrlBig))
                {
                    banner.ImgUrlBig = imgUrlBig;
                }
            }
            else
            {
                return;
            }
        }

    }

    public struct DisplayFor
    {
        public const string Demo = "Demo";
        public const string Live = "Live";
        public const string Both = "Both";
    }
}