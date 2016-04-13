using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using AyondoTrade.Model;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using ServiceStack.Redis;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/position")]
    public class PositionController : CFDController
    {
        public PositionController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
            : base(db, mapper, redisClient)
        {
        }

        [HttpGet]
        [Route("open")]
        [BasicAuth]
        public List<PositionDTO> GetOpenPositions()
        {
            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");

            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            //var r1 = clientTcp.Test("haha tcp");
            //var r2 = clientHttp.Test("haha http");
            var result = clientHttp.GetPositionReport("jiangyi1985", "ivan");

            var secIds = result.Select(o => Convert.ToInt32(o.SecurityID)).ToList();

            var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();

            var dbSecurities = db.AyondoSecurities.Where(o => secIds.Contains(o.Id)).ToList();
            var prodDefs = redisProdDefClient.GetByIds(secIds);
            var quotes = redisQuoteClient.GetByIds(secIds);

            var positionDtos = result.Select(delegate(PositionReport report)
            {
                var dbSec = dbSecurities.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                var prodDef = prodDefs.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                var quote = quotes.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                
                //
                var security = Mapper.Map<SecurityDTO>(dbSec);

                security.preClose = prodDef.PreClose;
                security.open = prodDef.OpenAsk;
                security.isOpen = prodDef.QuoteType == enmQuoteType.Open;

                security.last = quote.Offer;

                //
                var posDTO= new PositionDTO()
                {
                    id = report.PosMaintRptID,
                    isLong = report.LongQty != null,
                    settlePrice = report.SettlPrice,
                    invest = 0,
                    leverage = 1,
                    security = security
                };

                return posDTO;
            }).ToList();

            return positionDtos;
        }
    }
}
