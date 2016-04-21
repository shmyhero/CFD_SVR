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

            var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);

            if (security == null)
                throw new Exception("security not found");

            var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();

            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");

            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            var tradeValueInUSD = form.invest*form.leverage;

            decimal quantity = 0;
            decimal stopPx = 0;

            switch (security.AssetClass)
            {
                case "Currencies":
                    decimal lotSizeInUSD;

                    if (security.BaseCcy == "USD")
                        lotSizeInUSD = security.LotSize.Value;
                    else
                    {
                        //get fxRate and convert product's lotSize from its BaseCcy to USD
                        //the fx for convertion! not the fx that is being bought!
                        var fxConverterProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == security.BaseCcy + "USD");

                        if (fxConverterProdDef == null)
                            throw new Exception("Cannot find fx rate: " + security.BaseCcy + "/" + "USD");

                        var fxConverterQuote = redisQuoteClient.GetById(fxConverterProdDef.Id);
                        var fxConverterRate = (fxConverterQuote.Bid + fxConverterQuote.Offer)/2;

                        lotSizeInUSD = security.LotSize.Value*fxConverterRate;
                    }

                    quantity = tradeValueInUSD/lotSizeInUSD;

                    var fxProductQuote = redisQuoteClient.GetById(form.securityId);
                    var fxProductPrice = form.isLong ? fxProductQuote.Offer : fxProductQuote.Bid;
                    stopPx = form.isLong ? fxProductPrice*(1 - 1/form.leverage) : fxProductPrice*(1 + 1/form.leverage);

                    break;

                case "Commodities":
                case "Stock Indices":
                    if (security.PerUnitEquals == null || security.PerUnit == null)
                        throw new Exception("Cannot find PerUnit or PerUnitEquals for this security.");

                    var unitPrice = security.PerUnitEquals/security.PerUnit;
                    var quote = redisQuoteClient.GetById(form.securityId);
                    var quotePrice = form.isLong ? quote.Offer : quote.Bid;
                    var unitQuotePrice = unitPrice*quotePrice;

                    if (security.BaseCcy == "USD")
                    {
                        quantity = tradeValueInUSD/unitQuotePrice.Value;
                    }
                    else
                    {
                        var fxProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == security.BaseCcy + "USD");

                        if (fxProdDef == null)
                            throw new Exception("Cannot find fx rate: " + security.BaseCcy + "/" + "USD");

                        var fxRate = (fxProdDef.Offer + fxProdDef.Bid)/2;
                        var unitQuotePriceInUSD = unitQuotePrice*fxRate;
                        quantity = tradeValueInUSD/unitQuotePriceInUSD.Value;
                    }

                    stopPx = form.isLong ? quotePrice*(1 - 1/form.leverage) : quotePrice*(1 + 1/form.leverage);
                    break;

                case "Single Stocks":
                    var stockQuote = redisQuoteClient.GetById(form.securityId);
                    var stockQuotePrice = form.isLong ? stockQuote.Offer : stockQuote.Bid;

                    if (security.BaseCcy == "USD")
                    {
                        quantity = tradeValueInUSD/stockQuotePrice;
                    }
                    else
                    {
                        var fxProdDef = redisProdDefClient.GetAll().FirstOrDefault(o => o.Symbol == security.BaseCcy + "USD");

                        if (fxProdDef == null)
                            throw new Exception("Cannot find fx rate: " + security.BaseCcy + "/" + "USD");

                        var fxRate = (fxProdDef.Offer + fxProdDef.Bid)/2;
                        var stockQuotePriceInUSD = stockQuotePrice*fxRate;
                        quantity = tradeValueInUSD/stockQuotePriceInUSD.Value;
                    }

                    stopPx = form.isLong ? stockQuotePrice*(1 - 1/form.leverage) : stockQuotePrice*(1 + 1/form.leverage);
                    break;
            }

            //var tradeValue = form.invest/security.BaseMargin;
            //var quantity = tradeValue/security.LotSize/(form.isLong ? quote.Offer : quote.Bid);

            CFDGlobal.LogLine("NewOrder: userId:" + UserId + " secId:" + form.securityId + " long:" + form.isLong + " invest:" + form.invest + " leverage:" + form.leverage +
                              "|quantity:" + quantity + " stopPx:" + stopPx);

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
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.ORDER_REJECTED) + " " + e.Detail.Text));
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
            PositionReport result;
            try
            {
                result = clientHttp.NewOrder(user.AyondoUsername, user.AyondoPassword, form.securityId, !form.isPosLong, form.posQty, nettingPositionId: form.posId);
            }
            catch (FaultException<OrderRejectedFault> e)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.ORDER_REJECTED) + " " + e.Detail.Text));
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