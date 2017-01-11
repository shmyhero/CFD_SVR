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
using CFD_API.Caching;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_API.DTO.FormDTO;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using CFD_COMMON.Utils;
using ServiceStack.Redis;
using System.Data.SqlTypes;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/position")]
    public class PositionController : CFDController
    {
        public PositionController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }

        private static decimal _minStopPx = 0.000001m;

        [HttpGet]
        [Route("open")]
        [Route("live/open")]
        [BasicAuth]
        public List<PositionDTO> GetOpenPositions(bool ignoreCache = false)
        {
            //throw new NullReferenceException();

            var user = GetUser();
            
            if(!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            IList<PositionReport> result = null;
            using (var wcfClient = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    result = wcfClient.GetPositionReport(IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                        IsLiveUrl ? null : user.AyondoPassword,
                        ignoreCache);
                }
                catch (FaultException<OAuthLoginRequiredFault>)//when oauth is required
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            if (result == null || result.Count == 0)
                return new List<PositionDTO>();

            //order by recent created
            result = result.OrderByDescending(o => o.CreateTime).ToList();

            //var redisProdDefClient = RedisClient.As<ProdDef>();
            //var redisQuoteClient = RedisClient.As<Quote>();

            //var prodDefs = redisProdDefClient.GetAll();
            //var quotes = redisQuoteClient.GetAll();

            //var secIds = result.Select(o => Convert.ToInt32(o.SecurityID)).ToList();

            //var dbSecurities = db.AyondoSecurities.Where(o => secIds.Contains(o.Id)).ToList();

            var cache = WebCache.GetInstance(IsLiveUrl);

            var positionDtos = result.Select(delegate(PositionReport report)
            {
                //var dbSec = dbSecurities.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));

                if (prodDef == null)
                {
                    CFDGlobal.LogInformation("cannot find prodDef for secId: " + report.SecurityID + " in open positions of userId: " + UserId +
                                             " | posId:" + report.PosMaintRptID + " longQty:" + report.LongQty + " shortQty:" + report.ShortQty);
                    return null;
                }

                var quote = cache.Quotes.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                
                var security = Mapper.Map<SecurityDetailDTO>(prodDef);
                if (Quotes.IsPriceDown(cache.PriceDownInterval.FirstOrDefault(o => o.Key == quote.Id), quote.Time))
                {
                    security.isPriceDown = true;
                }

                if (quote != null)
                {
                    security.last = Quotes.GetLastPrice(quote);
                    security.ask = quote.Offer;
                    security.bid = quote.Bid;
                }

                var posDTO = MapPositionReportToPositionDTO(report);
                posDTO.security = security;

                //default fx rate for client calculation
                if (prodDef.Ccy2 != "USD")
                {
                    var fxProdDef =
                        cache.ProdDefs.FirstOrDefault(
                            o => o.Symbol == prodDef.Ccy2 + "USD" && o.Name.EndsWith(" Outright"));

                    if (fxProdDef == null)
                    {
                        fxProdDef =
                            cache.ProdDefs.FirstOrDefault(
                                o => o.Symbol == "USD" + prodDef.Ccy2 && o.Name.EndsWith(" Outright"));
                    }

                    if (fxProdDef != null)
                    {
                        var fx =new SecurityDetailDTO();
                        fx.id = fxProdDef.Id;
                        fx.symbol = fxProdDef.Symbol;
                        
                        var fxQuote = cache.Quotes.FirstOrDefault(o => o.Id == fx.id);
                        if (fxQuote != null)
                        {
                            fx.ask = fxQuote.Offer;
                            fx.bid = fxQuote.Bid;
                        }

                        posDTO.fxOutright = fx;
                    }
                }

                //if (prodDef.Prec != null)
                posDTO.settlePrice = Math.Round(posDTO.settlePrice, Convert.ToInt32(prodDef.Prec));

                //************************************************************************
                //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                //************************************************************************
                var tradeValue = report.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(report.LongQty ?? report.ShortQty);
                //var tradeValueUSD = tradeValue;
                //if (prodDef.Ccy2 != "USD")
                //    tradeValueUSD = FX.Convert(tradeValue.Value, prodDef.Ccy2, "USD", WebCache.ProdDefs, WebCache.Quotes);

                posDTO.invest = tradeValue.Value/report.Leverage.Value;

                //calculate UPL
                if (quote != null)
                {
                    decimal upl = report.LongQty.HasValue ? tradeValue.Value*(quote.Bid/report.SettlPrice - 1) : tradeValue.Value*(1 - quote.Offer/report.SettlPrice);
                    var uplUSD = FX.ConvertPlByOutright(upl, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes);
                    posDTO.upl = uplUSD;
                }
                else
                {
                    CFDGlobal.LogWarning("cannot find quote:" + report.SecurityID + " when calculating UPL for open position "+posDTO.id);
                }

                return posDTO;
            }).Where(o => o != null).ToList();

            return positionDtos;
        }

        [HttpGet]
        [Route("closed2")]
        [Route("live/closed2")]
        [BasicAuth]
        public List<PositionHistoryDTO> GetPositionHistory(bool ignoreCache = false)
        {
            var user = GetUser();
            if(!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);
            
            IList<PositionReport> historyReports;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                var endTime = DateTime.UtcNow;
                var startTime = DateTimes.GetHistoryQueryStartTime(endTime);

                try
                {
                    historyReports = clientHttp.GetPositionHistoryReport(IsLiveUrl?user.AyLiveUsername:user.AyondoUsername, IsLiveUrl?null: user.AyondoPassword,
                        startTime, endTime, ignoreCache);
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            var result = new List<PositionHistoryDTO>();

            if (historyReports.Count == 0)
                return result;

            var groupByPositions = historyReports.GroupBy(o => o.PosMaintRptID);

            //var secIds = groupByPositions.Select(o => Convert.ToInt32(o.First().SecurityID)).Distinct().ToList();
            //var dbSecurities = db.AyondoSecurities.Where(o => secIds.Contains(o.Id)).ToList();

            //var redisQuoteClient = RedisClient.As<Quote>();
            //var redisProdDefClient = RedisClient.As<ProdDef>();
            //var prodDefs = redisProdDefClient.GetAll();
            //var quotes = redisQuoteClient.GetAll();

            var cache = WebCache.GetInstance(IsLiveUrl);
            foreach (var positionGroup in groupByPositions) //for every position group
            {
                var dto = new PositionHistoryDTO();
                dto.id = positionGroup.Key;

                var positionReports = positionGroup.ToList();

                if (positionReports.Count >= 2)
                {
                    var openReport = positionReports.OrderBy(o => o.CreateTime).First();
                    var closeReport = positionReports.OrderBy(o => o.CreateTime).Last();

                    if (Decimals.IsTradeSizeZero(closeReport.LongQty) || Decimals.IsTradeSizeZero(closeReport.ShortQty))
                    {
                        var secId = Convert.ToInt32(openReport.SecurityID);
                        var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == secId);

                        if (prodDef == null)
                        {
                            CFDGlobal.LogLine("cannot find product definition for sec id: " + secId + " in history position reports of user id: " + UserId);
                            continue;
                        }

                        //var closeReport = positionReports.FirstOrDefault(o => Decimals.IsEqualToZero(o.LongQty) || Decimals.IsEqualToZero(o.ShortQty));

                        dto.openPrice = Math.Round(openReport.SettlPrice, prodDef.Prec);
                        dto.openAt = openReport.CreateTime;

                        dto.closePrice = Math.Round(closeReport.SettlPrice, prodDef.Prec);
                        dto.closeAt = closeReport.CreateTime;

                        dto.leverage = closeReport.Leverage;
                        dto.pl = closeReport.PL.Value;

                        dto.isLong = openReport.LongQty != null;

                        //************************************************************************
                        //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                        //************************************************************************
                        var tradeValue = openReport.SettlPrice * prodDef.LotSize / prodDef.PLUnits * (openReport.LongQty ?? openReport.ShortQty);
                        //var tradeValueUSD = tradeValue;
                        //if (prodDef.Ccy2 != "USD")
                        //    tradeValueUSD = FX.Convert(tradeValue.Value, prodDef.Ccy2, "USD", WebCache.ProdDefs, WebCache.Quotes);

                        dto.invest = tradeValue / dto.leverage;

                        var security = Mapper.Map<SecurityDetailDTO>(prodDef);
                        security.bid = null;
                        security.ask = null;
                        security.lastOpen = null;
                        security.lastClose = null;
                        security.maxLeverage = null;
                        security.smd = null;
                        security.gsmd = null;
                        security.preClose = null;
                        security.open = null;
                        security.last = null;
                        security.isOpen = null;

                        //var dbSec = dbSecurities.FirstOrDefault(o => o.Id == secId);
                        //if (dbSec.CName != null)
                        //    security.name = dbSec.CName;
                        //if (dbSec.Financing == "US Stocks")
                        //    security.tag = "US";
                        //if (dbSec.DisplayDecimals != null)
                        //    security.dcmCount = Convert.ToInt32(dbSec.DisplayDecimals);

                        dto.security = security;

                        result.Add(dto);
                    }
                }
                //else
                //{
                //    var sb = new StringBuilder();
                //    sb.AppendLine("PositionHistory: position " + positionGroup.Key + " has " + positionReports.Count + " reports");
                //    foreach (var report in positionReports)
                //    {
                //        sb.AppendLine(report.PosMaintRptID + " " + report.CreateTime + " " + report.LongQty + " " + report.ShortQty
                //                      + " " + report.SettlPrice + " " + report.UPL + " " + report.PL);
                //    }
                //    CFDGlobal.LogInformation(sb.ToString());
                //}
            }

            return result.OrderByDescending(o => o.closeAt).ToList();
        }

        [HttpGet]
        [Route("closed")]
        [Route("live/closed")]
        [BasicAuth]
        public List<PositionHistoryDTO> GetPositionHistory()
        {
            var user = GetUser();

            if(!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            var result = new List<PositionHistoryDTO>();

            IList<PositionReport> historyReports;
            var endTimeAyondo = DateTime.UtcNow;
            var startTimeAyondo = DateTimes.GetHistoryQueryStartTime(endTimeAyondo);

            var endTimeDB = startTimeAyondo.AddMilliseconds(-1);
            var startTimeDB = endTimeDB.AddDays(-20);
            int monthDays = 30;//假设一个月30天
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    //可能从Ayondo拿，也可能从Cache里面拿。
                    //不论是Ayondo还是Cache，拿出来的结果集都不一定是10天。
                    historyReports =
                        clientHttp.GetPositionHistoryReport(
                            IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                            IsLiveUrl ? null : user.AyondoPassword,
                            startTimeAyondo, endTimeAyondo).ToList();
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            var cache = WebCache.GetInstance(IsLiveUrl);

            if (historyReports.Count > 0)
            {
                var groupByPositions = historyReports.GroupBy(o => o.PosMaintRptID);

                foreach (var positionGroup in groupByPositions) //for every position group
                {
                    var dto = new PositionHistoryDTO();
                    dto.id = positionGroup.Key;

                    var positionReportsAyondo = positionGroup.ToList();


                    if (positionReportsAyondo.Count >= 2)
                    {
                        var openReport = positionReportsAyondo.OrderBy(o => o.CreateTime).First();
                        var closeReport = positionReportsAyondo.OrderBy(o => o.CreateTime).Last();

                        if (Decimals.IsTradeSizeZero(closeReport.LongQty) || Decimals.IsTradeSizeZero(closeReport.ShortQty))
                        {
                            var secId = Convert.ToInt32(openReport.SecurityID);
                            var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == secId);

                            if (prodDef == null)
                            {
                                CFDGlobal.LogLine("cannot find product definition for sec id: " + secId + " in history position reports of user id: " + UserId);
                                continue;
                            }

                            dto.openPrice = Math.Round(openReport.SettlPrice, prodDef.Prec);
                            dto.openAt = openReport.CreateTime;

                            dto.closePrice = Math.Round(closeReport.SettlPrice, prodDef.Prec);
                            dto.closeAt = closeReport.CreateTime;

                            dto.leverage = closeReport.Leverage;
                            dto.pl = closeReport.PL.Value;

                            dto.isLong = openReport.LongQty != null;

                            //************************************************************************
                            //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                            //************************************************************************
                            var tradeValue = openReport.SettlPrice * prodDef.LotSize / prodDef.PLUnits * (openReport.LongQty ?? openReport.ShortQty);
                            //var tradeValueUSD = tradeValue;
                            //if (prodDef.Ccy2 != "USD")
                            //    tradeValueUSD = FX.Convert(tradeValue.Value, prodDef.Ccy2, "USD", WebCache.ProdDefs, WebCache.Quotes);

                            dto.invest = tradeValue / dto.leverage;

                            var security = Mapper.Map<SecurityDetailDTO>(prodDef);
                            HideSecInfo(security);

                            dto.security = security;

                            result.Add(dto);
                        }
                    }
                    else if (positionReportsAyondo.Count == 1)//对于只拿到一条Delete Position Report的情况，去DB里拿Open的信息
                    {
                        var closePR = positionReportsAyondo[0];
                        //确定该条记录是Delete，而非Update
                        if (Decimals.IsTradeSizeZero(closePR.LongQty) || Decimals.IsTradeSizeZero(closePR.ShortQty))
                        {
                            //去DB里面拿Open的记录
                            var openPR = IsLiveUrl
                                ? (NewPositionHistoryBase)db.NewPositionHistory_live.FirstOrDefault(o => o.Id.ToString() == closePR.PosMaintRptID)
                                : db.NewPositionHistories.FirstOrDefault(o => o.Id.ToString() == closePR.PosMaintRptID);
                            if (openPR == null)
                            {
                                continue;
                            }

                            var secId = Convert.ToInt32(openPR.SecurityId);
                            var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == secId);

                            if (prodDef == null)
                            {
                                CFDGlobal.LogLine("cannot find product definition for sec id: " + secId + " in history position reports of user id: " + UserId);
                                continue;
                            }

                            dto.openPrice = Math.Round(openPR.SettlePrice.Value, prodDef.Prec);
                            dto.openAt = openPR.CreateTime.Value;

                            dto.closePrice = Math.Round(closePR.SettlPrice, prodDef.Prec);
                            dto.closeAt = closePR.CreateTime;

                            dto.leverage = closePR.Leverage;
                            dto.pl = closePR.PL.Value;

                            dto.isLong = openPR.LongQty.HasValue;

                            //************************************************************************
                            //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                            //************************************************************************
                            var tradeValue = openPR.SettlePrice * prodDef.LotSize / prodDef.PLUnits * (openPR.LongQty ?? openPR.ShortQty);
                            //var tradeValueUSD = tradeValue;
                            //if (prodDef.Ccy2 != "USD")
                            //    tradeValueUSD = FX.Convert(tradeValue.Value, prodDef.Ccy2, "USD", WebCache.ProdDefs, WebCache.Quotes);

                            dto.invest = tradeValue / dto.leverage;

                            var security = Mapper.Map<SecurityDetailDTO>(prodDef);
                            HideSecInfo(security);

                            dto.security = security;

                            result.Add(dto);
                        }
                    }
                }
            }

            #region 如果上面的接口拿到的数据不足一个月，补足一个月
            //已拿到的数据中，最前面一条的平仓时间
            //如果已拿到的数据集为空，则以当前时间为开始时间
            var startTime = (historyReports.Count == 0 || result.Count == 0)? DateTime.UtcNow : result.Min(o => o.closeAt);

            if ((DateTime.UtcNow - startTime).Days < monthDays) //如果拿出来的数据的跨度小于一个月
            {
                endTimeDB = startTime.AddMilliseconds(-1);
                startTimeDB = endTimeDB.AddDays(-1 * (monthDays - (DateTime.UtcNow - startTime).Days));

                var positionReportsDB = IsLiveUrl
                    ? db.NewPositionHistory_live.Where(
                        o =>
                            o.PL.HasValue && o.SettlePrice.HasValue && o.CreateTime.HasValue && o.ClosedAt.HasValue &&
                            o.ClosedAt >= startTimeDB && o.ClosedAt <= endTimeDB && o.UserId == UserId)
                        .ToList()
                        .Select(o => o as NewPositionHistoryBase).ToList()
                    : db.NewPositionHistories.Where(
                        o =>
                            o.PL.HasValue && o.SettlePrice.HasValue && o.CreateTime.HasValue && o.ClosedAt.HasValue &&
                            o.ClosedAt >= startTimeDB && o.ClosedAt <= endTimeDB && o.UserId == UserId)
                        .ToList()
                        .Select(o => o as NewPositionHistoryBase).ToList();
                positionReportsDB.ToList().ForEach(item => {
                    var dto = new PositionHistoryDTO() {
                        closeAt = item.ClosedAt.Value,
                        closePrice = item.ClosedPrice.Value,
                        id = item.Id.ToString(),
                        invest = item.InvestUSD.Value, //先用USD的金额赋值，在后面如果对应的产品不为空，就用产品计算出对应的货币。
                        isLong = item.LongQty.HasValue,
                        leverage = item.Leverage,
                        openAt = item.CreateTime.Value,
                        openPrice = item.SettlePrice.Value,
                        pl = item.PL.Value
                    };

                    var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == item.SecurityId);

                    if (prodDef == null)
                    {
                        CFDGlobal.LogLine("cannot find product definition for sec id: " + item.SecurityId + " in history position reports of user id: " + UserId);
                    }
                    else
                    {
                        var tradeValue = dto.openPrice * prodDef.LotSize / prodDef.PLUnits * (item.LongQty ?? item.ShortQty);
                        dto.invest = tradeValue / dto.leverage;

                        var security = Mapper.Map<SecurityDetailDTO>(prodDef);
                        HideSecInfo(security);

                        dto.security = security;

                        result.Add(dto);
                    }
                });
            }
            #endregion

            //拿到平仓记录的PositionId, 与卡牌表中的PositionId做比较。 然后更新平仓记录的HasCard数据
            List<long> positionIdList = result.Select(o => long.Parse(o.id)).ToList();
            var posList = (from u in db.UserCards_Live
                           where positionIdList.Contains(u.PositionId)
                          //orderby u.ClosedAt descending
                          select u.PositionId.ToString()).ToList();

            result.ForEach(item =>
            {
                if(posList.Contains(item.id))
                {
                    item.hasCard = true;
                }
            });

            return result.OrderByDescending(o => o.closeAt).ToList();
        }

        /// <summary>
        /// 隐藏一些字段，使之不在平仓记录中显示
        /// </summary>
        /// <param name="security"></param>
        private void HideSecInfo(SecurityDetailDTO security)
        {
            security.bid = null;
            security.ask = null;
            security.lastOpen = null;
            security.lastClose = null;
            security.maxLeverage = null;
            security.smd = null;
            security.gsmd = null;
            security.preClose = null;
            security.open = null;
            security.last = null;
            security.isOpen = null;
        }

        [HttpPost]
        [Route("")]
        [Route("live")]
        [BasicAuth2]
        public PositionDTO NewPosition(NewPositionFormDTO form, bool ignorePriceDelay = false)
        {
            var user = GetUser();

            if (!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            //var redisProdDefClient = RedisClient.As<ProdDef>();
            //var redisQuoteClient = RedisClient.As<Quote>();

            //var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);

            //if (security == null)
            //    throw new Exception("security not found");

            var cache = WebCache.GetInstance(IsLiveUrl);

            var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == form.securityId);
            if (prodDef == null)
                throw new Exception("security not found");

            var tradeValueUSD = form.invest*form.leverage;

            //************************************************************************
            //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
            //************************************************************************

            decimal tradeValueCcy2 = FX.ConvertByOutrightMidPrice(tradeValueUSD, "USD", prodDef.Ccy2, cache.ProdDefs, cache.Quotes);

            var quote = cache.Quotes.FirstOrDefault(o => o.Id == form.securityId);

            //price
            if(!ignorePriceDelay && Quotes.IsPriceDown(cache.PriceDownInterval.FirstOrDefault(o => o.Key == quote.Id), quote.Time))
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        __(TransKey.PRICEDOWN)));
            }

            var quotePrice = form.isLong ? quote.Offer : quote.Bid;
            decimal quantity = tradeValueCcy2/(quotePrice/prodDef.PLUnits*prodDef.LotSize);
            quantity = Maths.Floor(quantity, 8);
            decimal stopPx = form.isLong ? quotePrice*(1 - 1/form.leverage) : quotePrice*(1 + 1/form.leverage);

            //prevent lost >100%
            stopPx = form.isLong ? Maths.Ceiling(stopPx, prodDef.Prec) : Maths.Floor(stopPx, prodDef.Prec);

            //Long, Leverage=1, stop will be Zero! which is invalid
            if (stopPx == 0)
                stopPx = _minStopPx;

            PositionReport result;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    result = clientHttp.NewOrder(
                        IsLiveUrl? user.AyLiveUsername: user.AyondoUsername,
                        IsLiveUrl? null: user.AyondoPassword,
                        form.securityId, form.isLong,
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
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }

                CFDGlobal.LogLine("NewOrder: userId:" + UserId + " secId:" + form.securityId + " long:" + form.isLong +
                                  " invest:" + form.invest + " leverage:" + form.leverage +
                                  " | quote:" + quotePrice + " | quantity:" + quantity + " stopPx:" + stopPx + " | Qty:" +
                                  (result.LongQty ?? result.ShortQty) + " SettlePrice:" +
                                  result.SettlPrice);

                //save new position history
                var newHistory = new NewPositionHistoryBase()
                {
                    Id = Convert.ToInt64(result.PosMaintRptID),
                    UserId = UserId,
                    SecurityId = Convert.ToInt32(result.SecurityID),
                    CreateTime = result.CreateTime,
                    Leverage = result.Leverage,
                    LongQty = result.LongQty,
                    ShortQty = result.ShortQty,
                    SettlePrice = result.SettlPrice,
                    InvestUSD = form.invest,
                };
                if (IsLiveUrl)
                    db.NewPositionHistory_live.Add(Mapper.Map<NewPositionHistory_live>( newHistory));
                else
                    db.NewPositionHistories.Add(Mapper.Map<NewPositionHistory>(newHistory));

                //update ayondo account id if not same
                var accountId = Convert.ToInt64(result.Account);
                if (IsLiveUrl && user.AyLiveAccountId != accountId)
                    user.AyLiveAccountId = accountId;
                if (!IsLiveUrl && user.AyondoAccountId != accountId)
                    user.AyondoAccountId = accountId;

                db.SaveChanges();

                if(!IsLiveUrl) RewardDailyDemoTransaction();

                //when price changes, set stop again to prevent >100% loss
                if (quotePrice != result.SettlPrice)
                {
                    decimal newStopPx = result.LongQty.HasValue
                        ? result.SettlPrice*(1 - 1/result.Leverage.Value)
                        : result.SettlPrice*(1 + 1/result.Leverage.Value);
                    newStopPx = result.LongQty.HasValue
                        ? Maths.Ceiling(newStopPx, prodDef.Prec)
                        : Maths.Floor(newStopPx, prodDef.Prec);

                    if (result.LongQty.HasValue && newStopPx > stopPx || result.ShortQty.HasValue && newStopPx < stopPx)
                    {
                        CFDGlobal.LogLine("ReSet StopPx: quote:" + quotePrice + " settlePrice:" + result.SettlPrice +
                                          " | oldStop:" + stopPx + " newStop:" + newStopPx);
                        try
                        {
                            var positionReport = clientHttp.ReplaceOrder(
                                IsLiveUrl?user.AyLiveUsername: user.AyondoUsername, 
                                IsLiveUrl? null : user.AyondoPassword,
                                Convert.ToInt32(result.SecurityID), result.StopOID, newStopPx,
                                result.PosMaintRptID);
                        }
                        catch (FaultException<OrderRejectedFault> e)
                        {
                            CFDGlobal.LogWarning("Error while ReSetting StopPx: " +
                                                 Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text));
                        }
                    }
                }
            }

            var tradedValue = result.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(result.LongQty ?? result.ShortQty);
            //var tradedValueUSD = tradedValue.Value;
            //if (prodDef.Ccy2 != "USD")
            //    tradedValueUSD = FX.Convert(tradedValue.Value, prodDef.Ccy2, "USD", WebCache.ProdDefs, WebCache.Quotes);

            decimal settlP = Math.Round(result.SettlPrice, prodDef.Prec);

            var posDTO = new PositionDTO()
            {
                id = result.PosMaintRptID,
                isLong = result.LongQty != null,
                settlePrice = settlP,
                invest = tradedValue.Value/result.Leverage.Value,
                leverage = result.Leverage.Value,
                createAt = result.CreateTime,
                quantity = result.LongQty ?? result.ShortQty.Value,
                stopPx = result.StopPx,
                stopOID = result.StopOID,
            };

            posDTO.security = new SecurityDetailDTO() {id = prodDef.Id, ccy = prodDef.Ccy2};

            return posDTO;
        }

        /// <summary>
        /// daily demo trasaction reward
        /// </summary>
        /// <param name="userId"></param>
        private void RewardDailyDemoTransaction()
        {
            var rewardService=new RewardService(db);
            rewardService.TradeReward(UserId);

            //DateTime today = DateTime.UtcNow.AddHours(8).Date;
            //DailyTransaction todayTrasaction = db.DailyTransactions.Where(item => item.UserId == UserId && item.Date == today).FirstOrDefault();
            //if(todayTrasaction == null)
            //{
            //    todayTrasaction = new DailyTransaction();
            //    todayTrasaction.Date = DateTime.UtcNow.AddHours(8).Date;
            //    todayTrasaction.Amount = RewardService.REWARD_DEMO_TRADE;
            //    todayTrasaction.DealAt = DateTime.UtcNow.AddHours(8);
            //    todayTrasaction.UserId = UserId;
            //    todayTrasaction.IsPaid = false;
            //    db.DailyTransactions.Add(todayTrasaction);
            //    db.SaveChanges();
            //}

            //return;
        }

        [HttpPost]
        [Route("net")]
        [Route("live/net")]
        [BasicAuth]
        public PositionDTO NetPosition(NetPositionFormDTO form)
        {
            var user = GetUser();

            if(!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            var cache = WebCache.GetInstance(IsLiveUrl);

            //var redisProdDefClient = RedisClient.As<ProdDef>();
            var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == form.securityId);
            //if (prodDef == null)
            //    throw new Exception("security not found");

            //var redisQuoteClient = RedisClient.As<Quote>();
            //var quote = redisQuoteClient.GetById(form.securityId);

            PositionReport result;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    result = clientHttp.NewOrder(
                        IsLiveUrl? user.AyLiveUsername : user.AyondoUsername, 
                        IsLiveUrl? null  :user.AyondoPassword, 
                        form.securityId, !form.isPosLong, form.posQty, nettingPositionId: form.posId);
                }
                catch (FaultException<OrderRejectedFault> e)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text)));
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            //var security = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);
            //decimal settlP;
            //if (security != null && security.DisplayDecimals != null)
            //{
            //    int decimalCount = Convert.ToInt32(security.DisplayDecimals);
            //    settlP = Math.Round(result.SettlPrice, decimalCount);
            //}
            //else
            //{
            //    settlP = result.SettlPrice;
            //}
           var  settlP = prodDef == null ? result.SettlPrice : Math.Round(result.SettlPrice, prodDef.Prec);

            var posDTO = new PositionDTO()
            {
                id = result.PosMaintRptID,
                isLong = result.LongQty != null,
                settlePrice = settlP,
                //invest = 0,
                leverage = result.Leverage.Value,
                createAt = result.CreateTime,
                quantity = result.LongQty ?? result.ShortQty.Value,
                pl = result.PL,
            };

            if (IsLiveUrl) //只在实盘有卡牌
            {
                //Ayondo返回的Report里面不包含Invest和开仓价格，要从数据库里面拿
                long posID = long.Parse(form.posId);
                var position = db.NewPositionHistory_live.FirstOrDefault(o => o.Id == posID);

                if (position != null)
                {
                    //收益率
                    var plRatePercent = position.LongQty.HasValue
                        ? (result.SettlPrice - position.SettlePrice)/position.SettlePrice*result.Leverage*100
                        : (position.SettlePrice - result.SettlPrice)/position.SettlePrice*result.Leverage*100;

                    var cardService = new CardService(db);
                    var card = cardService.GetCard(result.PL.Value, plRatePercent.Value, position.SettlePrice.Value);

                    posDTO.isLong = position.LongQty.HasValue;

                    if (card != null)
                    {
                        posDTO.card = new CardDTO()
                        {
                            imgUrlBig = card.CardImgUrlBig,
                            imgUrlMiddle = card.CardImgUrlMiddle,
                            imgUrlSmall = card.CardImgUrlSmall,
                            invest = position.InvestUSD,
                            isLong = position.LongQty.HasValue,
                            leverage = posDTO.leverage,
                            reward = card.Reward,
                            settlePrice = settlP,
                            tradePrice = position.SettlePrice,
                            tradeTime = position.CreateTime.Value,
                            ccy = prodDef == null ? string.Empty : prodDef.Ccy2,
                            stockName = prodDef == null ? string.Empty : Translator.GetCName(prodDef.Name),
                            themeColor = card.ThemeColor
                        };

                        UserCard_Live uc = new UserCard_Live()
                        {
                            UserId = this.UserId,
                            CardId = card.Id,
                            //CCY = posDTO.card.ccy,
                            ClosedAt = result.CreateTime,
                            CreatedAt = DateTime.UtcNow,
                            Expiration = SqlDateTime.MaxValue.Value,
                            Invest = posDTO.card.invest,
                            PositionId = long.Parse(posDTO.id),
                            IsLong = posDTO.card.isLong,
                            Leverage = posDTO.card.leverage,
                            Likes = 0,
                            PL = posDTO.pl,
                            Qty = position.LongQty ?? position.ShortQty,
                            Reward = card.Reward,
                            SettlePrice = settlP,
                            //StockName = posDTO.card.stockName,
                            SecurityId = form.securityId,
                            TradePrice = posDTO.card.tradePrice,
                            TradeTime = posDTO.card.tradeTime,
                            IsNew = false,
                            IsShared = false,
                            IsPaid = false
                        };
                        db.UserCards_Live.Add(uc);
                        RewardService.AddTotalReward(UserId, card.Reward.HasValue? card.Reward.Value : 0, db);
                        db.SaveChanges();

                        posDTO.card.cardId = uc.Id;
                    }
                }
            }

            return posDTO;
        }

        [HttpPost]
        [Route("order/take")]
        [Route("live/order/take")]
        [BasicAuth]
        public PositionDTO NewTake(NewTakeFormDTO form)
        {
            var user = GetUser();

            if (!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            PositionReport report;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    report = clientHttp.NewTakeOrder(
                        IsLiveUrl?user.AyLiveUsername: user.AyondoUsername,
                        IsLiveUrl?null : user.AyondoPassword,
                        form.securityId, form.price, form.posId);
                }
                catch (FaultException<OrderRejectedFault> e)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text)));
                }
            }

            var posDTO = MapPositionReportToPositionDTO(report);

            //var dbSec = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);
            //if (dbSec.DisplayDecimals != null)
            //    posDTO.settlePrice = Math.Round(posDTO.settlePrice, Convert.ToInt32(dbSec.DisplayDecimals));
            //var redisProdDefClient = RedisClient.As<ProdDef>();
            var prodDef = WebCache.GetInstance(IsLiveUrl).ProdDefs.FirstOrDefault(o => o.Id == form.securityId);
            posDTO.settlePrice = Math.Round(posDTO.settlePrice, prodDef.Prec);

            return posDTO;
        }

        [HttpDelete]
        [Route("order/take")]
        [Route("live/order/take")]
        [BasicAuth]
        public PositionDTO CancelTakeOrder(CancelTakeFormDTO form)
        {
            var user = GetUser();

            if (!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            PositionReport report;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    report = clientHttp.CancelOrder(
                        IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                        IsLiveUrl ? null : user.AyondoPassword,
                        form.securityId, form.orderId, form.posId);
                }
                catch (FaultException<OrderRejectedFault> e)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text)));
                }
            }

            var posDTO = MapPositionReportToPositionDTO(report);

            //var dbSec = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);
            //if (dbSec.DisplayDecimals != null)
            //    posDTO.settlePrice = Math.Round(posDTO.settlePrice, Convert.ToInt32(dbSec.DisplayDecimals));
            //var redisProdDefClient = RedisClient.As<ProdDef>();
            var prodDef = WebCache.GetInstance(IsLiveUrl).ProdDefs.FirstOrDefault(o => o.Id == form.securityId);
            posDTO.settlePrice = Math.Round(posDTO.settlePrice, prodDef.Prec);

            return posDTO;
        }

        [HttpPut]
        [Route("order")]
        [Route("live/order")]
        [BasicAuth]
        public PositionDTO ReplaceOrder(ReplaceStopTakeFormDTO form)
        {
            var user = GetUser();

            if (!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            PositionReport report;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            { 
                try
                {
                    report = clientHttp.ReplaceOrder(
                        IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                        IsLiveUrl ? null : user.AyondoPassword,
                        form.securityId, form.orderId, form.price, form.posId);
                }
                catch (FaultException<OrderRejectedFault> e)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, Translator.AyondoOrderRejectMessageTranslate(e.Detail.Text)));
                }
            }

            var posDTO = MapPositionReportToPositionDTO(report);

            //var dbSec = db.AyondoSecurities.FirstOrDefault(o => o.Id == form.securityId);
            //if (dbSec.DisplayDecimals != null)
            //    posDTO.settlePrice = Math.Round(posDTO.settlePrice, Convert.ToInt32(dbSec.DisplayDecimals));
            //var redisProdDefClient = RedisClient.As<ProdDef>();
            var prodDef = WebCache.GetInstance(IsLiveUrl).ProdDefs.FirstOrDefault(o => o.Id == form.securityId);
            posDTO.settlePrice = Math.Round(posDTO.settlePrice, prodDef.Prec);

            return posDTO;
        }

        [HttpGet]
        [Route("printcache")]
        public string PrintCache(string username = "")
        {
            string result;
            using (var clientHttp = new AyondoTradeClient())
            {
                result = clientHttp.PrintCache(username);
            }
            return result;
        }

        [HttpGet]
        [Route("switchcache")]
        public void SwitchCache(string mode = "")
        {
            using (var clientHttp = new AyondoTradeClient())
            {
                clientHttp.SwitchCache(mode);
            }
        }

        [HttpGet]
        [Route("clearcache")]
        public void ClearCache(string username = "")
        {
            using (var clientHttp = new AyondoTradeClient())
            {
                clientHttp.ClearCache(username);
            }
        }

        private static PositionDTO MapPositionReportToPositionDTO(PositionReport report)
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