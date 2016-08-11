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
using CFD_API.Azure;

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

        [Route("banner2")]
        [HttpGet]
        public IList<SimpleBannerDTO> GetBanners2()
        {
            int max = 5;
            //get top banner
            var topBanners = db.Banners2.Where(item => item.IsTop == 1 && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.TopAt).Take(5).ToList();
            
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
            int max = 5;
            var currentBanner = db.Banners2.Where(item => item.Id == id).FirstOrDefault();
            if (currentBanner == null)
                return null;

            List<Banner2> results = new List<Banner2>();
            if(currentBanner.IsTop.HasValue && currentBanner.IsTop.Value == 1) //last one is top banner
            {
                //get top banner
                results = db.Banners2.Where(item => item.IsTop == 1 && item.Expiration.HasValue && item.Expiration.Value == SqlDateTime.MaxValue.Value && item.TopAt < currentBanner.TopAt).OrderByDescending(o => o.TopAt).Take(5).ToList();

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
            banner.Expiration = DateTime.UtcNow;
            db.SaveChanges();
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Route("settop")]
        [HttpGet]
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
        public async Task<Dictionary<string, string>> PostBanner()
        {
           //Tuple<string, Dictionary<string, string>> formData =  await UploadHelper.UploadImage(Request);
            Tuple<string, Dictionary<string, string>> formData = await UploadHelper.UploadImage(Request, data => new Tuple<string, Dictionary<string, string>>
                (
                  data.Item1, data.Item2
                ));

            Stream reqStream = Request.Content.ReadAsStreamAsync().Result;
            if (reqStream.CanSeek)
            {
                reqStream.Position = 0;
            }
            try
            {
                //string fullPath = HttpContext.Current.Server.MapPath("~/App_Data");
                //var streamProvider = new MultipartFormDataStreamProvider(fullPath);
                //await Request.Content.ReadAsMultipartAsync(provider);
                //foreach (var data in formData.Item2)
                //{//接收FormData  
                //    dicFormData.Add(data.Key, provider.FormData[key]);
                //}

                //contains "ID" means update
                if (formData.Item2.ContainsKey("ID"))
                {
                    UpdateBanner(formData.Item1, formData.Item2);
                }
                else //create banner
                {
                    CreateBanner(formData.Item1, formData.Item2);
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Request.CreateResponse(HttpStatusCode.ExpectationFailed, ex.Message);
            }

            return null;
        }

        private void CreateBanner(string imgleUrl, Dictionary<string, string> dicFormData)
        {
            db.Banners2.Add(new Banner2()
            {
                Url = dicFormData.ContainsKey("Url") ? dicFormData["Url"] : string.Empty,
                Header = dicFormData.ContainsKey("Header") ? dicFormData["Header"] : string.Empty,
                Body = dicFormData.ContainsKey("Body") ? dicFormData["Body"] : string.Empty,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = dicFormData.ContainsKey("CreatedBy") ? dicFormData["CreatedBy"] : string.Empty,
                Expiration = SqlDateTime.MaxValue.Value,
                ImgUrl = imgleUrl
            });
        }

        private void UpdateBanner(string imageUrl, Dictionary<string, string> dicFormData)
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
                banner.CreatedAt = DateTime.UtcNow;
                banner.CreatedBy = dicFormData.ContainsKey("CreatedBy") ? dicFormData["CreatedBy"] : string.Empty;
                banner.Expiration = SqlDateTime.MaxValue.Value;
                if(!string.IsNullOrEmpty(imageUrl))
                {
                    banner.ImgUrl = imageUrl;
                }
            }
            else
            {
                return;
            }
        }

        [Route("operation/login")]
        [HttpPost]
        public string Login(OperationUserDTO userDTO)
        {
            int userType = 0;
            int.TryParse(userDTO.Type, out userType);
            OperationUser user = db.OperationUsers.FirstOrDefault(u => (u.UserName == userDTO.name) && (u.Password == userDTO.password) && (u.UserType == userType));

            if(user != null)
            {
                return "true";
            }
            else
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(userDTO);
            }
        }

        [HttpPost]
        [Route("headline")]
        public HttpResponseMessage PostHeadline(HeadlineDTO headLineDTO)
        {
            if(headLineDTO.id > 0) //update
            {
                UpdateHeadline(headLineDTO);
            }
            else//created
            {
                CreateHeadline(headLineDTO);
            }
            db.SaveChanges();

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private void CreateHeadline(HeadlineDTO headLineDTO)
        {
            db.Headlines.Add(
                new Headline()
                {
                    Header = headLineDTO.header,
                    Body = headLineDTO.body,
                    CreatedAt = DateTime.UtcNow,
                    Expiration = SqlDateTime.MaxValue.Value
                }
            );
        }

        private void UpdateHeadline(HeadlineDTO headLineDTO)
        {
            var headlines = db.Headlines.Where(item => item.Id == headLineDTO.id).ToList();
            if(headlines != null && headlines.Count > 0)
            {
                var headline = headlines.FirstOrDefault();
                headline.Header = headLineDTO.header;
                headline.Body = headLineDTO.body;
            }
        }

        [Route("headline/{id}")]
        [HttpDelete]
        public HttpResponseMessage DeleteHeadline(int id)
        {
            Headline headline = null;
            var headlines = db.Headlines.Where(item => item.Id == id).ToList();
            if (headlines != null && headlines.Count > 0)
            {
                headline = headlines.FirstOrDefault();
            }
            else
            {
                Request.CreateResponse(HttpStatusCode.OK);
            }
            headline.Expiration = DateTime.UtcNow;
            db.SaveChanges();
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [HttpGet]
        [Route("headline/{id}")]
        public IList<HeadlineDTO> GetHeadline(int id)
        {
            IList<Headline> headlines = null;
            int maxCount = 14;

            DateTime lastDay = new DateTime(DateTime.UtcNow.AddDays(-1).Year, DateTime.UtcNow.AddDays(-1).Month, DateTime.UtcNow.AddDays(-1).Day);

            if (id <= 0)//take top 10 by date
            {
                headlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && item.CreatedAt >= lastDay).OrderByDescending(o => o.CreatedAt).ToList();

                while (headlines == null || headlines.Count == 0)
                {
                    lastDay = lastDay.AddDays(-1);
                    headlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && item.CreatedAt >= lastDay).OrderByDescending(o => o.CreatedAt).ToList();

                    if (maxCount-- <= 0)//trace back for at most 2 weeks
                    {
                        break;
                    }
                }
            }
            else //take specific one
            {
                headlines = db.Headlines.Where(item => item.Id == id && item.Expiration.Value == SqlDateTime.MaxValue.Value && item.CreatedAt >= lastDay).OrderByDescending(o => o.CreatedAt).ToList();
            }

            return headlines.Select(o => Mapper.Map<HeadlineDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("headline/Top10")]
        public IList<HeadlineDTO> GetHeadlineTop10()
        {
            IList<Headline> headlines = null;
            int maxLines = 10;

            int maxCount = 14;

            DateTime lastDay = new DateTime(DateTime.UtcNow.AddDays(-1).Year, DateTime.UtcNow.AddDays(-1).Month, DateTime.UtcNow.AddDays(-1).Day);
            headlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && item.CreatedAt >= lastDay).OrderByDescending(o => o.CreatedAt).Take(maxLines).ToList();

            while(headlines == null || headlines.Count == 0)
            {
                lastDay = lastDay.AddDays(-1);
                headlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && item.CreatedAt >= lastDay).OrderByDescending(o => o.CreatedAt).Take(maxLines).ToList();

                if(maxCount-- <=0)//trace back for at most 2 weeks
                {
                    break;
                }
            }
            return headlines.Select(o => Mapper.Map<HeadlineDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("headline/group")]
        public IList<HeadlineGroupDTO> GetHeadlineGroup()
        {
            IList<Headline> headlines = null;
            int maxCount = 14;

            DateTime lastDay = new DateTime(DateTime.UtcNow.AddDays(-1).Year, DateTime.UtcNow.AddDays(-1).Month, DateTime.UtcNow.AddDays(-1).Day);

            headlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && item.CreatedAt >= lastDay).OrderByDescending(o => o.CreatedAt).ToList();

            while (headlines == null || headlines.Count == 0)
            {
                lastDay = lastDay.AddDays(-1);
                headlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && item.CreatedAt >= lastDay).OrderByDescending(o => o.CreatedAt).ToList();
                if (maxCount-- <= 0)//trace back for at most 2 weeks
                {
                    break;
                }
            }

            List<HeadlineGroupDTO> headlinesGroup = new List<HeadlineGroupDTO>();

            foreach (Headline headLine in headlines)
            {
                HeadlineGroupDTO headlineGroupDTO = headlinesGroup.FirstOrDefault(item => item.createdDay == headLine.CreatedAt.Value.ToString("yyyy-MM-dd"));
                if (headlineGroupDTO != null)
                {
                    headlineGroupDTO.headlines.Add(new HeadlineDTO() { id = headLine.Id, header = headLine.Header, body = headLine.Body, createdAt = headLine.CreatedAt });
                }
                else
                {
                    headlineGroupDTO = new HeadlineGroupDTO();
                    headlineGroupDTO.createdDay = headLine.CreatedAt.Value.ToString("yyyy-MM-dd");
                    headlineGroupDTO.headlines = new List<HeadlineDTO>();
                    headlineGroupDTO.headlines.Add(new HeadlineDTO() { id = headLine.Id, header = headLine.Header, body = headLine.Body, createdAt = headLine.CreatedAt });
                    headlinesGroup.Add(headlineGroupDTO);
                }
            }

            return headlinesGroup;
        }
    }
}