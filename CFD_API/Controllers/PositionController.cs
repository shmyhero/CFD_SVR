using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using AyondoTrade.FaultModel;
using AyondoTrade.Model;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.DTO.FormDTO;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
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
                    security.open =Quotes.GetOpenPrice(prodDef);
                    security.isOpen = prodDef.QuoteType == enmQuoteType.Open;
                }

                if (quote != null)
                {
                    security.last =Quotes.GetLastPrice( quote);
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
                    upl = report.UPL,
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

            var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();

            //var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);

            //if (security == null)
            //    throw new Exception("security not found");

            var prodDef = redisProdDefClient.GetById(form.securityId);
            if (prodDef == null)
                throw new Exception("security not found");

            var tradeValueUSD = form.invest*form.leverage;

            //************************************************************************
            //TradeValue (to ccy2) = QuotePrice * (1 / MDS_PLUNITS * MDS_LOTSIZE) * quantity
            //************************************************************************

            decimal tradeValueCcy2;
            if (prodDef.Ccy2 == "USD")
            {
                tradeValueCcy2 = tradeValueUSD;
            }
            else
            {
                //get fxRate and convert 
                //the fx for convertion! not the fx that is being bought!
                var fxConverterProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == "USD" + prodDef.Ccy2);

                if (fxConverterProdDef == null)
                    throw new Exception("Cannot find fx rate: " + "USD" + "/" + prodDef.Ccy2);

                var fxConverterQuote = redisQuoteClient.GetById(fxConverterProdDef.Id);
                var fxConverterRate = (fxConverterQuote.Bid + fxConverterQuote.Offer)/2;

                tradeValueCcy2 = tradeValueUSD*fxConverterRate;
            }
            var quote = redisQuoteClient.GetById(form.securityId);
            var quotePrice = form.isLong ? quote.Offer : quote.Bid;
            decimal quantity = tradeValueCcy2/(quotePrice/prodDef.PLUnits*prodDef.LotSize);
            decimal stopPx = form.isLong ? quotePrice * (1 - 1 / form.leverage) : quotePrice * (1 + 1 / form.leverage);

            CFDGlobal.LogLine("NewOrder: userId:" + UserId + " secId:" + form.securityId + " long:" + form.isLong + " invest:" + form.invest + " leverage:" + form.leverage +
                              "|quantity:" + quantity + " stopPx:" + stopPx);

            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");
            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            PositionReport result;
            try
            {
                result = clientHttp.NewOrder(user.AyondoUsername, user.AyondoPassword, form.securityId, form.isLong,
                    //form.isLong ? security.MinSizeLong.Value : security.MinSizeShort.Value
                    quantity,
                    leverage: form.leverage,
                    stopPx: stopPx
                    );
            }
            catch (FaultException<OrderRejectedFault> e)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                    __(TransKey.ORDER_REJECTED) + " " + Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text)));
            }

            var posDTO = new PositionDTO()
            {
                id = result.PosMaintRptID,
                isLong = result.LongQty != null,
                settlePrice = result.SettlPrice,
                invest = 0,
                leverage = 0,
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

            var redisProdDefClient = RedisClient.As<ProdDef>();
            var prodDef = redisProdDefClient.GetById(form.securityId);
            if (prodDef == null)
                throw new Exception("security not found");

            //var redisQuoteClient = RedisClient.As<Quote>();
            //var quote = redisQuoteClient.GetById(form.securityId);

            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");
            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            PositionReport result;
            try
            {
                result = clientHttp.NewOrder(user.AyondoUsername, user.AyondoPassword, form.securityId, !form.isPosLong, form.posQty, nettingPositionId: form.posId);
            }
            catch (FaultException<OrderRejectedFault> e)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                    __(TransKey.ORDER_REJECTED) + " " + Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text)));
            }

            var posDTO = new PositionDTO()
            {
                id = result.PosMaintRptID,
                isLong = result.LongQty != null,
                settlePrice = result.SettlPrice,
                invest = 0,
                leverage = 0,
                createAt = result.CreateTime,
                quantity = result.LongQty ?? result.ShortQty.Value,
                pl = result.PL,
            };

            return posDTO;
        }
    }
}