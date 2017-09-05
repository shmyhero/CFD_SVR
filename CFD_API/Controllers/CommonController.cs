using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using Newtonsoft.Json.Linq;
using CFD_COMMON;
using CFD_API.Controllers.Attributes;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/common")]
    public class CommonController : CFDController
    {
        public CommonController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [HttpGet]
        [Route("area")]
        public List<AreaDTO> GetAreas(int id)
        {
            List<AreaDTO> areas = null;

            if (id < 0)
            {
                areas = db.Areas.Select(o => new AreaDTO { Id = o.Id, ParentId = o.ParentID, Name = o.Name, ShortName = o.ShortName }).ToList();
            }
            else
            {
                areas = db.Areas.Where(o => o.ParentID == id).OrderBy(o => o.Sort).Select(o => new AreaDTO { Id = o.Id, ParentId = o.ParentID, Name = o.Name, ShortName = o.ShortName }).ToList();
            }

            return areas;
        }

        [HttpGet]
        [Route("banks")]
        public List<BankDTO> GetBanks()
        {
            var banks = db.Banks.Where(o => o.ExpiredAt.HasValue && o.ExpiredAt.Value == SqlDateTime.MaxValue.Value).OrderBy(o => o.Order).Select(o => new BankDTO()
            {
                cname = o.CName,
                logo = o.Logo
            });

            return banks.ToList();
        }

        /// <summary>
        /// 出金设置，包括了最小费用金额，费用百分比和出金预期时间
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("setting/withdraw")]
        public RefundSettingDTO refundETA()
        {
            Misc refundSetting = db.Miscs.OrderByDescending(o => o.Id).FirstOrDefault(o => o.Key == "RefundETA");

            if (refundSetting != null)
            {
                var setting = JObject.Parse(refundSetting.Value);
                return new RefundSettingDTO() { eta = setting["etaStr"].Value<string>(), charge = new RefundChargeDTO() { minimum = setting["charge"]["min"].Value<decimal>(), rate = setting["charge"]["rate"].Value<decimal>() } };
            }
            else
            {
                return new RefundSettingDTO { eta = "3-5", charge = new RefundChargeDTO() { minimum = 0, rate = 0 } };
            }

        }

        /// <summary>
        /// 入金设置
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("setting/deposit")]
        public DepositSettingDTO GetDepositSetting()
        {
            Misc refundSetting = null;
            CFDGlobal.LogInformation("deposit setting request uri " + Request.RequestUri.Host);
            if (Request.RequestUri.Host == "api.typhoontechnology.hk")
            {
                refundSetting = db.Miscs.OrderByDescending(o => o.Id).FirstOrDefault(o => o.Key == "Deposit");
            }
            else
            {
                refundSetting = db.Miscs.OrderByDescending(o => o.Id).FirstOrDefault(o => o.Key == "DepositStaging");
            }

            var Banks = GetBanks();

            if (refundSetting != null)
            {
                var setting = JObject.Parse(refundSetting.Value);
                return new DepositSettingDTO() { minimum = setting["min"].Value<decimal>(), alipay = setting["alipay"].Value<string>(), alipayPing = setting["alipay_ping"].Value<string>(),  alipayMax = setting["alipayMax"].Value<decimal>(), alipayMaxPing = setting["alipayMax_ping"].Value<decimal>(), alipayMin = setting["alipayMin"].Value<decimal>(), alipayMinPing = setting["alipayMin_ping"].Value<decimal>(), cupMax = setting["cupMax"].Value<decimal>(), cupMin = setting["cupMin"].Value<decimal>(), fxRate = FxRate("CNYUSD"), banks = Banks, charge = new DepositChargeDTO() { minimum = setting["charge"]["min"].Value<decimal>(), rate = setting["charge"]["rate"].Value<decimal>() } };
            }
            else
            {
                return new DepositSettingDTO { minimum = 100M, alipayMax=50M, alipayMin = 50M, cupMax = 20000M, cupMin = 50M, alipay= "单笔固定50美元", fxRate = FxRate("CNYUSD"), banks = Banks, charge = new DepositChargeDTO() { minimum = 0, rate = 0 } };
            }
        }

        [HttpGet]
        [Route("setting/publishterms/{version}")]
        public DataPublishDTO GetPublishTerms(int version)
        {
            DataPublishDTO dto = new DataPublishDTO() { version = version };
            var setting = db.Miscs.FirstOrDefault(m => m.Key == "PublishTerms");
            if (setting == null)
                return dto;

            var settingObj = JObject.Parse(setting.Value);
            int savedVersion = settingObj["version"].Value<int>();

            //更新过，就返回新的Terms和新的版本号
            if(savedVersion > version)
            {
                dto.version = savedVersion;
                dto.terms = new List<string>();
                var terms = (settingObj["terms"] as JArray);
                foreach (var term in terms)
                {
                    dto.terms.Add(term.Value<string>());
                }
            }

            return dto;
        }

        [HttpGet]
        [Route("uri")]
        public string GetRequestUri()
        {
            return Request.RequestUri.Host;
        }

        [HttpGet]
        [Route("fxrate")]
        public decimal FxRate(string fxType)
        {
            decimal fxRate = getWeCollectFxRate();
            switch (fxType)
            {
                case "USDCNY": break;
                case "CNYUSD": fxRate = 1 / fxRate; break;
            }
            return fxRate;
        }

        private decimal getWeCollectFxRate()
        {
            string url = string.Format("{0}getrate?symbol=USDCNY&merchantid={1}", ConfigurationManager.AppSettings["WecollectAPI"], ConfigurationManager.AppSettings["WecollectMerchantID"]);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            try
            {
                request.ContentType = "application/json";
                request.Method = "GET";
                request.Timeout = int.MaxValue;

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream streamReceive = response.GetResponseStream();
                Encoding encoding = Encoding.UTF8;

                StreamReader streamReader = new StreamReader(streamReceive, encoding);
                string strResult = streamReader.ReadToEnd();
                var jsonObj = JObject.Parse(strResult);
                int status = jsonObj["status"].Value<int>();
                if (status != 0)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "获取汇率失败"));
                }
                decimal rate = jsonObj["rate"].Value<JObject>()["USDCNY"].Value<decimal>();
                Console.WriteLine(strResult);
                return rate;
            }
            catch (WebException webEx)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "获取汇率失败"));
            }
            catch (Exception ex)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "获取汇率失败"));
            }
            finally
            {
                request.Abort();
            }
        }

        /// <summary>
        /// 根据用户情况(是否开通过实盘)，显示模拟/实盘活动广告
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("activity")]
        [BasicAuth]
        public ActivityDTO GetActivity(bool isLive=false)
        {
            ActivityDTO dto = new ActivityDTO();

            if(!isLive)
            {
                var demoAct = db.Miscs.FirstOrDefault(m=>m.Key=="DemoActivity");
                if(demoAct!=null)
                {
                    var jObj = JObject.Parse(demoAct.Value);
                    dto.id = jObj["id"].Value<int>();
                    dto.name = jObj["name"].Value<string>();
                    dto.picUrl = jObj["picUrl"].Value<string>();
                    dto.pageUrl = jObj["pageUrl"].Value<string>();
                }
            }
            else
            {
                var liveAct = db.Miscs.FirstOrDefault(m => m.Key == "LiveActivity");
                if (liveAct != null)
                {
                    var jObj = JObject.Parse(liveAct.Value);
                    dto.id = jObj["id"].Value<int>();
                    dto.name = jObj["name"].Value<string>();
                    dto.picUrl = jObj["picUrl"].Value<string>();
                    dto.pageUrl = jObj["pageUrl"].Value<string>();
                }
            }

            return dto;
        }
    }
}
