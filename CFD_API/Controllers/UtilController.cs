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
using CFD_API.Controllers.Attributes;
using CFD_API.Caching;
using CFD_COMMON.Models.Cached;
using Newtonsoft.Json.Linq;
using System.Configuration;
using System.Drawing;
using CFD_API.DTO.Form;
using ServiceStack.Text;
using System.Threading;

namespace CFD_API.Controllers
{
    [RoutePrefix("api")]
    public class UtilController : CFDController
    {
        public UtilController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [HttpGet]
        [Route("version")]
        public VersionDTO GetVersion()
        {
            var version = db.Versions.FirstOrDefault();

            return Mapper.Map<VersionDTO>(version);
        }

        [HttpGet]
        [Route("version/ios")]
        public VersionIOSDTO GetVersionIOS()
        {
            var version = db.Versions.FirstOrDefault();

            return Mapper.Map<VersionIOSDTO>(version);
        }

        [HttpGet]
        [Route("version/android")]
        public VersionAndroidDTO GetVersionAndroid()
        {
            var version = db.Versions.FirstOrDefault();

            return Mapper.Map<VersionAndroidDTO>(version);
        }

        [HttpGet]
        [Route("timestampNonce")]
        public TimeStampDTO GetTimeStamp()
        {
            long timeStamp = DateTime.Now.ToUnixTime();
            int nonce = new Random(DateTime.Now.Millisecond).Next(0, 100000);

            db.TimeStampNonces.Add(new TimeStampNonce() { TimeStamp = timeStamp, Nonce = nonce, CreatedAt = DateTime.UtcNow, Expiration = SqlDateTime.MaxValue.Value });
            db.SaveChanges();
            return new TimeStampDTO() { timeStamp = timeStamp, nonce = nonce };
        }

        [HttpGet]
        [Route("timestampCaptcha")]
        public TimeStampDTO GetTimestampCaptcha()
        {
            long timeStamp = DateTime.Now.ToUnixTime();
            int nonce = new Random(DateTime.Now.Millisecond).Next(0, 100000);


            var code = Randoms.GetRandomAlphanumericCaptchaString(4);
            int randAngle = 45; //随机转动角度
            Bitmap map = new Bitmap(code.Length*16, 22); //创建图片背景
            Graphics graph = Graphics.FromImage(map);

            Random rand = new Random();

            Color[] cBackground =
            {
                Color.AliceBlue, Color.Azure, Color.Beige, Color.BlanchedAlmond, Color.Cornsilk, Color.FloralWhite, Color.LemonChiffon,
                Color.PapayaWhip
            };
            graph.Clear(cBackground[rand.Next(cBackground.Length)]); //清除画面，填充背景
            //graph.DrawRectangle(new Pen(Color.Black, 0), 0, 0, map.Width - 1, map.Height - 1);//画一个边框
            //graph.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;//模式

            //背景噪点生成
            Pen blackPen = new Pen(Color.LightGray, 0);
            for (int i = 0; i < 50; i++)
            {
                int x = rand.Next(0, map.Width);
                int y = rand.Next(0, map.Height);
                graph.DrawRectangle(blackPen, x, y, 1, 1);
            }


            //验证码旋转，防止机器识别
            char[] chars = code.ToCharArray(); //拆散字符串成单字符数组

            //文字距中
            StringFormat format = new StringFormat(StringFormatFlags.NoClip)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            //定义颜色
            Color[] cFont =
            {
                Color.Black, Color.Red, Color.DarkBlue, Color.Green, Color.Orange, Color.Brown, Color.DarkCyan,
                Color.Purple
            };
            //定义字体
            string[] font = {"Verdana", "Microsoft Sans Serif", "Comic Sans MS", "Arial", "宋体"};
            int cindex = rand.Next(cFont.Length);

            for (int i = 0; i < chars.Length; i++)
            {
                int findex = rand.Next(font.Length);

                Font f = new Font(font[findex], 14, FontStyle.Bold); //字体样式(参数2为字体大小)
                Brush b = new SolidBrush(cFont[cindex]);

                Point dot = new Point(14, 14);
                //graph.DrawString(dot.X.ToString(),fontstyle,new SolidBrush(Color.Black),10,150);//测试X坐标显示间距的
                float angle = rand.Next(-randAngle, randAngle); //转动的度数

                graph.TranslateTransform(dot.X, dot.Y); //移动光标到指定位置
                graph.RotateTransform(angle);
                graph.DrawString(chars[i].ToString(), f, b, 1, 1, format);
                //graph.DrawString(chars[i].ToString(),fontstyle,new SolidBrush(Color.Blue),1,1,format);
                graph.RotateTransform(-angle); //转回去
                graph.TranslateTransform(-2, -dot.Y); //移动光标到指定位置，每个字符紧凑显示，避免被软件识别
            }
            //生成图片
            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            map.Save(ms, System.Drawing.Imaging.ImageFormat.Gif);
            //Response.ClearContent();
            //Response.ContentType = "image/gif";
            //Response.BinaryWrite(ms.ToArray());
            graph.Dispose();
            map.Dispose();

            byte[] byteImage = ms.ToArray();
            var base64String = Convert.ToBase64String(byteImage, Base64FormattingOptions.None);


            db.TimeStampNonces.Add(new TimeStampNonce()
            {
                TimeStamp = timeStamp,
                Nonce = nonce,
                CreatedAt = DateTime.UtcNow,
                Expiration = SqlDateTime.MaxValue.Value,
                CaptchaCode = code,
                CaptchaImg = base64String
            });
            db.SaveChanges();
            return new TimeStampDTO() {timeStamp = timeStamp, nonce = nonce, captchaImg = base64String};
        }

