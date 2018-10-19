using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using CFD_API.Caching;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.DTO.Form;
using CFD_COMMON;
using CFD_COMMON.Azure;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using Newtonsoft.Json.Linq;
using ServiceStack.Redis;
using System.Web;
using System.Drawing;
using System.ServiceModel;
using AyondoTrade.Model;
using CFD_COMMON.Utils.Extensions;
using System.Threading.Tasks;
using AyondoTrade.FaultModel;
using EntityFramework.Extensions;
using Newtonsoft.Json;
using ServiceStack.Text;
using System.Data.SqlTypes;
using System.Text;
using CFD_COMMON.IdentityVerify;
using System.Text.RegularExpressions;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/risk")]
    public class RiskController : CFDController
    {
        public RiskController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }

        [HttpGet]
        [Route("user")]
        public List<UserRiskDTO> GetUserRisk(bool ignoreCache = false)
        {
            string queryString =
              @" select u.Nickname,u.AyLiveUsername,ui.Gender,2018-substring(ui.Birthday,0,5) Age,g.* from
(
select  UserId,
count(p.id) posCount,
sum(case when s.AssetClass='Currencies' then 1 else 0 end)/CONVERT(DECIMAL(16,4), count(p.id)) posRaio_fx,
sum(case when s.AssetClass='Stock Indices' then 1 else 0 end)/CONVERT(DECIMAL(16,4), count(p.id)) posRatio_index,
sum(case when s.AssetClass='Cryptocurrencies' then 1 else 0 end)/CONVERT(DECIMAL(16,4), count(p.id)) posRatio_crypto,
sum(case when s.AssetClass='Commodities' then 1 else 0 end)/CONVERT(DECIMAL(16,4), count(p.id)) posRatio_commodity,
sum(case when s.AssetClass='Single Stocks' then 1 else 0 end)/CONVERT(DECIMAL(16,4), count(p.id)) posRatio_stock,
count(longqty) longCount, count(shortqty) shortCount, count(shortqty)/CONVERT(DECIMAL(16,4), count(p.id)) shortRatio,
sum(pl) sum_PL, avg(pl) avg_PL, stdev(pl) stdev_PL, stdev(pl)/avg(pl) cv_PL, 
sum(pl)/sum(investusd) ROI,
sum(investUSD) sum_Invest, avg(investusd) avg_Invest,  stdev(investusd)stdev_Invest, stdev(investusd)/avg(investusd) cv_Invest,
sum(leverage) sum_Leverage, avg(Leverage) avg_Leverage, stdev(Leverage)stdev_Leverage, stdev(Leverage)/avg(Leverage) cv_Leverage,
sum(datediff(SECOND,createtime, closedat)) sum_HoldingTime_second, avg(datediff(SECOND,createtime, closedat)) avg_HoldingTime_second,
stdev(datediff(SECOND,createtime, closedat)) stdev_HoldingTime_second, stdev(datediff(SECOND,createtime, closedat))/avg(datediff(SECOND,createtime, closedat)) cv_HoldingTime_second,
count(stoppx) stopLossSetCount,  count(takePx) takeProfitSetCount,  CONVERT(DECIMAL(16,4), count(stoppx))/count(p.id) stopSetRatio,
sum(investUSD*leverage) sum_TradeValue,
sum((case when LongQty is not null then 1 else -1 end)*InvestUSD) sum_sidedInvest,avg((case when LongQty is not null then 1 else -1 end)*InvestUSD) avg_sidedInvest,
sum((case when LongQty is not null then 1 else -1 end)*InvestUSD*Leverage) sum_sidedTradeValue,avg((case when LongQty is not null then 1 else -1 end)*InvestUSD*Leverage) avg_sidedTradeValue,
min(createtime) firstPosCreateTime,max(closedat) lastPosCloseTime,datediff(DAY,min(createtime),max(closedat)) playTime_day,
CONVERT(DECIMAL(16,4), count(p.id))/datediff(HOUR,min(createtime),max(closedat)) posCreateFrequency_posPerHour

from [NewPositionHistory_live] p
left join AyondoSecurity_Live s on p.SecurityId=s.Id
where closedat is not null
group by userid
) g
left join [user] u on g.userid=u.id
left join userinfo ui on g.userid=ui.UserId

where posCount>1 
and avg_holdingtime_second>100 
and playTime_day>=7
and posCount>10

--avg_holdingtime_second<10

order by sum_pl";

            SqlDataAdapter adapter = new SqlDataAdapter(queryString, CFDGlobal.GetDbConnectionString("CFDEntities"));

            DataSet ds = new DataSet();
            adapter.Fill(ds);
            DataTable dt = ds.Tables[0];
            List<UserRiskDTO> userRisks = new List<UserRiskDTO>();
            foreach (DataRow dr in dt.Rows)
            {
                UserRiskDTO userRiskDTO = new UserRiskDTO();
                userRiskDTO.UserId = (int)dr["UserId"];
                userRiskDTO.NickName = (string)dr["Nickname"];
                userRiskDTO.Leverage = (decimal)dr["avg_Leverage"];
                userRiskDTO.Frequency = (decimal)dr["posCreateFrequency_posPerHour"];
                userRiskDTO.HoldTime = (int)dr["avg_HoldingTime_second"];
                userRiskDTO.Invest = (decimal)(double)dr["cv_Invest"];
                userRiskDTO.PosCount = (int)dr["posCount"];
                userRiskDTO.TotalInvest = (decimal)dr["sum_Invest"];
                userRiskDTO.TotalPL = (decimal)dr["sum_PL"];
                userRiskDTO.AveragePL = (decimal)dr["avg_PL"];
                //Console.WriteLine(userRiskDTO.Index);
                userRisks.Add(userRiskDTO);
            }
            userRisks.Sort((x, y) => y.Index.CompareTo(x.Index));
            return userRisks;
        }

        [HttpGet]
        [Route("position/realtime")]
        [IPAuth]        
        public List<Tuple<int, string, decimal>> GetRealTimeOpenPositionRisk()
        {
            var openPositions = db.NewPositionHistory_live.Where(o => o.ClosedAt == null)
                .ToList();
            List<int> testUserIds = new List<int>() { 5165, 5963, 3256 };
            var query = from p in db.NewPositionHistory_live
                        join u in db.Users on (int)p.UserId equals u.Id
                        where p.ClosedAt == null && p.UserId == u.Id && !testUserIds.Contains(u.Id) && u.Id >= 3202
                        select new RealTimeOpenPositionExposureDTO()
                        {
                            UserId = p.UserId,
                            SecurityId = p.SecurityId,
                            LongQty = p.LongQty,
                            ShortQty = p.ShortQty,
                            SettlePrice = p.SettlePrice,
                            Leverage = p.Leverage,
                            InvestUSD = p.InvestUSD,
                            NickName = u.Nickname
                        };
            List<RealTimeOpenPositionExposureDTO> positionList = query.ToList();
            List<int> securityIds = positionList.Select(x => (int)x.SecurityId).ToList();
            List<ProdDefDTO> proddefList = new SecurityController(this.db, this.Mapper).GetAllList(1, 1000);
            Dictionary<int, decimal> securityPriceDic = new Dictionary<int, decimal>();
            Dictionary<int, string> securityNameDic = new Dictionary<int, string>();
            foreach (var proddef in proddefList)
            {
                if (securityIds.Contains(proddef.Id))
                {
                    securityPriceDic[proddef.Id] = ((decimal)proddef.Bid + (decimal)proddef.Offer) / 2;
                    securityNameDic[proddef.Id] = proddef.cname;
                }
            }
            Dictionary<int, decimal> securityRiskDic = new Dictionary<int, decimal>();
            foreach (RealTimeOpenPositionExposureDTO position in positionList)
            {
                int securityId = (int)position.SecurityId;
                decimal currentPrice = securityPriceDic[securityId];
                if (!securityRiskDic.Keys.Contains(securityId))
                {
                    securityRiskDic[securityId] = 0;
                }
                if (position.LongQty != null)
                {
                    securityRiskDic[securityId] += (decimal)position.Leverage * (decimal)position.InvestUSD * (currentPrice / (decimal)position.SettlePrice - 1);
                }
                else
                {
                    securityRiskDic[securityId] += (decimal)position.Leverage * (decimal)position.InvestUSD * (1 - currentPrice / (decimal)position.SettlePrice);
                }
            }

            List<Tuple<int, string, decimal>> result = new List<Tuple<int, string, decimal>>();
            foreach (KeyValuePair<int, decimal> pair in securityRiskDic)
            {
                result.Add(new Tuple<int, string, decimal>(pair.Key, securityNameDic[pair.Key], pair.Value));
            }
            result.Sort((x, y) => y.Item3.CompareTo(x.Item3));
            return result;
        }

    }

}