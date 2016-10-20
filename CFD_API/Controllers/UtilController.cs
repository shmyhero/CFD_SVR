﻿using System;
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
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;
using CFD_COMMON.Azure;
using System.Text.RegularExpressions;
using AyondoTrade;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.OpenSsl;

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

        /// <summary>
        /// feedback with picture(s)
        /// </summary>
        /// <returns></returns>
        [Route("feedback_pic")]
        [HttpPost]
        public ResultDTO NewFeedbackPicture(FeedBackFormDTO_Pic feedBackDTO)
        {
            ResultDTO result = new ResultDTO();
            List<string> picList = new List<string>();
            foreach (string picture in feedBackDTO.photos)
            {
                string picName = Guid.NewGuid().ToString("N");
                Blob.UploadFromBytes(CFDGlobal.FEEDBACK_PIC_BLOC_CONTAINER, picName, Convert.FromBase64String(picture));
                picList.Add(picName);
            }
            try
            {
                Feedback feedBack = new Feedback();
                feedBack.Phone = feedBackDTO.phone;
                feedBack.Text = feedBackDTO.text;
                feedBack.PicUrl = GetPicUrl(picList);
                feedBack.Time = DateTime.UtcNow;
                db.Feedbacks.Add(feedBack);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                result.success = false;
                result.message = __(TransKey.EXCEPTION);
            }

            result.success = true;
            return result;
        }

        [HttpGet]
        [Route("feedback")]
        public List<FeedBackFormDTO_Pic> GetAllFeedBacks()
        {
            List<Feedback> feedbacks = db.Feedbacks.OrderByDescending(o => o.Id).Take(20).ToList();

            List<FeedBackFormDTO_Pic> feedBackDTO = new List<FeedBackFormDTO_Pic>();
            feedbacks.ForEach(
                    o =>
                    {
                        List<string> photos = new List<string>();
                        if(!string.IsNullOrEmpty(o.PicUrl))
                        {
                            photos.AddRange(o.PicUrl.Split(';').ToList().Where(url => !string.IsNullOrEmpty(url)));
                            for (int x = 0; x < photos.Count; x++)
                            {
                                photos[x] = CFDGlobal.FEEDBACK_PIC_BLOC_CONTAINER_URL + photos[x];
                            }
                        }

                        feedBackDTO.Add(new FeedBackFormDTO_Pic()
                        {
                            id = o.Id,
                            phone = o.Phone,
                            text = o.Text,
                            photos = photos
                        });
                    }
                );

            return feedBackDTO;
        }

        [HttpGet]
        [Route("feedback/anonymous")]
        public List<FeedBackFormDTO_Pic> GetAnonymousFeedBacks()
        {
            List<Feedback> feedbacks = db.Feedbacks.OrderByDescending(o => o.Id).Where(x=>string.IsNullOrEmpty(x.Phone)).Take(20).ToList();

            List<FeedBackFormDTO_Pic> feedBackDTO = new List<FeedBackFormDTO_Pic>();
            feedbacks.ForEach(
                    o =>
                    {
                        List<string> photos = new List<string>();
                        if (!string.IsNullOrEmpty(o.PicUrl))
                        {
                            photos.AddRange(o.PicUrl.Split(';').ToList().Where(url => !string.IsNullOrEmpty(url)));
                            for (int x = 0; x < photos.Count; x++)
                            {
                                photos[x] = CFDGlobal.FEEDBACK_PIC_BLOC_CONTAINER_URL + photos[x];
                            }
                        }

                        feedBackDTO.Add(new FeedBackFormDTO_Pic()
                        {
                            id = o.Id,
                            phone = o.Phone,
                            text = o.Text,
                            photos = photos
                        });
                    }
                );

            return feedBackDTO;
        }

        [HttpGet]
        [Route("nextfeedback/{id}")]
        public List<FeedBackFormDTO_Pic> NextFeedBack(int id)
        {
            List<Feedback> feedbacks = db.Feedbacks.Where(o=>o.Id < id).OrderByDescending(o => o.Time).Take(20).ToList();

            List<FeedBackFormDTO_Pic> feedBackDTO = new List<FeedBackFormDTO_Pic>();
            feedbacks.ForEach(
                    o =>
                    {
                        List<string> photos = new List<string>();
                        if (!string.IsNullOrEmpty(o.PicUrl))
                        {
                            photos.AddRange(o.PicUrl.Split(';').ToList().Where(url => !string.IsNullOrEmpty(url)));
                            for (int x = 0; x < photos.Count; x++)
                            {
                                photos[x] = CFDGlobal.FEEDBACK_PIC_BLOC_CONTAINER_URL + photos[x];
                            }
                        }

                        feedBackDTO.Add(new FeedBackFormDTO_Pic()
                        {
                            id = o.Id,
                            phone = o.Phone,
                            text = o.Text,
                            photos = photos
                        });
                    }
                );

            return feedBackDTO;
        }

        [HttpGet]
        [Route("nextfeedback/anonymous/{id}")]
        public List<FeedBackFormDTO_Pic> NextAnonymousFeedBack(int id)
        {
            List<Feedback> feedbacks = db.Feedbacks.Where(o => o.Id < id && string.IsNullOrEmpty(o.Phone)).OrderByDescending(o => o.Time).Take(20).ToList();

            List<FeedBackFormDTO_Pic> feedBackDTO = new List<FeedBackFormDTO_Pic>();
            feedbacks.ForEach(
                    o =>
                    {
                        List<string> photos = new List<string>();
                        if (!string.IsNullOrEmpty(o.PicUrl))
                        {
                            photos.AddRange(o.PicUrl.Split(';').ToList().Where(url => !string.IsNullOrEmpty(url)));
                            for (int x = 0; x < photos.Count; x++)
                            {
                                photos[x] = CFDGlobal.FEEDBACK_PIC_BLOC_CONTAINER_URL + photos[x];
                            }
                        }

                        feedBackDTO.Add(new FeedBackFormDTO_Pic()
                        {
                            id = o.Id,
                            phone = o.Phone,
                            text = o.Text,
                            photos = photos
                        });
                    }
                );

            return feedBackDTO;
        }

        [HttpGet]
        [Route("feedback/phone/{number}")]
        public List<FeedBackFormDTO_Pic> GetFeedBacksByPhone(string number)
        {
            List<Feedback> feedbacks = db.Feedbacks.Where(x=>x.Phone.Contains(number)).OrderByDescending(o => o.Id).Take(20).ToList();

            List<FeedBackFormDTO_Pic> feedBackDTO = new List<FeedBackFormDTO_Pic>();
            feedbacks.ForEach(
                    o =>
                    {
                        List<string> photos = new List<string>();
                        if (!string.IsNullOrEmpty(o.PicUrl))
                        {
                            photos.AddRange(o.PicUrl.Split(';').ToList().Where(url => !string.IsNullOrEmpty(url)));
                            for (int x = 0; x < photos.Count; x++)
                            {
                                photos[x] = CFDGlobal.FEEDBACK_PIC_BLOC_CONTAINER_URL + photos[x];
                            }
                        }

                        feedBackDTO.Add(new FeedBackFormDTO_Pic()
                        {
                            id = o.Id,
                            phone = o.Phone,
                            text = o.Text,
                            photos = photos
                        });
                    }
                );

            return feedBackDTO;
        }

        //public async Task<Dictionary<string, string>> NewFeedbackPicture()
        //{
        //    if (!Request.Content.IsMimeMultipartContent())
        //    {
        //        Request.CreateResponse(HttpStatusCode.ExpectationFailed);
        //        return null;
        //    }

        //    var provider = new MultipartFormDataStreamProvider(Path.GetTempPath());
        //    await Request.Content.ReadAsMultipartAsync(provider);

        //    List<string> imgList = UploadHelper.UploadFiles(provider, CFDGlobal.FEEDBACK_PIC_BLOC_CONTAINER);
        //    Dictionary<string, string> formData = UploadHelper.GetFormData(provider);

        //    try
        //    {
        //        Feedback feedBack = new Feedback();
        //        feedBack.Phone = formData.ContainsKey("phone") ? formData["phone"] : string.Empty;
        //        feedBack.Text = formData.ContainsKey("text") ? formData["text"] : string.Empty;
        //        feedBack.PicUrl = GetPicUrl(imgList);
        //        feedBack.Time = DateTime.UtcNow;
        //        db.Feedbacks.Add(feedBack);
        //        db.SaveChanges();
        //    }
        //    catch (Exception ex)
        //    {
        //        Request.CreateResponse(HttpStatusCode.ExpectationFailed, ex.Message);
        //    }

        //    return null;
        //}

        private string GetPicUrl(List<string> imgList)
        {
            StringBuilder sb = new StringBuilder();
            imgList.ForEach(url => { sb.Append(url); sb.Append(";"); });
            return sb.ToString();
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
            if (!Request.Content.IsMimeMultipartContent())
            {
                Request.CreateResponse(HttpStatusCode.ExpectationFailed);
                return null;
            }

            var provider = new MultipartFormDataStreamProvider(Path.GetTempPath());
            await Request.Content.ReadAsMultipartAsync(provider);

            List<string> imgList = UploadHelper.UploadFiles(provider, CFDGlobal.BANNER_PIC_BLOB_CONTAINER);
            Dictionary<string, string> formData = UploadHelper.GetFormData(provider);

            try
            {
                //contains "ID" means update
                if (formData.ContainsKey("ID"))
                {
                    UpdateBanner(imgList.FirstOrDefault(), formData);
                }
                else //create banner
                {
                    CreateBanner(imgList.FirstOrDefault(), formData);
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
                Digest = dicFormData.ContainsKey("Digest") ? dicFormData["Digest"] : string.Empty,
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
                banner.Digest = dicFormData.ContainsKey("Digest") ? dicFormData["Digest"] : string.Empty;
                banner.CreatedBy = dicFormData.ContainsKey("CreatedBy") ? dicFormData["CreatedBy"] : string.Empty;
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
            Headline headline = new Headline()
            {
                Header = headLineDTO.header,
                Body = headLineDTO.body,
                Color = headLineDTO.color,
                CreatedAt = DateTime.UtcNow,
                Expiration = SqlDateTime.MaxValue.Value
            };

            if(!string.IsNullOrEmpty(headLineDTO.image))
            {
                string picName = Guid.NewGuid().ToString("N");
                Byte[] bytes = Convert.FromBase64String(headLineDTO.image);
                Blob.UploadFromBytes(CFDGlobal.HEADLINE_PIC_BLOB_CONTAINER, picName, bytes);

                headline.ImgUrl = CFDGlobal.HEADLINE_PIC_BLOB_CONTAINER_URL + picName;
            }


            db.Headlines.Add(headline);
        }

        private void UpdateHeadline(HeadlineDTO headLineDTO)
        {
            var headlines = db.Headlines.Where(item => item.Id == headLineDTO.id).ToList();
            if(headlines != null && headlines.Count > 0)
            {
                var headline = headlines.FirstOrDefault();

                if(!string.IsNullOrEmpty(headLineDTO.image))
                {
                    string picName = string.Empty;
                    if (!string.IsNullOrEmpty(headline.ImgUrl)) //delete existing blob before upload
                    {
                        picName = headline.ImgUrl.Split('/').Last();
                        Blob.DeleteBlob(CFDGlobal.HEADLINE_PIC_BLOB_CONTAINER, picName);
                    }

                    if(string.IsNullOrEmpty(picName))
                    {
                        picName = Guid.NewGuid().ToString("N");
                    }

                    Byte[] bytes = Convert.FromBase64String(headLineDTO.image);
                    Blob.UploadFromBytes(CFDGlobal.HEADLINE_PIC_BLOB_CONTAINER, picName, bytes);

                    headline.ImgUrl = CFDGlobal.HEADLINE_PIC_BLOB_CONTAINER_URL + picName;
                }

                headline.Header = headLineDTO.header;
                headline.Body = headLineDTO.body;
                headline.Color = headLineDTO.color;
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

            //return headlines.Select(o => Mapper.Map<HeadlineDTO>(o)).ToList();
            return headlines.Select(o => new HeadlineDTO() {
                id = o.Id,
                header = o.Header,
                body = o.Body,
                image = o.ImgUrl,
                color = o.Color.HasValue ? o.Color.Value : 0,
                createdAt = o.CreatedAt
            }).ToList();
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
            //return headlines.Select(o => Mapper.Map<HeadlineDTO>(o)).ToList();
            return headlines.Select(o => new HeadlineDTO()
            {
                id = o.Id,
                header = o.Header,
                body = o.Body,
                image = o.ImgUrl,
                color = o.Color.HasValue ? o.Color.Value : 0,
                createdAt = o.CreatedAt
            }).ToList();
        }

        [HttpGet]
        [Route("headline/group")]
        public IList<HeadlineGroupDTO> GetHeadlineGroup()
        {
            List<Headline> headlines = null;
            //find past 7 days which has headlines
            int maxDays = 7;

            DateTime chinaToday = DateTime.UtcNow.AddHours(8);
            List<HeadlineGroupDTO> headlinesGroup = new List<HeadlineGroupDTO>();

            while(maxDays > 0)
            {
                DateTime chinaLastDay = chinaToday.AddDays(-1);
                var tempHeadlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && item.CreatedAt >= chinaLastDay && item.CreatedAt <= chinaToday).OrderByDescending(o => o.CreatedAt).ToList();
                chinaToday = chinaToday.AddDays(-1);

                if (tempHeadlines != null && tempHeadlines.Count > 0) // find those days which has headline (total 7 days)
                {
                    maxDays--;
                    if (headlines != null)
                    {
                        headlines.AddRange(tempHeadlines);
                    }
                    else
                    {
                        headlines = tempHeadlines;
                    }
                }
               
            }

            foreach (Headline headLine in headlines)
            {
                HeadlineGroupDTO headlineGroupDTO = headlinesGroup.FirstOrDefault(item => item.createdDay == headLine.CreatedAt.Value.ToString("yyyy-MM-dd"));
                if (headlineGroupDTO != null)
                {
                    headlineGroupDTO.headlines.Add(new HeadlineDTO() { id = headLine.Id, header = headLine.Header, body = headLine.Body, createdAt = headLine.CreatedAt, color = headLine.Color.HasValue? headLine.Color.Value : 0, image = headLine.ImgUrl });
                }
                else
                {
                    headlineGroupDTO = new HeadlineGroupDTO();
                    headlineGroupDTO.createdDay = headLine.CreatedAt.Value.ToString("yyyy-MM-dd");
                    headlineGroupDTO.headlines = new List<HeadlineDTO>();
                    headlineGroupDTO.headlines.Add(new HeadlineDTO() { id = headLine.Id, header = headLine.Header, body = headLine.Body, createdAt = headLine.CreatedAt, color = headLine.Color.HasValue ? headLine.Color.Value : 0, image = headLine.ImgUrl });
                    headlinesGroup.Add(headlineGroupDTO);
                }
            }

            return headlinesGroup;
        }

        private const string SMS_Auth = "7AF1CCCC-DDB8-460A-A526-B204C91D316E";
        [HttpPost]
        [Route("SMS")]
        public ResultDTO SendSMS(SMSDTO form)
        {
            ResultDTO result = new ResultDTO() { success = true };

            if (Request.Headers.Authorization == null || Request.Headers.Authorization.Parameter != SMS_Auth)
            {
                result.success = false;
                result.message = "not authorized.";
                return result;
            }

            if (form == null || string.IsNullOrEmpty(form.mobile) || string.IsNullOrEmpty(form.message))
            {
                result.success = false;
                result.message = "missing necessary parameter.";
                return result;
            }

            string pattern = "[1][358]\\d{9}";
            if(!Regex.IsMatch(form.mobile, pattern))
            {
                result.success = false;
                result.message = "invalid mobile number.";
                return result;
            }

            YunPianMessenger.SendSms(form.message, form.mobile);

            return result;
        }

        [HttpGet]
        [Route("demo/oauth")]
        public HttpResponseMessage AyondoDemoOAuth()
        {
            var queryNameValuePairs = Request.GetQueryNameValuePairs();
            //CFDGlobal.LogInformation(oauth_token+" "+state+" "+expires_in);

            var currentUrl = Request.RequestUri.GetLeftPart(UriPartial.Path);

            var errorResponse = Request.CreateResponse(HttpStatusCode.Redirect);
            errorResponse.Headers.Location = new Uri(currentUrl + "/error");

            var error = queryNameValuePairs.FirstOrDefault(o => o.Key == "error").Value;
            if (!string.IsNullOrWhiteSpace(error))
            {
                string log = queryNameValuePairs.Aggregate("Demo OAuth error: ",
                    (current, pair) => current + (pair.Key + " " + pair.Value + ", "));
                CFDGlobal.LogLine(log);

                //return "ERROR";
                return errorResponse;
            }

            var oauth_token = queryNameValuePairs.FirstOrDefault(o => o.Key == "oauth_token").Value;
            if (!string.IsNullOrWhiteSpace(oauth_token))
            {
                var bytes = Convert.FromBase64String(oauth_token);

                var decryptEngine = new Pkcs1Encoding(new RsaEngine());
                using (var txtreader = new StringReader(CFDGlobal.OAUTH_TOKEN_PUBLIC_KEY))
                {
                    var keyParameter = (AsymmetricKeyParameter) new PemReader(txtreader).ReadObject();
                    decryptEngine.Init(false, keyParameter);
                }

                var decrypted = Encoding.UTF8.GetString(decryptEngine.ProcessBlock(bytes, 0, bytes.Length));

                var split = decrypted.Split(':');
                var username1 = split[0];
                var username2 = split[1]; //ayondo username
                var expiry = split[2];
                var checksum = split[3];

                // check if cfd userid and ayondo username are bound
                var state = queryNameValuePairs.FirstOrDefault(o => o.Key == "state").Value;
                var userId = Convert.ToInt32(state);
                var user = db.Users.FirstOrDefault(o => o.Id == userId);
                if (user == null || user.AyondoUsername != username2)
                {
                    CFDGlobal.LogLine("cfd user id and ayondo demo username doesn't match");
                    return errorResponse;
                }

                using (var client = new AyondoTradeClient())
                {
                    var account = client.LoginOAuth(username2, oauth_token);

                    CFDGlobal.LogLine("Demo OAuth logged in: " + username2 + " " + account);
                }

                //return "OK";
                var okResponse = Request.CreateResponse(HttpStatusCode.Redirect);
                okResponse.Headers.Location = new Uri(currentUrl + "/ok");
                return okResponse;
            }

            return errorResponse;
        }

        [HttpGet]
        [Route("demo/oauth/ok")]
        public string AyondoDemoOAuthOK()
        {
            return "OK";
        }

        [HttpGet]
        [Route("demo/oauth/error")]
        public string AyondoDemoOAuthError()
        {
            return "ERROR";
        }

        [HttpGet]
        [Route("live/oauth")]
        public HttpResponseMessage AyondoLiveOAuth()
        {
            var queryNameValuePairs = Request.GetQueryNameValuePairs();
            //CFDGlobal.LogInformation(oauth_token+" "+state+" "+expires_in);

            var currentUrl = Request.RequestUri.GetLeftPart(UriPartial.Path);

            var errorResponse = Request.CreateResponse(HttpStatusCode.Redirect);
            errorResponse.Headers.Location = new Uri(currentUrl + "/error");

            var error = queryNameValuePairs.FirstOrDefault(o => o.Key == "error").Value;
            if (!string.IsNullOrWhiteSpace(error))
            {
                string log = queryNameValuePairs.Aggregate("Live OAuth error: ",
                    (current, pair) => current + (pair.Key + " " + pair.Value + ", "));
                CFDGlobal.LogLine(log);

                //return "ERROR";
                return errorResponse;
            }

            var oauth_token = queryNameValuePairs.FirstOrDefault(o => o.Key == "oauth_token").Value;

            if (!string.IsNullOrWhiteSpace(oauth_token))
            {
                var bytes = Convert.FromBase64String(oauth_token);

                var decryptEngine = new Pkcs1Encoding(new RsaEngine());
                using (var txtreader = new StringReader(CFDGlobal.OAUTH_TOKEN_PUBLIC_KEY))
                {
                    var keyParameter = (AsymmetricKeyParameter)new PemReader(txtreader).ReadObject();
                    decryptEngine.Init(false, keyParameter);
                }

                var decrypted = Encoding.UTF8.GetString(decryptEngine.ProcessBlock(bytes, 0, bytes.Length));

                var split = decrypted.Split(':');
                var username1 = split[0];
                var username2 = split[1];//ayondo username
                var expiry = split[2];
                var checksum = split[3];

                // check if cfd userid and ayondo username are bound
                var state = queryNameValuePairs.FirstOrDefault(o => o.Key == "state").Value;
                var userId = Convert.ToInt32(state);
                var user = db.Users.FirstOrDefault(o => o.Id == userId);
                if (user == null || user.AyLiveUsername != username2)
                {
                    CFDGlobal.LogLine("cfd user id and ayondo live username doesn't match");
                    return errorResponse;
                }

                //using (var client = new AyondoTradeClient())
                //{
                //    var account = client.LoginOAuth(username2, oauth_token);

                //    CFDGlobal.LogLine("OAuth login: " + username2 + " " + account);
                //}

                //return "OK";
                var okResponse = Request.CreateResponse(HttpStatusCode.Redirect);
                okResponse.Headers.Location = new Uri(currentUrl + "/ok");
                return okResponse;
            }

            return errorResponse;
        }

        [HttpGet]
        [Route("live/oauth/ok")]
        public string AyondoLiveOAuthOK()
        {
            return "OK";
        }

        [HttpGet]
        [Route("live/oauth/error")]
        public string AyondoLiveOAuthError()
        {
            return "ERROR";
        }

        private const string LIFECYCLE_CALLBACK_AUTH_TOKEN = "Tj3Id8N7mG6Dyi9Pl1Se4b7dNMik9N0sz1V5sM8cT3we8x9PoqcW3N7dV61cD5J2Ur3Qjf8yTd3EG0UX3";

        [HttpPut]
        [Route("live/lifecycle")]
        public LifecycleCallbackDTO AyondoLiveAccountLifecycleCallback(LifecycleCallbackFormDTO form)
        {
            var authorization = Request.Headers.Authorization;

            //if (authorization != null)
            //    CFDGlobal.LogWarning("Lifecycle Callback header: " + authorization.Scheme + " " + authorization.Parameter);

            if (authorization==null || authorization.Parameter == null || authorization.Parameter != LIFECYCLE_CALLBACK_AUTH_TOKEN)
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "invalid auth token"));

            if (form != null)
            {
                CFDGlobal.LogWarning("Lifecycle Callback form: " + (form.Guid ?? "") + " " + (form.Status ?? ""));

                if (!string.IsNullOrWhiteSpace(form.Guid) && !string.IsNullOrWhiteSpace(form.Status))
                {
                    var user = db.Users.FirstOrDefault(o => o.AyLiveAccountGuid == form.Guid);
                    if (user != null)
                    {
                        user.AyLiveAccountStatus = form.Status;
                        db.SaveChanges();
                    }
                }
            }

            return new LifecycleCallbackDTO();
        }
    }
}