        [HttpGet]
        [Route("ipCheck")]
        public bool IpCheck()
        {
            var ipCheck = db.SystemSettings.FirstOrDefault().IpCheck;
            if (!ipCheck)
                return true;

            string ip = null;
            if (Request.Properties.ContainsKey("MS_HttpContext"))
            {
                var requestBase = ((HttpContextWrapper) Request.Properties["MS_HttpContext"]).Request;
                ip = requestBase.UserHostAddress;

                if (ip == "212.36.187.202" || ip == "84.19.42.150") //Sheng Xu, ayondo
                    return true;


                var record =
                    db.IP2Country.SqlQuery(
                        "SELECT TOP 1 * FROM IP2Country WHERE StartAddress <= @p0 ORDER BY StartAddress DESC",
                        IPAddress.Parse(ip).MapToIPv6().GetAddressBytes()).FirstOrDefault();
                if (record != null && record.CountryCode == "CN")
                    return true;

            }

            return false;
        }

        /// <summary>
        /// 检查手机号是否已注册过模拟盘
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        [Route("phone")]
        [HttpPost]
        public ResultDTO CheckPhoneNumber(CheckPhoneDTO  form)
        {
            if (db.Users.Any(u => u.Phone == form.phone))
            {
                return new ResultDTO() { success = false, message = "手机号已注册" };
            }

            return new ResultDTO() { success = true, message = "OK" };
        }

        /// <summary>
        /// for test only
        /// </summary>
        /// <param name="phone"></param>
        /// <returns></returns>
        [Route("verifyCode")]
        [HttpGet]
        [IPAuth]
        public List<VerifyCode> GetVerifyCode()
        {
            var verifyCodes = db.VerifyCodes.OrderByDescending(o => o.SentAt).Take(200).ToList();
            return verifyCodes;
        }

        [Route("sendCode")]
        [HttpPost]
        [IgnoreBrowserRequest]
        public ResultDTO SendCode(string phone)
        {
            return CheckAndSendSMSVerifyCode(phone);
            //return new ResultDTO(true);
        }

        private ResultDTO CheckAndSendSMSVerifyCode(string phone)
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
                CFDGlobal.RetryMaxOrThrow(() => YunPianMessenger.TplSendCodeSms(string.Format("#code#={0}", code), phone),
                    sleepMilliSeconds: 0);

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

        [Route("sendVerifyCode")]
        [HttpPost]
        //[RequireHttps]
        [TimestampNonceAuth]
        public ResultDTO SendVerifyCode(string phone)
        {
            return CheckAndSendSMSVerifyCode(phone);
        }

        [Route("sendSMSCode")]
        [HttpPost]
        //[RequireHttps]
        [TimestampCaptchaAuth]
        public ResultDTO SendSMSCode(string phone)
        {
            return CheckAndSendSMSVerifyCode(phone);
        }

