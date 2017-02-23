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
                return new RefundSettingDTO() { eta = setting["eta"].Value<int>(), charge = new RefundChargeDTO() { minimum = setting["charge"]["min"].Value<decimal>(), rate = setting["charge"]["rate"].Value<decimal>() } };
            }
            else
            {
                return new RefundSettingDTO { eta = 3, charge = new RefundChargeDTO() { minimum = 0, rate = 0 } };
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
            Misc refundSetting = db.Miscs.OrderByDescending(o => o.Id).FirstOrDefault(o => o.Key == "Deposit");

            var Banks = GetBanks();

            if (refundSetting != null)
            {
                var setting = JObject.Parse(refundSetting.Value);
                return new DepositSettingDTO() { minimum = setting["min"].Value<decimal>(), fxRate = FxRate("CNYUSD"), banks = Banks, charge = new DepositChargeDTO() { minimum = setting["charge"]["min"].Value<decimal>(), rate = setting["charge"]["rate"].Value<decimal>() } };
            }
            else
            {
                return new DepositSettingDTO { minimum = 100M, fxRate = FxRate("CNYUSD"), banks = Banks, charge = new DepositChargeDTO() { minimum = 0, rate = 0 } };
            }
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
    }
}
