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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServiceStack.Common.Extensions;

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

            var posIDs = result.Select(o => Convert.ToInt64(o.PosMaintRptID)).ToList();
            var transferHistories = IsLiveUrl
                ? db.AyondoTransferHistory_Live.Where(o => o.PositionId.HasValue && posIDs.Contains(o.PositionId.Value)).ToList().Select(o => o as AyondoTransferHistoryBase).ToList()
                : db.AyondoTransferHistories.Where(o => o.PositionId.HasValue && posIDs.Contains(o.PositionId.Value)).ToList().Select(o => o as AyondoTransferHistoryBase).ToList();

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
                if (Quotes.IsPriceDown(cache.GetProdSettingByID(quote.Id), quote.Time))
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

                //security
                posDTO.security = security;

                //transferHistory
                var financings = transferHistories.Where(o => o.PositionId.ToString() == posDTO.id && o.TransferType == "Financing").ToList();
                var dividends = transferHistories.Where(o => o.PositionId.ToString() == posDTO.id && o.TransferType == "Dividend").ToList();
                if (financings.Count > 0) posDTO.financingSum = financings.Sum(o => o.Amount);
                if (dividends.Count > 0) posDTO.dividendSum = dividends.Sum(o => o.Amount);

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
                        var fx =new SecurityDTO();
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
        [Route("~/api/user/{userId}/live/position/open")]
        [BasicAuth]
        public List<SimplePositionDTO> GetSimpleOpenPositions(int userID)
        {
            if (userID != UserId) //not myself
            {
                var user = db.Users.FirstOrDefault(o => o.Id == userID);
                if (user == null || !(user.ShowData ?? false))
                    return new List<SimplePositionDTO>();
            }

            List<SimplePositionDTO> results = new List<SimplePositionDTO>();
            var positions = db.NewPositionHistory_live.Where(p => p.UserId == userID && !p.ClosedAt.HasValue).OrderByDescending(p => p.Id).Take(20).ToList();
            var cache = WebCache.GetInstance(true);

            positions.ForEach(p => {
                SimplePositionDTO dto = new SimplePositionDTO();
                dto.id = p.SecurityId.HasValue ? p.SecurityId.Value : 0;
                
                var prodDef = cache.ProdDefs.FirstOrDefault(pd => pd.Id == p.SecurityId);
                var quote = cache.Quotes.FirstOrDefault(o => o.Id == p.SecurityId.Value);
                if (prodDef != null)
                {
                    #region 计算PL
                    var tradeValue = p.InvestUSD * p.Leverage;
                    if (quote != null)
                    {
                        //直接用美元算的损益，就不用再转换美元了
                        decimal upl = p.LongQty.HasValue ? tradeValue.Value * (quote.Bid / p.SettlePrice.Value - 1) : tradeValue.Value * (1 - quote.Offer / p.SettlePrice.Value);
                        //var uplUSD = FX.ConvertPlByOutright(upl, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes);
                        //dto.pl = uplUSD;
                        dto.pl = upl;
                    }
                    dto.rate = p.InvestUSD.HasValue ? (dto.pl / p.InvestUSD.Value) : 0;
                    #endregion

                    dto.symbol = prodDef.Symbol;
                    dto.name = Translator.GetCName(prodDef.Name);
                }

                results.Add(dto);
            });

            return results;
        }

        [HttpGet]
        [Route("~/api/position/live/report")]
        [IPAuth]
        public List<PositionReportDTO> GetPositionsByUser(int userID)
        {
            var results = new List<PositionReportDTO>();
            var positions = db.NewPositionHistory_live.Where(p => p.UserId == userID).OrderByDescending(p => p.CreateTime).ToList();
            var cache = WebCache.GetInstance(true);

            positions.ForEach(p =>
            {

                var prodDef = cache.ProdDefs.FirstOrDefault(pd => pd.Id == p.SecurityId);

                var dto = new PositionReportDTO
                {
                    id = p.Id.ToString(),
                    openAt = p.CreateTime.Value,
                    openPrice = p.SettlePrice.Value,
                    invest = p.InvestUSD.Value,
                    leverage = p.Leverage.Value,
                    isLong = p.LongQty.HasValue,
                    security = Mapper.Map< SecurityDetailDTO>(prodDef),
                };
                if (p.ClosedAt == null)
                {
                    var tradeValue = p.InvestUSD*p.Leverage;
                    var quote = cache.Quotes.FirstOrDefault(o => o.Id == p.SecurityId.Value);
                    if (quote != null)
                    {
                        decimal upl = p.LongQty.HasValue
                            ? tradeValue.Value*(quote.Bid/p.SettlePrice.Value - 1)
                            : tradeValue.Value*(1 - quote.Offer/p.SettlePrice.Value);
                        dto.pl = upl;
                    }
                }
                else
                {
                    dto.closeAt = p.ClosedAt.Value;
                    dto.closePrice = p.ClosedPrice.Value;
                    dto.pl = p.PL.Value;
                    dto.isAutoClosed = p.IsAutoClosed??false;
                }

                results.Add(dto);
            });

            return results;
        }

        [HttpGet]
        [Route("~/api/position/live/report/openTime")]
        [IPAuth]
        public List<PositionReportDTO> GetPositionOpenTime(int week)
        {
            var weeksAgo = DateTime.UtcNow.AddDays(-week*7);

            var results = new List<PositionReportDTO>();
            var positions = db.NewPositionHistory_live.Where(o=>o.CreateTime> weeksAgo).OrderByDescending(p => p.CreateTime).ToList();

            positions.ForEach(p =>
            {

                var dto = new PositionReportDTO
                {
                    openAt = DateTime.SpecifyKind(p.CreateTime.Value, DateTimeKind.Utc),
                };

                results.Add(dto);
            });

            return results;
        }

        [HttpGet]
        [Route("~/api/position/live/report/investLev")]
        [IPAuth]
        public List<PositionReportDTO> GetPositionInvestLev(int day, int secId = 0)
        {
            var daysAgo = DateTime.UtcNow.AddDays(-day);

            var results = new List<PositionReportDTO>();
            var positions = db.NewPositionHistory_live.Where(o => o.CreateTime > daysAgo && (secId==0 || o.SecurityId==secId)).OrderByDescending(p => p.CreateTime).ToList();

            positions.ForEach(p =>
            {

                var dto = new PositionReportDTO
                {
                    invest =p.InvestUSD.Value,
                    leverage = p.Leverage.Value,
                };

                results.Add(dto);
            });

            return results;
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
        public List<PositionHistoryDTO> GetPositionHistory(DateTime? closedBefore = null, int count = 20)
        {
            var user = GetUser();

            if(!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            var startTime = DateTime.UtcNow.AddMonths(-3);
            if (closedBefore == null)
                closedBefore = DateTime.MaxValue;
            else
            {
                if (closedBefore.Value.Kind == DateTimeKind.Local)
                    closedBefore = closedBefore.Value.ToUniversalTime();
            }

            var positions = IsLiveUrl
                ? db.NewPositionHistory_live.Where(
                    o => o.ClosedAt >= startTime && o.ClosedAt < closedBefore && o.UserId == UserId)
                    .OrderByDescending(o => o.ClosedAt).Take(count)
                    .ToList().Select(o => o as NewPositionHistoryBase).ToList()
                : db.NewPositionHistories.Where(
                    o => o.ClosedAt >= startTime && o.ClosedAt < closedBefore && o.UserId == UserId)
                    .OrderByDescending(o => o.ClosedAt).Take(count)
                    .ToList().Select(o => o as NewPositionHistoryBase).ToList();

            //need to get latest data from FIX?
            if (closedBefore > DateTime.UtcNow.AddDays(-3))
            {
                //get all fix data
                IList<PositionReport> fixReports;
                var endTimeFix = DateTime.UtcNow;
                var startTimeFix = DateTimes.GetHistoryQueryStartTime(endTimeFix);
                using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
                {
                    try
                    {
                        fixReports =
                            clientHttp.GetPositionHistoryReport(IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                                IsLiveUrl ? null : user.AyondoPassword, startTimeFix, endTimeFix).ToList();
                    }
                    catch (FaultException<OAuthLoginRequiredFault>)
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                            __(TransKey.OAUTH_LOGIN_REQUIRED)));
                    }
                }

                //get closed data that are only in FIX
                var dbPosIds = positions.Select(o => o.Id.ToString()).ToList();
                var fixOnlyClosedReports =
                    fixReports.Where(
                        o => !dbPosIds.Contains(o.PosMaintRptID) &&
                            o.CreateTime < closedBefore && o.CreateTime >= startTime &&
                            (Decimals.IsTradeSizeZero(o.LongQty) || Decimals.IsTradeSizeZero(o.ShortQty))
                            ).ToList();

                //add fix data to result
                if (fixOnlyClosedReports.Count > 0)
                {
                    var posIds = fixOnlyClosedReports.Select(o => Convert.ToInt64(o.PosMaintRptID)).ToList();

                    var supplementPositions = IsLiveUrl
                        ? db.NewPositionHistory_live.Where(o => posIds.Contains(o.Id)).ToList().Select(o => o as NewPositionHistoryBase).ToList()
                        : db.NewPositionHistories.Where(o => posIds.Contains(o.Id)).ToList().Select(o => o as NewPositionHistoryBase).ToList();

                    foreach (var p in supplementPositions)
                    {
                        var first = fixOnlyClosedReports.First(o => Convert.ToInt64(o.PosMaintRptID) == p.Id);
                        p.ClosedAt = first.CreateTime;
                        p.ClosedPrice = first.SettlPrice;
                        p.PL = first.PL;
                    }

                    positions = positions.Concat(supplementPositions).OrderByDescending(o => o.ClosedAt).Take(count).ToList();
                }
            }

            //create DTOs
            var cache = WebCache.GetInstance(IsLiveUrl);
            var result = positions.Select(o =>
            {
                var prodDef = cache.ProdDefs.FirstOrDefault(p => p.Id == o.SecurityId);

                if (prodDef == null)
                {
                    CFDGlobal.LogLine("cannot find product definition for sec id: " + o.SecurityId + " in history position reports of user id: " + UserId);
                    return null;
                }

                return new PositionHistoryDTO
                {
                    id = o.Id.ToString(),

                    invest = o.SettlePrice.Value*prodDef.LotSize/prodDef.PLUnits*(o.LongQty ?? o.ShortQty)/o.Leverage,
                    isLong = o.LongQty != null,
                    leverage = o.Leverage,

                    openAt = o.CreateTime.Value,
                    closeAt = o.ClosedAt.Value,

                    openPrice = Decimals.RoundIfExceed(o.SettlePrice.Value, prodDef.Prec),
                    closePrice = Decimals.RoundIfExceed(o.ClosedPrice.Value, prodDef.Prec),

                    pl = o.PL.Value,

                    security = new SecurityDetailDTO()
                    {
                        id = prodDef.Id,
                        symbol = prodDef.Symbol,
                        name = Translator.GetCName(prodDef.Name),
                        ccy = prodDef.Ccy2,
                        dcmCount = prodDef.Prec,
                    }
                };
            }).Where(o => o != null).ToList();

            //financing/dividends
            var posIDs = result.Select(o => Convert.ToInt64(o.id)).ToList();
            var transferHistories = IsLiveUrl
                ? db.AyondoTransferHistory_Live.Where(o => o.PositionId.HasValue && posIDs.Contains(o.PositionId.Value)).ToList().Select(o => o as AyondoTransferHistoryBase).ToList()
                : db.AyondoTransferHistories.Where(o => o.PositionId.HasValue && posIDs.Contains(o.PositionId.Value)).ToList().Select(o => o as AyondoTransferHistoryBase).ToList();
            result.ForEach(posDTO =>
            {
                var financings = transferHistories.Where(o => o.PositionId.ToString() == posDTO.id && o.TransferType == "Financing").ToList();
                var dividends = transferHistories.Where(o => o.PositionId.ToString() == posDTO.id && o.TransferType == "Dividend").ToList();
                if (financings.Count > 0) posDTO.financingSum = financings.Sum(o => o.Amount);
                if (dividends.Count > 0) posDTO.dividendSum = dividends.Sum(o => o.Amount);
            });

            return result;


            /*
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

            //financing/dividends
            var posIDs = result.Select(o => Convert.ToInt64(o.id)).ToList();
            var transferHistories = IsLiveUrl
                ? db.AyondoTransferHistory_Live.Where(o => o.PositionId.HasValue && posIDs.Contains(o.PositionId.Value)).ToList().Select(o => o as AyondoTransferHistoryBase).ToList()
                : db.AyondoTransferHistories.Where(o => o.PositionId.HasValue && posIDs.Contains(o.PositionId.Value)).ToList().Select(o => o as AyondoTransferHistoryBase).ToList();
            result.ForEach(posDTO =>
            {
                var financings = transferHistories.Where(o => o.PositionId.ToString() == posDTO.id && o.TransferType == "Financing").ToList();
                var dividends = transferHistories.Where(o => o.PositionId.ToString() == posDTO.id && o.TransferType == "Dividend").ToList();
                if (financings.Count > 0) posDTO.financingSum = financings.Sum(o => o.Amount);
                if (dividends.Count > 0) posDTO.dividendSum = dividends.Sum(o => o.Amount);
            });

            return result.OrderByDescending(o => o.closeAt).ToList();
            */
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

        /// <summary>
        /// 达人榜个人主页显示的用户平仓记录，只取前20条
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("~/api/user/{userId}/live/position/closed")]
        [BasicAuth]
        public List<SimplePositionDTO> GetSimpleClosedPositions(int userID)
        {
            if (userID != UserId) //not myself
            {
                var user = db.Users.FirstOrDefault(o => o.Id == userID);
                if (user == null || !(user.ShowData ?? false))
                    return new List<SimplePositionDTO>();
            }

            List<SimplePositionDTO> results = new List<SimplePositionDTO>();
            var positions = db.NewPositionHistory_live.Where(p => p.UserId == userID && p.ClosedAt.HasValue).OrderByDescending(p=>p.Id).Take(20).ToList();
            var cache = WebCache.GetInstance(true);

            positions.ForEach(p => {
                SimplePositionDTO dto = new SimplePositionDTO();
                dto.id = p.SecurityId.HasValue ? p.SecurityId.Value : 0;
                dto.pl = p.PL.HasValue ? p.PL.Value : 0;
                dto.rate = p.PL.HasValue && p.InvestUSD.HasValue ? (p.PL.Value / p.InvestUSD.Value) : 0;
                dto.rate = Math.Round(dto.rate, 4);
                var prodDef = cache.ProdDefs.FirstOrDefault(pd => pd.Id == p.SecurityId);
                if (prodDef != null)
                {
                    dto.symbol = prodDef.Symbol;
                    dto.name = Translator.GetCName(prodDef.Name);
                }

                results.Add(dto);
            });

            return results;
        }

        [HttpPost]
        [Route("")]
        [Route("live")]
        [BasicAuth2]
        public PositionDTO NewPosition(NewPositionFormDTO form, bool ignorePriceDelay = false)
        {
            var user = GetUser();
            int score = 0;
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
            if(!ignorePriceDelay && Quotes.IsPriceDown(cache.GetProdSettingByID(quote.Id), quote.Time))
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
                //if (IsLiveUrl && user.AyLiveAccountId != accountId)
                //    user.AyLiveAccountId = accountId;
                if (!IsLiveUrl && user.AyondoAccountId != accountId)
                    user.AyondoAccountId = accountId;

                #region 计算积分 - 仅实盘
                if(IsLiveUrl)
                {
                    try
                    {
                        var scoreSetting = GetScoresSetting();
                        score = (int)(form.invest * form.leverage * scoreSetting.LiveOrder);

                        db.ScoreHistorys.Add(new ScoreHistory()
                        {
                            UserID = UserId,
                            Score = score,
                            Source = ScoreSource.LiveOrder,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    catch (Exception ex)
                    {
                        CFDGlobal.LogWarning("Error getting score when open position: " +
                                                  ex.Message);
                    }
                }
                #endregion

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
                invest = tradedValue.Value / result.Leverage.Value,
                leverage = result.Leverage.Value,
                createAt = result.CreateTime,
                quantity = result.LongQty ?? result.ShortQty.Value,
                stopPx = result.StopPx,
                stopOID = result.StopOID,
                score = score
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
                            themeColor = card.ThemeColor,
                            cardType = card.CardType.HasValue ? card.CardType.Value : 0,
                            title = card.Title
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

            //update NewPositionHistory table
            try
            {
                var posId = Convert.ToInt64(report.PosMaintRptID);
                var position = IsLiveUrl
                    ? (NewPositionHistoryBase) db.NewPositionHistory_live.FirstOrDefault(o => o.Id == posId)
                    : db.NewPositionHistories.FirstOrDefault(o => o.Id == posId);
                if (position != null)
                {
                    position.TakePx = report.TakePx;
                    db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                CFDGlobal.LogExceptionAsWarning(e);
            }

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

            //update NewPositionHistory table
            try
            {
                var posId = Convert.ToInt64(report.PosMaintRptID);
                var position = IsLiveUrl
                    ? (NewPositionHistoryBase)db.NewPositionHistory_live.FirstOrDefault(o => o.Id == posId)
                    : db.NewPositionHistories.FirstOrDefault(o => o.Id == posId);
                if (position != null)
                {
                    position.TakePx = null;
                    db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                CFDGlobal.LogExceptionAsWarning(e);
            }

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

            //update NewPositionHistory table
            try
            {
                var posId = Convert.ToInt64(report.PosMaintRptID);
                var position = IsLiveUrl
                    ? (NewPositionHistoryBase)db.NewPositionHistory_live.FirstOrDefault(o => o.Id == posId)
                    : db.NewPositionHistories.FirstOrDefault(o => o.Id == posId);
                if (position != null)
                {
                    position.TakePx = report.TakePx;
                    position.StopPx = report.StopPx;
                    db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                CFDGlobal.LogExceptionAsWarning(e);
            }

            return posDTO;
        }

        [HttpGet]
        [Route("~/api/user/{userId}/position/chart/plClosed")]
        [Route("~/api/user/{userId}/live/position/chart/plClosed")]
        [BasicAuth]
        public List<PosChartDTO> PLChartClosed(int userId)
        {
            //var user = db.Users.FirstOrDefault(o => o.Id == userId);
            //if (!(user.ShowData ?? true) && userId != UserId)//not showing data && not myself
            //    return new List<PosChartDTO>();

            var dbList = IsLiveUrl
                ? db.NewPositionHistory_live.Where(o => o.UserId == userId && o.ClosedAt != null)
                    .OrderBy(o => o.ClosedAt)
                    .ToList()
                    .Select(o => o as NewPositionHistoryBase)
                    .ToList()
                : db.NewPositionHistories.Where(o => o.UserId == userId && o.ClosedAt != null)
                    .OrderBy(o => o.ClosedAt)
                    .ToList()
                    .Select(o => o as NewPositionHistoryBase)
                    .ToList();

            if (dbList.Count == 0)
                return new List<PosChartDTO>();

            var result = dbList.GroupBy(o => o.ClosedAt.Value.AddHours(8).Date).Select(o => new PosChartDTO
            {
                date = o.Key,
                //Count = o.Count(),
                pl = o.Sum(p => p.PL.Value)
            }).ToList();

            #region fill-in all data points for client...

            var beginDate = DateTime.SpecifyKind(result.First().date, DateTimeKind.Utc);
            var endDate = DateTimes.GetChinaToday();
            var newResult = new List<PosChartDTO>();
            decimal cumulativePL = 0;
            for (DateTime d = beginDate; d <= endDate; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Sunday) continue;

                var data = result.FirstOrDefault(o => o.date == d);

                if (data == null)
                    newResult.Add(new PosChartDTO() { date = d, pl = cumulativePL });
                else
                {
                    cumulativePL += data.pl;
                    newResult.Add(new PosChartDTO() { date = d, pl = cumulativePL });
                }
            }

            #endregion

            var user = db.Users.FirstOrDefault(o => o.Id == userId);
            if (!(user.ShowData ?? false) && userId != UserId) //not showing data && not myself
            {
                //data obfuscation
                var max = newResult.Max(o => o.pl);
                var min = newResult.Min(o => o.pl);
                var ratio = 100/(max - min);
                foreach (var dto in newResult)
                {
                    dto.pl = (dto.pl - min)*ratio;
                }
            }

            return newResult;
        }

        [HttpGet]
        [Route("~/api/user/{userId}/position/chart/plClosed/2w")]
        [Route("~/api/user/{userId}/live/position/chart/plClosed/2w")]
        [BasicAuth]
        public List<PosChartDTO> PLChartClosed2w(int userId)
        {
            //var user = db.Users.FirstOrDefault(o => o.Id == userId);
            //if (!(user.ShowData ?? true) && userId != UserId)//not showing data && not myself
            //    return new List<PosChartDTO>();

            var twoWeeksAgo = DateTimes.GetChinaToday().AddDays(-13);
            var twoWeeksAgoUtc = twoWeeksAgo.AddHours(-8);

            var dbList = IsLiveUrl
                ? db.NewPositionHistory_live.Where(o => o.UserId == userId && o.ClosedAt != null && o.ClosedAt >= twoWeeksAgoUtc)
                    .OrderBy(o => o.ClosedAt)
                    .ToList()
                    .Select(o => o as NewPositionHistoryBase)
                    .ToList()
                : db.NewPositionHistories.Where(o => o.UserId == userId && o.ClosedAt != null && o.ClosedAt >= twoWeeksAgoUtc)
                    .OrderBy(o => o.ClosedAt)
                    .ToList()
                    .Select(o => o as NewPositionHistoryBase)
                    .ToList();

            //if (dbList.Count == 0)
            //    return new List<PosChartDTO>();

            var result = dbList.GroupBy(o => o.ClosedAt.Value.AddHours(8).Date).Select(o => new PosChartDTO
            {
                date = o.Key,
                //Count = o.Count(),
                pl = o.Sum(p => p.PL.Value)
            }).ToList();

            #region fill-in all data points for client...

            var beginDate = twoWeeksAgo;
            var endDate = DateTimes.GetChinaToday();
            var newResult = new List<PosChartDTO>();
            decimal cumulativePL = 0;
            for (DateTime d = beginDate; d <= endDate; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Sunday) continue;

                var data = result.FirstOrDefault(o => o.date == d);

                if (data == null)
                    newResult.Add(new PosChartDTO() { date = d, pl = cumulativePL });
                else
                {
                    cumulativePL += data.pl;
                    newResult.Add(new PosChartDTO() { date = d, pl = cumulativePL });
                }
            }

            #endregion

            var user = db.Users.FirstOrDefault(o => o.Id == userId);
            if (!(user.ShowData ?? false) && userId != UserId) //not showing data && not myself
            {
                //data obfuscation
                var max = newResult.Max(o => o.pl);
                var min = newResult.Min(o => o.pl);
                var ratio = 100 / (max - min);
                foreach (var dto in newResult)
                {
                    dto.pl = (dto.pl - min) * ratio;
                }
            }

            return newResult;
        }

        [HttpGet]
        [Route("~/api/user/{userId}/detail")]
        [Route("~/api/user/{userId}/live/detail")]
        [BasicAuth]
        public UserDetailDTO Get(int userId)
        {
            var user = db.Users.FirstOrDefault(o => o.Id == userId);

            var positions = IsLiveUrl
                ? db.NewPositionHistory_live.Where(o => o.UserId == userId && o.PL != null)
                    .ToList()
                    .Select(o => o as NewPositionHistoryBase)
                    .ToList()
                : db.NewPositionHistories.Where(o => o.UserId == userId && o.PL != null)
                    .ToList()
                    .Select(o => o as NewPositionHistoryBase)
                    .ToList();

            var isEmpty = positions.Count == 0;

            var followingCount = db.UserFollows.Count(o => o.FollowingId == userId);

            var isFollowing = db.UserFollows.Any(o => o.UserId == UserId && o.FollowingId == userId);
            var rank = db.LiveRanks.FirstOrDefault(o => o.Rank == user.LiveRank);
            string rankDescription = rank == null? "默默无闻" : rank.Description;

            var result = new UserDetailDTO();
            result.id = userId;
            result.nickname = user.Nickname;
            result.picUrl = user.PicUrl;
            result.isFollowing = isFollowing;
            result.showData = user.ShowData ?? false;
            result.rankDescription = string.Empty; //默认值，为了使返回的json包含该字段
            result.cards = new List<CardDTO>(); //默认值，为了使返回的json包含该字段

            if (result.showData || result.id == UserId) //user.showdata or mySelf?
            {
                result.rank = user.LiveRank.HasValue ? user.LiveRank.Value : 0;
                result.rankDescription = rankDescription;

                result.avgPl = isEmpty ? 0 : positions.Average(o => o.PL.Value);
                result.totalPl = isEmpty ? 0 : positions.Sum(o => o.PL.Value);

                var twoWeeksAgo = DateTimes.GetChinaToday().AddDays(-13);
                var twoWeeksAgoUtc = twoWeeksAgo.AddHours(-8);
                var positions2w = positions.Where(o => o.ClosedAt > twoWeeksAgoUtc).ToList();
                result.pl2w = positions2w.Count > 0 ? positions2w.Sum(o => o.PL).Value : 0;

                result.winRate = isEmpty ? 0 : (decimal) positions.Count(o => o.PL > 0)/positions.Count;
                result.followerCount = followingCount;

                result.avgHoldPeriod = isEmpty ? 0 : positions.Average(p => {
                    if(!p.CreateTime.HasValue)
                    { return 0; }
                    else if(!p.ClosedAt.HasValue)
                    {
                        return Convert.ToDecimal((DateTime.Now - p.CreateTime.Value).TotalHours);
                    }
                    else
                    {
                        return Convert.ToDecimal((p.ClosedAt.Value - p.CreateTime.Value).TotalHours);
                    }
                });
                //为了把小数点后第二位也向上取整，所以先除100，再乘100
                result.avgHoldPeriod = Math.Ceiling((result.avgHoldPeriod / 24) * 100) / 100;
                result.avgInvestUSD = isEmpty ? 0 : (int)(positions.Average(p => p.InvestUSD.HasValue? p.InvestUSD : 0));
                result.avgLeverage = isEmpty ? 0 : (int)(positions.Sum(p => p.InvestUSD.Value * p.Leverage.Value) / positions.Sum(p => p.InvestUSD.Value));
                result.orderCount = positions.Count;

                if (IsLiveUrl)
                {
                    var cards = from u in db.UserCards_Live
                        join c in db.Cards on u.CardId equals c.Id
                            into x
                        from y in x.DefaultIfEmpty()
                        where u.UserId == userId
                        //------------user id
                        orderby u.CreatedAt descending
                        select new CardDTO()
                        {
                            cardId = u.Id,
                            //ccy = u.CCY,
                            imgUrlBig = y.CardImgUrlBig,
                            imgUrlMiddle = y.CardImgUrlMiddle,
                            imgUrlSmall = y.CardImgUrlSmall,
                            invest = u.Invest,
                            isLong = u.IsLong,
                            isNew = !u.IsNew.HasValue ? true : u.IsNew.Value,
                            shared = !u.IsShared.HasValue ? false : u.IsShared.Value,
                            leverage = u.Leverage,
                            likes = u.Likes,
                            reward = y.Reward,
                            settlePrice = u.SettlePrice,
                            //stockName = u.StockName,
                            stockID = u.SecurityId,
                            pl = u.PL,
                            plRate =
                                ((u.SettlePrice - u.TradePrice)/u.TradePrice*u.Leverage*100)*(u.IsLong.Value ? 1 : -1),
                            themeColor = y.ThemeColor,
                            title = y.Title,
                            cardType = y.CardType.HasValue ? y.CardType.Value : 0,
                            tradePrice = u.TradePrice,
                            tradeTime = u.ClosedAt
                        };

                    if (cards != null)
                    {
                        var cache = WebCache.GetInstance(true);
                        result.cards = cards.ToList();
                        result.cards.ForEach(cardDTO =>
                        {
                            var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == cardDTO.stockID);
                            if (prodDef != null)
                            {
                                cardDTO.ccy = prodDef.Ccy2;
                                cardDTO.stockName = Translator.GetCName(prodDef.Name);
                            }
                        });
                    }
                }
            }

            return result;
        }

        [HttpGet]
        [Route("printcache")]
        [Route("live/printcache")]
        public string PrintCache(string username = "")
        {
            string result;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
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
        [Route("live/clearcache")]
        public void ClearCache(string username = "")
        {
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
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