        /// <summary>
        /// 给推荐人用的验证码
        /// 在生成验证码之前先判断是否已经注册过模拟盘
        /// </summary>
        /// <param name="phone"></param>
        /// <returns></returns>
        [Route("sendReferCode")]
        [HttpPost]
        [TimestampCaptchaAuth]
        public ResultDTO SendReferCode(string phone)
        {
            if(db.Users.Any(u => u.Phone == phone))
            {
                return new ResultDTO() { success = false, message = "手机号已被注册" };
            }

            return CheckAndSendSMSVerifyCode(phone);
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
                headlines = db.Headlines.Where(item => item.Id == id && item.Expiration.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.CreatedAt).ToList();
            }

            //return headlines.Select(o => Mapper.Map<HeadlineDTO>(o)).ToList();
            return headlines.Select(o => new HeadlineDTO() {
                id = o.Id,
                header = o.Header,
                body = o.Body,
                image = o.ImgUrl,
                color = o.Color.HasValue ? o.Color.Value : 0,
                createdAt = o.CreatedAt,
                language = o.Language
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
            var languages = Translator.GetCultureNamesByThreadCulture();
            headlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && languages.Contains(item.Language) && item.CreatedAt >= lastDay).OrderByDescending(o => o.CreatedAt).Take(maxLines).ToList();

            while(headlines == null || headlines.Count == 0)
            {
                lastDay = lastDay.AddDays(-1);
                headlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && languages.Contains(item.Language) && item.CreatedAt >= lastDay).OrderByDescending(o => o.CreatedAt).Take(maxLines).ToList();

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
                createdAt = o.CreatedAt,
                language = o.Language
            }).ToList();
        }

        /// <summary>
        /// 返回页数*10的记录，且不超过7天（有数据的天数）。
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("headline/group/{page}")]
        public IList<HeadlineGroupDTO> GetHeadlineGroupPaging(int page)
        {
            if(page<1 || page > 10)
            {
                return new List<HeadlineGroupDTO>();
            }

            List<Headline> headlines = new List<Headline>();
            //find past 7 days which has headlines
            //int maxDays = 7;
            int maxHeadlines = page * 10; //总共需要返回的条数
            int skipCount = (page - 1) * 10; //跳过前N条

            DateTime chinaToday = DateTime.UtcNow.AddHours(8);
            List<HeadlineGroupDTO> headlinesGroup = new List<HeadlineGroupDTO>();
            var languages = Translator.GetCultureNamesByThreadCulture();
            var tempHeadlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && languages.Contains(item.Language)).OrderByDescending(o => o.CreatedAt).Skip(skipCount).Take(maxHeadlines - skipCount).ToList();
            if (tempHeadlines != null && tempHeadlines.Count > 0) 
            {
                headlines.AddRange(tempHeadlines);
            }

            //while(maxDays > 0)
            //{
            //    DateTime chinaLastDay = chinaToday.AddDays(-1);
            //    var tempHeadlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && item.CreatedAt >= chinaLastDay && item.CreatedAt <= chinaToday).OrderByDescending(o => o.CreatedAt).ToList();
            //    chinaToday = chinaToday.AddDays(-1);

            //    if (tempHeadlines != null && tempHeadlines.Count > 0) // find those days which has headline (total 7 days)
            //    {
            //        maxDays--;
            //        if (headlines != null)
            //        {
            //            headlines.AddRange(tempHeadlines);
            //        }
            //        else
            //        {
            //            headlines = tempHeadlines;
            //        }
            //    }

            //}

            foreach (Headline headLine in headlines)
            {
                HeadlineGroupDTO headlineGroupDTO = headlinesGroup.FirstOrDefault(item => item.createdDay == headLine.CreatedAt.Value.ToString("yyyy-MM-dd"));
                if (headlineGroupDTO != null)
                {
                    headlineGroupDTO.headlines.Add(new HeadlineDTO() { id = headLine.Id, header = headLine.Header, body = headLine.Body, createdAt = headLine.CreatedAt, color = headLine.Color.HasValue? headLine.Color.Value : 0, image = headLine.ImgUrl, language = headLine.Language });
                }
                else
                {
                    headlineGroupDTO = new HeadlineGroupDTO();
                    headlineGroupDTO.createdDay = headLine.CreatedAt.Value.ToString("yyyy-MM-dd");
                    headlineGroupDTO.headlines = new List<HeadlineDTO>();
                    headlineGroupDTO.headlines.Add(new HeadlineDTO() { id = headLine.Id, header = headLine.Header, body = headLine.Body, createdAt = headLine.CreatedAt, color = headLine.Color.HasValue ? headLine.Color.Value : 0, image = headLine.ImgUrl, language = headLine.Language });
                    headlinesGroup.Add(headlineGroupDTO);
                }
            }

            return headlinesGroup;
        }

        [HttpGet]
        [Route("headline/group")]
        public IList<HeadlineGroupDTO> GetHeadlineGroup()
        {
            List<Headline> headlines = null;
            //find past 2 days which has headlines
            int maxDays = 2;

            DateTime chinaToday = DateTime.UtcNow.AddHours(8);
            List<HeadlineGroupDTO> headlinesGroup = new List<HeadlineGroupDTO>();

            while (maxDays > 0)
            {
                DateTime chinaLastDay = chinaToday.AddDays(-1);
                var languages = Translator.GetCultureNamesByThreadCulture();
                var tempHeadlines = db.Headlines.Where(item => item.Expiration.Value == SqlDateTime.MaxValue.Value && languages.Contains(item.Language) && item.CreatedAt >= chinaLastDay && item.CreatedAt <= chinaToday).OrderByDescending(o => o.CreatedAt).ToList();
                chinaToday = chinaToday.AddDays(-1);

                if (tempHeadlines != null && tempHeadlines.Count > 0) // find those days which has headline (total 2 days)
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
                    headlineGroupDTO.headlines.Add(new HeadlineDTO() { id = headLine.Id, header = headLine.Header, body = headLine.Body, createdAt = headLine.CreatedAt, color = headLine.Color.HasValue ? headLine.Color.Value : 0, image = headLine.ImgUrl, language = headLine.Language });
                }
                else
                {
                    headlineGroupDTO = new HeadlineGroupDTO();
                    headlineGroupDTO.createdDay = headLine.CreatedAt.Value.ToString("yyyy-MM-dd");
                    headlineGroupDTO.headlines = new List<HeadlineDTO>();
                    headlineGroupDTO.headlines.Add(new HeadlineDTO() { id = headLine.Id, header = headLine.Header, body = headLine.Body, createdAt = headLine.CreatedAt, color = headLine.Color.HasValue ? headLine.Color.Value : 0, image = headLine.ImgUrl, language = headLine.Language });
                    headlinesGroup.Add(headlineGroupDTO);
                }
            }

            return headlinesGroup;
        }

        //private List<string> GetLanguageByCulture()
        //{
        //    List<string> languages = new List<string>();
        //    if (Thread.CurrentThread.CurrentUICulture.Name == CFDGlobal.CULTURE_CN)
        //    {
        //        languages.AddRange(new string[] { null, CFDGlobal.CULTURE_CN });
        //    }
        //    else
        //    {
        //        languages.Add(CFDGlobal.CULTURE_EN);
        //    }

        //    return languages;
        //}

        ///// <summary>
        ///// 无验证信息或验证信息错误时返回0，否则返回UserID
        ///// </summary>
        ///// <returns></returns>
        //public int GetUserID()
        //{
        //    if(HttpContext.Current.Request.Headers.AllKeys.Contains("Authorization"))
        //    {
        //        string auth = HttpContext.Current.Request.Headers["Authorization"];
        //        var authArray = auth.Split(' ');
        //        if(authArray.Length !=2)
        //        {
        //            return 0;
        //        }

        //        var tokenArray = authArray[1].Split('_');
        //        if(tokenArray.Length != 2)
        //        {
        //            return 0;
        //        }

        //        string userIdStr = tokenArray[0];
        //        int userId = 0;
        //        int.TryParse(userIdStr, out userId);

        //        return userId;
        //    }

        //    return 0;
        //}

        //public decimal GetLastPrice(ProdDef prodDef)
        //{

        //    var quotes = WebCache.GetInstance(IsLiveUrl).Quotes.Where(o => o.Id == prodDef.Id).ToList();
        //    //var prodDefs = redisProdDefClient.GetByIds(ids);
        //    var quote = quotes.FirstOrDefault(o => o.Id == prodDef.Id);
        //    if (quote != null)
        //    {
        //        return Quotes.GetLastPrice(quote);
        //    }

        //    return 0;
        //}

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
        [Route("IOSEffectDay")]
        public string GetIOSEffectDate()
        {
            Misc iosEffectDay = db.Miscs.OrderByDescending(o => o.Id).FirstOrDefault(o => o.Key == "ISOEffectDate");
            if (iosEffectDay != null)
            {
                return iosEffectDay.Value;
            }
            else
            {
                return DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");
            }
        }
    }
}