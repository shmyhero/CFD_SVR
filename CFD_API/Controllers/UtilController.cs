using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using System.Threading.Tasks;
using System.Web;
using System.IO;
using System.Data.SqlTypes;

namespace CFD_API.Controllers
{
    [RoutePrefix("api")]
    public class UtilController : CFDController
    {
        public UtilController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [Route("sendCode")]
        [HttpPost]
        //[RequireHttps]
        public ResultDTO SendCode(string phone)
        {
            var result = new ResultDTO();

            if (!Phone.IsValidPhoneNumber(phone))
            {
                result.message = __(TransKey.INVALID_PHONE_NUMBER);
                result.success = false;
                return result;
            }

            string code = string.Empty;

            ////send last code instead of regenerating if within ?
            //if (verifyCodes.Any())
            //{
            //    var lastCode = verifyCodes.OrderByDescending(c => c.CreatedAt).First();
            //}

            ////day limit
            //if (verifyCodes.Count() >= 5)
            //{
            //    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKeys.SEND_CODE_LIMIT)));
            //}

            var r = new Random();
            code = r.Next(10000).ToString("0000");

            //TODO: prevent from brute-force attack

            if (!string.IsNullOrWhiteSpace(code))
            {
                CFDGlobal.RetryMaxOrThrow(() => YunPianMessenger.TplSendCodeSms(string.Format("#code#={0}", code), phone), sleepMilliSeconds: 0);

                db.VerifyCodes.Add(new VerifyCode
                {
                    Code = code,
                    SentAt = DateTime.UtcNow,
                    Phone = phone
                });
                db.SaveChanges();
            }

            result.success = true;
            return result;
        }

        [Route("banner")]
        [HttpGet]
        public IList<BannerDTO> GetBanners()
        {
            var banners = db.Banners.OrderBy(o => o.Id).ToList();
            return banners.Select(o => Mapper.Map<BannerDTO>(o)).ToList();
        }

        [Route("feedback")]
        [HttpPost]
        public HttpResponseMessage NewFeedback(FeedbackFormDTO form)
        {
            if (form.text.Trim() == String.Empty)
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "no text");

            db.Feedbacks.Add(new Feedback()
            {
                Phone = form.phone,
                Text = form.text,
                Time = DateTime.UtcNow,
            });
            db.SaveChanges();
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Route("getbannerbyid")]
        [HttpGet]
        public BannerDTO GetBannerById(int id)
        {
            var banners = db.Banners2.Where(item => item.Id == id ).ToList();
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
            banner.Expiration = DateTime.Now;
            db.SaveChanges();
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Route("postbanner")]
        [HttpPost]
        public async Task<Dictionary<string, string>> PostBanner()
        {
           Dictionary<string, string> dicFiles =  await UploadHelper.UploadImage(Request, file => new Dictionary<string, string>
                {
                    {"url", file == null? string.Empty : file.Location.AbsoluteUri}
                });

            Dictionary<string, string> dicFormData = new Dictionary<string, string>();
            Stream reqStream = Request.Content.ReadAsStreamAsync().Result;
            if (reqStream.CanSeek)
            {
                reqStream.Position = 0;
            }
            try
            {
                string fullPath = HttpContext.Current.Server.MapPath("~/App_Data");
                var streamProvider = new MultipartFormDataStreamProvider(fullPath);
                await Request.Content.ReadAsMultipartAsync(streamProvider);
                foreach (var key in streamProvider.FormData.AllKeys)
                {//接收FormData  
                    dicFormData.Add(key, streamProvider.FormData[key]);
                }

                //contains "ID" means update
                if (dicFormData.ContainsKey("ID"))
                {
                    UpdateBanner(dicFiles, dicFormData);
                }
                else //create banner
                {
                    CreateBanner(dicFiles, dicFormData);
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Request.CreateResponse(HttpStatusCode.ExpectationFailed);
            }

            return null;
        }

        private void CreateBanner(Dictionary<string, string> dicFiles, Dictionary<string, string> dicFormData)
        {
            //is banner on the top
            int isTop = 0;
            if (dicFormData.ContainsKey("IsTop"))
            {
                int.TryParse(dicFormData["IsTop"], out isTop);
            }
            DateTime? topAt = new DateTime();
            if (isTop == 1)
            {
                topAt = DateTime.Now;
            }
            else
            {
                topAt = null;
            }

            db.Banners2.Add(new Banner2()
            {
                Header = dicFormData.ContainsKey("Header") ? dicFormData["Header"] : string.Empty,
                Body = dicFormData.ContainsKey("Body") ? dicFormData["Body"] : string.Empty,
                IsTop = isTop,
                TopAt = topAt,
                CreatedAt = DateTime.Now,
                CreatedBy = dicFormData.ContainsKey("CreatedBy") ? dicFormData["CreatedBy"] : string.Empty,
                Expiration = SqlDateTime.MaxValue.Value,
                ImgUrl = dicFiles.ContainsKey("url") ? dicFiles["url"] : string.Empty
            });
        }

        private void UpdateBanner(Dictionary<string, string> dicFiles, Dictionary<string, string> dicFormData)
        {
            int isTop = 0;
            if (dicFormData.ContainsKey("IsTop"))
            {
                int.TryParse(dicFormData["IsTop"], out isTop);
            }
            DateTime? topAt = new DateTime();
            if (isTop == 1)
            {
                topAt = DateTime.Now;
            }
            else
            {
                topAt = null;
            }

            Banner2 banner = null;
            int id = 0;
            int.TryParse(dicFormData["ID"], out id);
            var banners = db.Banners2.Where(item => item.Id == id).ToList();
            if (banners != null && banners.Count > 0)
            {
                banner = banners.FirstOrDefault();
                banner.Header = dicFormData.ContainsKey("Header") ? dicFormData["Header"] : string.Empty;
                banner.Body = dicFormData.ContainsKey("Body") ? dicFormData["Body"] : string.Empty;
                banner.IsTop = isTop;
                banner.TopAt = topAt;
                banner.CreatedAt = DateTime.Now;
                banner.CreatedBy = dicFormData.ContainsKey("CreatedBy") ? dicFormData["CreatedBy"] : string.Empty;
                banner.Expiration = SqlDateTime.MaxValue.Value;
                if(dicFiles.ContainsKey("url") && !string.IsNullOrEmpty(dicFiles["url"]))
                {
                    banner.ImgUrl = dicFiles["url"];
                }
            }
            else
            {
                return;
            }
        }
    }
}