using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Azure;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_API.Controllers.Attributes;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/admin")]
    public class AdminController : CFDController
    {
        public AdminController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
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
        [Route("feedback/anonymous")]
        public List<FeedBackFormDTO_Pic> GetAnonymousFeedBacks()
        {
            List<Feedback> feedbacks = db.Feedbacks.OrderByDescending(o => o.Id).Where(x => string.IsNullOrEmpty(x.Phone)).Take(20).ToList();

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
            List<Feedback> feedbacks = db.Feedbacks.Where(o => o.Id < id).OrderByDescending(o => o.Time).Take(20).ToList();

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
            List<Feedback> feedbacks = db.Feedbacks.Where(x => x.Phone.Contains(number)).OrderByDescending(o => o.Id).Take(20).ToList();

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

        [Route("operation/login")]
        [HttpPost]
        [AdminAuth]
        public string Login(OperationUserDTO userDTO)
        {
            int userType = 0;
            int.TryParse(userDTO.Type, out userType);
            OperationUser user = db.OperationUsers.FirstOrDefault(u => (u.UserName == userDTO.name) && (u.Password == userDTO.password) && (u.UserType == userType));

            if (user != null)
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
        [AdminAuth]
        public HttpResponseMessage PostHeadline(HeadlineDTO headLineDTO)
        {
            if (headLineDTO.id > 0) //update
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

        [Route("headline/{id}")]
        [HttpDelete]
        [AdminAuth]
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

            if (!string.IsNullOrEmpty(headLineDTO.image))
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
            if (headlines != null && headlines.Count > 0)
            {
                var headline = headlines.FirstOrDefault();

                if (!string.IsNullOrEmpty(headLineDTO.image))
                {
                    string picName = string.Empty;
                    if (!string.IsNullOrEmpty(headline.ImgUrl)) //delete existing blob before upload
                    {
                        picName = headline.ImgUrl.Split('/').Last();
                        Blob.DeleteBlob(CFDGlobal.HEADLINE_PIC_BLOB_CONTAINER, picName);
                    }

                    if (string.IsNullOrEmpty(picName))
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
    }
}
