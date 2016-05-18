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

            if (result.Count == 0)
                return new List<PositionDTO>();

            //order by recent created
            result = result.OrderByDescending(o => o.CreateTime).ToList();

            var redisProdDefClient = RedisClient.As<ProdDef>();
            var redisQuoteClient = RedisClient.As<Quote>();

            var prodDefs = redisProdDefClient.GetAll();
            var quotes = redisQuoteClient.GetAll();

            var secIds = result.Select(o => Convert.ToInt32(o.SecurityID)).ToList();

            var dbSecurities = db.AyondoSecurities.Where(o => secIds.Contains(o.Id)).ToList();

            var positionDtos = result.Select(delegate(PositionReport report)
            {
                var dbSec = dbSecurities.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                var prodDef = prodDefs.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                var quote = quotes.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));

                var security = Mapper.Map<SecurityDetailDTO>(dbSec);

                if (prodDef != null)
                {
                    if (security.name == null) security.name = prodDef.Name;
                    security.symbol = prodDef.Symbol;

                    security.preClose = prodDef.PreClose;
                    security.open = Quotes.GetOpenPrice(prodDef);
                    security.isOpen = prodDef.QuoteType == enmQuoteType.Open;

                    security.lastOpen = prodDef.LastOpen;
                    security.lastClose = prodDef.LastClose;

                    if (dbSec.Financing == "US Stocks")
                        security.tag = "US";

                    if (dbSec.DisplayDecimals!=null)
                    security.dcmCount = Convert.ToInt32(dbSec.DisplayDecimals);
                }

                if (quote != null)
                {
                    security.last = Quotes.GetLastPrice(quote);
                }

                var posDTO = OpenPositionReportToPositionDTO(report);
                posDTO.security = security;

                if (dbSec.DisplayDecimals != null)
                    posDTO.settlePrice = Math.Round(posDTO.settlePrice, Convert.ToInt32(dbSec.DisplayDecimals));

            //************************************************************************
            //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
            //************************************************************************
                var tradeValue = report.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(report.LongQty ?? report.ShortQty);
                var tradeValueUSD = tradeValue;
                if( prodDef.Ccy2 != "USD")
                    tradeValueUSD =  FX.Convert(tradeValue.Value, prodDef.Ccy2, "USD", prodDefs, quotes);

                posDTO.invest = tradeValueUSD.Value/report.Leverage.Value;

                return posDTO;
            }).ToList();

            return positionDtos;
        }

        [HttpGet]
        [Route("closed")]
        [BasicAuth]
        public List<PositionHistoryDTO> GetPositionHistory()
        {
            var user = GetUser();
            if (string.IsNullOrEmpty(user.AyondoUsername))
                throw new Exception("user do not have an ayondo account");

            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");

            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddMonths(-3);
            var historyReports = clientHttp.GetPositionHistoryReport(user.AyondoUsername, user.AyondoPassword, startTime,endTime);

            var result = new List<PositionHistoryDTO>();

            if (historyReports.Count == 0)
                return result;

            var groupByPositions = historyReports.GroupBy(o => o.PosMaintRptID);

            var groupByClosedPositions = groupByPositions.Where(o => o.Any(p => p.LongQty == 0 || p.ShortQty == 0));

            var secIds = groupByClosedPositions.Select(o => Convert.ToInt32(o.First().SecurityID)).Distinct().ToList();
            var dbSecurities = db.AyondoSecurities.Where(o => secIds.Contains(o.Id)).ToList();

            var redisQuoteClient = RedisClient.As<Quote>();
            var redisProdDefClient = RedisClient.As<ProdDef>();
            var prodDefs = redisProdDefClient.GetAll();
            var quotes = redisQuoteClient.GetAll();

            foreach (var positionGroup in groupByClosedPositions)
            {
                var dto=new PositionHistoryDTO();
                dto.id = positionGroup.Key;

                var positionReports = positionGroup.ToList();

                if (positionReports.Count > 2)
                {
                    foreach (var report in positionReports)
                    {
                        CFDGlobal.LogInformation(report.PosMaintRptID+" "+report.CreateTime+" "+report);
                    }
                }

                var closeReport = positionGroup.FirstOrDefault(o => o.LongQty == 0 || o.ShortQty == 0);

                var openReport = positionGroup.OrderBy(o => o.CreateTime).First();

                dto.closeAt = closeReport.CreateTime;
                dto.openAt = openReport.CreateTime;
                dto.closePrice = closeReport.SettlPrice;
                dto.openPrice = openReport.SettlPrice;

                dto.leverage = closeReport.Leverage.Value;
                dto.pl = closeReport.PL.Value;

                dto.isLong = openReport.LongQty != null;

                var secId = Convert.ToInt32(openReport.SecurityID);
                var prodDef = prodDefs.FirstOrDefault(o => o.Id == secId);
                //************************************************************************
                //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                //************************************************************************
                var tradeValue = openReport.SettlPrice * prodDef.LotSize / prodDef.PLUnits * (openReport.LongQty ?? openReport.ShortQty);
                var tradeValueUSD = tradeValue;
                if (prodDef.Ccy2 != "USD")
                    tradeValueUSD = FX.Convert(tradeValue.Value, prodDef.Ccy2, "USD", prodDefs, quotes);

                dto.invest = tradeValueUSD.Value / dto.leverage;

                var security =new SecurityDetailDTO();
                security.symbol = prodDef.Symbol;
                security.id = secId;

                var dbSec = dbSecurities.FirstOrDefault(o => o.Id == secId);
                if (dbSec.CName != null)
                    security.name = dbSec.CName;
                if (dbSec.Financing == "US Stocks")
                    security.tag = "US";
                if (dbSec.DisplayDecimals != null)
                    security.dcmCount = Convert.ToInt32(dbSec.DisplayDecimals);

                dto.security = security;

                result.Add(dto);
            }

            return result.OrderByDescending(o => o.closeAt).ToList();
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
            //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
            //************************************************************************

            decimal tradeValueCcy2 = FX.ConvertUSDtoCcy(tradeValueUSD, prodDef.Ccy2, RedisClient);

            var quote = redisQuoteClient.GetById(form.securityId);
            var quotePrice = form.isLong ? quote.Offer : quote.Bid;
            decimal quantity = tradeValueCcy2/(quotePrice/prodDef.PLUnits*prodDef.LotSize);
            decimal stopPx = form.isLong ? quotePrice*(1 - 1/form.leverage) : quotePrice*(1 + 1/form.leverage);

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

            var tradedValue = result.SettlPrice * prodDef.LotSize / prodDef.PLUnits * (result.LongQty ?? result.ShortQty);
            var tradedValueUSD = tradedValue.Value;
            if (prodDef.Ccy2 != "USD")
                tradedValueUSD = FX.Convert(tradedValue.Value, prodDef.Ccy2, "USD", RedisClient);

            var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);
            decimal settlP;
            if (security != null && security.DisplayDecimals != null)
            {
                int decimalCount = Convert.ToInt32(security.DisplayDecimals);
                settlP = Math.Round(result.SettlPrice, decimalCount);
            }
            else
            {
                settlP = result.SettlPrice;
            }

            var posDTO = new PositionDTO()
            {
                id = result.PosMaintRptID,
                isLong = result.LongQty != null,
                settlePrice = settlP,
                invest = tradedValueUSD / result.Leverage.Value,
                leverage = result.Leverage.Value,
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
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text)));
            }

            var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);
            decimal settlP;
            if (security != null && security.DisplayDecimals != null)
            {
                int decimalCount = Convert.ToInt32(security.DisplayDecimals);
                settlP = Math.Round(result.SettlPrice, decimalCount);
            }
            else
            {
                settlP = result.SettlPrice;
            }

            var posDTO = new PositionDTO()
            {
                id = result.PosMaintRptID,
                isLong = result.LongQty != null,
                settlePrice = settlP,
                //invest = 0,
                leverage =result.Leverage.Value,
                createAt = result.CreateTime,
                quantity = result.LongQty ?? result.ShortQty.Value,
                pl = result.PL,
            };

            return posDTO;
        }

        [HttpPost]
        [Route("order/take")]
        [BasicAuth]
        public PositionDTO NewTake(NewTakeFormDTO form)
        {
            var user = GetUser();
            if (string.IsNullOrEmpty(user.AyondoUsername))
                throw new Exception("user do not have an ayondo account");

            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");
            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            PositionReport report;
            try
            {
                report = clientHttp.NewTakeOrder(user.AyondoUsername, user.AyondoPassword, form.securityId, form.price, form.posId);
            }
            catch (FaultException<OrderRejectedFault> e)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text)));
            }

            var posDTO = OpenPositionReportToPositionDTO(report);

            var dbSec = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);
            if (dbSec.DisplayDecimals != null)
                posDTO.settlePrice = Math.Round(posDTO.settlePrice, Convert.ToInt32(dbSec.DisplayDecimals));

            return posDTO;
        }

        [HttpDelete]
        [Route("order/take")]
        [BasicAuth]
        public PositionDTO CancelTakeOrder(CancelTakeFormDTO form)
        {
            var user = GetUser();
            if (string.IsNullOrEmpty(user.AyondoUsername))
                throw new Exception("user do not have an ayondo account");

            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");
            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            PositionReport report;
            try
            {
                report = clientHttp.CancelOrder(user.AyondoUsername, user.AyondoPassword, form.securityId, form.orderId, form.posId);
            }
            catch (FaultException<OrderRejectedFault> e)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text)));
            }

            var posDTO = OpenPositionReportToPositionDTO(report);

            var dbSec = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);
            if (dbSec.DisplayDecimals != null)
                posDTO.settlePrice = Math.Round(posDTO.settlePrice, Convert.ToInt32(dbSec.DisplayDecimals));

            return posDTO;
        }

        [HttpPut]
        [Route("order")]
        [BasicAuth]
        public PositionDTO ReplaceOrder(ReplaceStopTakeFormDTO form)
        {
            var user = GetUser();
            if (string.IsNullOrEmpty(user.AyondoUsername))
                throw new Exception("user do not have an ayondo account");

            EndpointAddress edpHttp = new EndpointAddress("http://ayondotrade.chinacloudapp.cn/ayondotradeservice.svc");
            //AyondoTradeClient clientTcp = new AyondoTradeClient(new NetTcpBinding(SecurityMode.None), edpTcp);
            AyondoTradeClient clientHttp = new AyondoTradeClient(new BasicHttpBinding(BasicHttpSecurityMode.None), edpHttp);

            PositionReport report;
            try
            {
                report = clientHttp.ReplaceOrder(user.AyondoUsername, user.AyondoPassword, form.securityId, form.orderId, form.price,form.posId);
            }
            catch (FaultException<OrderRejectedFault> e)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text)));
            }

            var posDTO = OpenPositionReportToPositionDTO(report);

            var dbSec = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);
            if (dbSec.DisplayDecimals != null)
                posDTO.settlePrice = Math.Round(posDTO.settlePrice, Convert.ToInt32(dbSec.DisplayDecimals));

            return posDTO;
        }

        private static PositionDTO OpenPositionReportToPositionDTO(PositionReport report)
        {
            var posDTO = new PositionDTO()
            {
                id = report.PosMaintRptID,
                isLong = report.LongQty != null,
                settlePrice = report.SettlPrice,
                leverage = report.Leverage.Value,
                createAt = report.CreateTime,
                quantity = report.LongQty ?? report.ShortQty.Value,
                upl = report.UPL,
                stopPx = report.StopPx,
                stopOID = report.StopOID,
                takePx = report.TakePx,
                takeOID = report.TakeOID,
            };
            return posDTO;
        }
    }
}