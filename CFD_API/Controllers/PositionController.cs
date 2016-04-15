using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using AyondoTrade.Model;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.DTO.FormDTO;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
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
            var user = GetUser();
            if (string.IsNullOrEmpty(user.AyondoUsername))
                throw new Exception("user do not have an ayondo account");

            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");

            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            var result = clientHttp.GetPositionReport(user.AyondoUsername, user.AyondoPassword);

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

                var security = Mapper.Map<SecurityDTO>(dbSec);

                if (prodDef != null)
                {
                    security.preClose = prodDef.PreClose;
                    security.open = prodDef.OpenAsk;
                    security.isOpen = prodDef.QuoteType == enmQuoteType.Open;
                }

                if (quote != null)
                {
                    security.last = quote.Offer;
                }

                var posDTO = new PositionDTO()
                {
                    id = report.PosMaintRptID,
                    isLong = report.LongQty != null,
                    settlePrice = report.SettlPrice,
                    invest = 0,
                    leverage = 1/dbSec.BaseMargin.Value,
                    createAt = report.CreateTime,
                    security = security,
                    quantity = report.LongQty ?? report.ShortQty.Value,
                };

                return posDTO;
            }).ToList();

            return positionDtos;
        }

        [HttpPost]
        [Route("")]
        [BasicAuth]
        public PositionDTO NewPosition(NewPositionFormDTO form)
        {
            var user = GetUser();
            if (string.IsNullOrEmpty(user.AyondoUsername))
                throw new Exception("user do not have an ayondo account");

            var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);

            if (security == null)
                throw new Exception("security not found");

            var redisQuoteClient = RedisClient.As<Quote>();
            var quote = redisQuoteClient.GetById(form.securityId);

            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");

            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            //*******************************************************
            //trade value = lot size * quantity * quote price
            //invest = trade value * margin rate
            //*******************************************************
            //var tradeValue = form.invest/security.BaseMargin;
            //var quantity = tradeValue/security.LotSize/(form.isLong ? quote.Offer : quote.Bid);
            var result = clientHttp.NewOrder(user.AyondoUsername, user.AyondoPassword, form.securityId, form.isLong,
                form.isLong ? security.MinSizeLong.Value : security.MinSizeShort.Value);

            var posDTO = new PositionDTO()
            {
                id = result.PosMaintRptID,
                isLong = result.LongQty != null,
                settlePrice = result.SettlPrice,
                invest = 0,
                leverage = 1/security.BaseMargin.Value,
                createAt = result.CreateTime,
                quantity = result.LongQty ?? result.ShortQty.Value,
            };

            return posDTO;
        }

        [HttpPost]
        [Route("net")]
        [BasicAuth]
        public PositionDTO NetPosition(NetPositionFormDTO form)
        {
            var user = GetUser();
            if (string.IsNullOrEmpty(user.AyondoUsername))
                throw new Exception("user do not have an ayondo account");

            var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);

            if (security == null)
                throw new Exception("security not found");

            //var redisQuoteClient = RedisClient.As<Quote>();
            //var quote = redisQuoteClient.GetById(form.securityId);

            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");

            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            //*******************************************************
            //trade value = lot size * quantity * quote price
            //invest = trade value * margin rate
            //*******************************************************
            //var tradeValue = form.invest/security.BaseMargin;
            //var quantity = tradeValue/security.LotSize/(form.isLong ? quote.Offer : quote.Bid);
            var result = clientHttp.NewOrder(user.AyondoUsername, user.AyondoPassword, form.securityId, !form.isPosLong, form.posQty, form.posId);

            var posDTO = new PositionDTO()
            {
                id = result.PosMaintRptID,
                isLong = result.LongQty != null,
                settlePrice = result.SettlPrice,
                invest = 0,
                leverage = 1/security.BaseMargin.Value,
                createAt = result.CreateTime,
                quantity = result.LongQty ?? result.ShortQty.Value,
            };

            return posDTO;
        }
    }
}