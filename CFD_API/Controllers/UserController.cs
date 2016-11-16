using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

namespace CFD_API.Controllers
{
    [RoutePrefix("api/user")]
    public class UserController : CFDController
    {
        //public UserController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
        //    : base(db, mapper, redisClient)
        //{
        //}

        public UserController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        private static readonly TimeSpan VERIFY_CODE_PERIOD = TimeSpan.FromHours(1);
        private const int NICKNAME_MAX_LENGTH=8;

        [HttpPost]
        //[RequireHttps]
        //[RestrictByIp]
        [ActionName("signupByPhone")]
        public SignupResultDTO SignupByPhone(SignupByPhoneFormDTO form)
        {
            var result = new SignupResultDTO();

            if (IsLoginBlocked(form.phone))
            {
                result.success = false;
                result.message = __(TransKey.PHONE_SIGNUP_FORBIDDEN);
                return result;
            }

            //verify this login
            var dtValidSince = DateTime.UtcNow - VERIFY_CODE_PERIOD;
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince);

            //auth success
            if (Phone.IsTrustedPhone(form.phone) || verifyCodes.Any())
            {
                var user = db.Users.FirstOrDefault(o => o.Phone == form.phone);

                if (user == null) //phone doesn't exist
                {
                    var userService = new UserService(db);
                    userService.CreateUserByPhone(form.phone);

                    //refetch
                    user = db.Users.FirstOrDefault(o => o.Phone == form.phone);

                    var nickname = "u" + user.Id.ToString("000000");
                    user.Nickname = nickname;

                    //check duplicate nickname and generate random suffix
                    int tryCount = 0;
                    while (db.Users.Any(o => o.Id != user.Id && o.Nickname == user.Nickname))
                    {
                        user.Nickname = nickname.TruncateMax(4) + Randoms.GetRandomAlphabeticString(4);

                        tryCount++;

                        if (tryCount > 10)
                        {
                            CFDGlobal.LogWarning("Tryout exceeded: signupByPhone - check duplicate nickname and generate random suffix");
                            break;
                        }
                    }

                    db.SaveChanges();

                    result.success = true;
                    result.isNewUser = true;
                    result.userId = user.Id;
                    result.token = user.Token;
                }
                else //phone exists
                {
                    result.success = true;
                    result.isNewUser = false;
                    result.userId = user.Id;
                    result.token = user.Token;
                }

                if (user.AyondoUsername == null)
                    try
                    {
                        CreateAyondoDemoAccount(user);
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogException(e);
                    }
            }
            else
            {
                //add login history ONLY WHEN AUTH FAILED
                db.PhoneSignupHistories.Add(new PhoneSignupHistory() { CreateAt = DateTime.UtcNow, Phone = form.phone });
                db.SaveChanges();

                result.success = false;
                result.message = __(TransKey.INVALID_VERIFY_CODE);
            }

            return result;
        }

        [HttpPost]
        //[RequireHttps]
        [ActionName("signupByWeChat")]
        public SignupResultDTO SignupByWeChat(SignupByWeChatFormDTO form)
        {
            if (string.IsNullOrEmpty(form.openid) || string.IsNullOrEmpty(form.unionid))
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, ""));

            var result = new SignupResultDTO();

            var user = db.Users.FirstOrDefault(o => o.WeChatOpenId == form.openid);

            if (user == null) //openid not exist
            {
                var userService = new UserService(db);
                userService.CreateUserByWeChat(form.openid, form.unionid);

                //refetch
                user = db.Users.FirstOrDefault(o => o.WeChatOpenId == form.openid);

                user.Nickname = form.nickname.Trim().TruncateMax(NICKNAME_MAX_LENGTH);

                //check duplicate nickname and generate random suffix
                int tryCount = 0;
                while (db.Users.Any(o => o.Id != user.Id && o.Nickname == user.Nickname))
                {
                    user.Nickname = form.nickname.TruncateMax(4) + Randoms.GetRandomAlphanumericString(4);

                    tryCount++;

                    if (tryCount > 10)
                    {
                        CFDGlobal.LogWarning("Tryout exceeded: signupByWeChat - check duplicate nickname and generate random suffix");
                        break;
                    }
                }

                //save wechat pic to azure storage blob
                if (form.headimgurl != null)
                {
                    try
                    {
                        var webClient = new WebClient();
                        var bytes = webClient.DownloadData(form.headimgurl);

                        var picName = Guid.NewGuid().ToString("N");

                        Blob.UploadFromBytes(CFDGlobal.USER_PIC_BLOB_CONTAINER, picName, bytes);

                        user.PicUrl = CFDGlobal.USER_PIC_BLOB_CONTAINER_URL + picName;
                    }
                    catch (Exception ex)
                    {
                        CFDGlobal.LogWarning("Fail saving wechat picture to azure blob");
                        CFDGlobal.LogException(ex);
                    }
                }

                db.SaveChanges();

                result.success = true;
                result.isNewUser = true;
                result.userId = user.Id;
                result.token = user.Token;
            }
            else //openid exists
            {
                result.success = true;
                result.isNewUser = false;
                result.userId = user.Id;
                result.token = user.Token;

                //TODO:if user is from wechat but user.picurl is null, reload img?
            }

            if (user.AyondoUsername == null)
                try
                {
                    CreateAyondoDemoAccount(user);
                }
                catch (Exception e)
                {
                    CFDGlobal.LogExceptionAsWarning(e);
                }

            return result;
        }

        //todo: for test only
        [HttpGet]
        [BasicAuth]
        [ActionName("resetAyondoAccount")]
        public void ResetAyondoAccount()
        {
            var user = GetUser();
            CreateAyondoDemoAccount(user);
        }

        [HttpGet]
        //[RequireHttps]
        [ActionName("me")]
        [BasicAuth]
        public UserDTO GetMe(LoginFormDTO form)
        {
            var user = GetUser();

            var userDto = Mapper.Map<UserDTO>(user);

            //TODO: only here to reward demo registration?
            //todo: transaction required!
            if(!db.DemoRegisterRewards.Any(item => item.UserId == this.UserId))
            {
                var reward = new DemoRegisterReward()
                {
                    Amount = RewardService.REWARD_DEMO_REG,
                    ClaimedAt = null,
                    UserId = UserId,
                };
                db.DemoRegisterRewards.Add(reward);
                db.SaveChanges();

                userDto.rewardAmount = reward.Amount;
            }

            userDto.liveAccStatus = GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);

            if (userDto.liveAccStatus == UserLiveStatus.Rejected)
                userDto.liveAccRejReason = GetUserLiveAccountRejectReason(user.AyLiveAccountStatus);

            return userDto;
        }

        [HttpPost]
        //[RequireHttps]
        [ActionName("nickname")]
        [BasicAuth]
        public ResultDTO SetNickname(string nickname)
        {
            if (nickname.Length > NICKNAME_MAX_LENGTH)
                return new ResultDTO() {success = false, message = __(TransKey.NICKNAME_TOO_LONG)};

            if (db.Users.Any(o => o.Id != UserId && o.Nickname == nickname))
                return new ResultDTO
                {
                    success = false,
                    message = __(TransKey.NICKNAME_EXISTS)
                };

            var user = GetUser();
            user.Nickname = nickname;
            db.SaveChanges();

            return new ResultDTO {success = true};
        }

        [HttpPost]
        [ActionName("photo")]
        [BasicAuth]
        public ResultDTO SetPhoto()
        {
            var requestString = Request.Content.ReadAsStringAsync().Result;
            var bytes = Convert.FromBase64String(requestString);

            var user = GetUser();

            string picName = string.Empty;
            if(!string.IsNullOrEmpty(user.PicUrl)) //delete existing blob before upload
            {
                picName = user.PicUrl.Split('/').Last();
                Blob.DeleteBlob(CFDGlobal.USER_PIC_BLOB_CONTAINER, picName);
            }
           
            picName = Guid.NewGuid().ToString("N"); //upload photo with a new name, b/c client will not refresh with same name.
            Blob.UploadFromBytes(CFDGlobal.USER_PIC_BLOB_CONTAINER, picName, bytes);

            user.PicUrl = CFDGlobal.USER_PIC_BLOB_CONTAINER_URL + picName;
            db.SaveChanges();

            return new ResultDTO { success = true };
        }

        [HttpPost]
        [Route("alert/{setting}")]
        [BasicAuth]
        public ResultDTO SetSystemAlert(bool setting)
        {
            var user = GetUser();

            user.AutoCloseAlert = setting;
            db.SaveChanges();

            return new ResultDTO { success = true };
        }

        [HttpPost]
        [ActionName("bindphone")]
        [BasicAuth]
        public ResultDTO BindPhone(BindPhoneDTO form)
        {
            if(!Phone.IsValidPhoneNumber(form.phone))
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,__(TransKey.INVALID_PHONE_NUMBER)));

            var user = GetUser();

            if (user.Phone != null)
            {
                if (user.Phone == form.phone)
                    return new ResultDTO { success = true };
                else
                    return new ResultDTO { success = false, message = __(TransKey.PHONE_ALREADY_BOUND) };
            }

            if (db.Users.Any(o => o.Phone == form.phone))
                return new ResultDTO {success = false, message = __(TransKey.PHONE_EXISTS)};

            ResultDTO result = new ResultDTO();

            //check verify block
            if (IsLoginBlocked(form.phone))
            {
                result.success = false;
                result.message = __(TransKey.PHONE_SIGNUP_FORBIDDEN);
                return result;
            }

            var dtValidSince = DateTime.UtcNow - VERIFY_CODE_PERIOD;
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince);
            
            if (verifyCodes.Any())//verifiy succeed
            {
                var userService=new UserService(db);
                userService.BindPhone(UserId,form.phone);

                result.success = true;
            }
            else//verify failed
            {
                db.PhoneSignupHistories.Add(new PhoneSignupHistory() {CreateAt = DateTime.UtcNow, Phone = form.phone});
                db.SaveChanges();

                result.success = false;
                result.message = __(TransKey.INVALID_VERIFY_CODE);
            }

            return result;
        }

        [HttpPost]
        [ActionName("bindwechat")]
        [BasicAuth]
        public ResultDTO BindWechat(string openId)
        {
            var user = GetUser();

            if (user.WeChatOpenId != null)
            {
                if (user.WeChatOpenId == openId)
                    return new ResultDTO {success = true};
                else
                    return new ResultDTO {success = false, message = __(TransKey.WECHAT_ALREADY_BOUND)};
            }

            if (db.Users.Any(o => o.WeChatOpenId == openId))
                return new ResultDTO {success = false, message = __(TransKey.WECHAT_OPENID_EXISTS)};

            var userService = new UserService(db);
            userService.BindWechat(UserId, openId);

            return new ResultDTO {success = true};
        }

        [HttpGet]
        [Route("balance")]
        [Route("live/balance")]
        [BasicAuth]
        public BalanceDTO GetBalance(bool ignoreCache = false)
        {
            var user = GetUser();

            if(!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            decimal balance;
            IList<PositionReport> positionReports;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    balance = clientHttp.GetBalance(
                        IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                        IsLiveUrl ? null : user.AyondoPassword);
                    positionReports = clientHttp.GetPositionReport(
                        IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                        IsLiveUrl ? null : user.AyondoPassword,
                        ignoreCache);
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            //var redisProdDefClient = RedisClient.As<ProdDef>();
            //var redisQuoteClient = RedisClient.As<Quote>();

            //var prodDefs = redisProdDefClient.GetAll();
            //var quotes = redisQuoteClient.GetAll();

            var cache = WebCache.GetInstance(IsLiveUrl);

            decimal marginUsed = 0;
            decimal totalUPL = 0;
            foreach (var report in positionReports)
            {
                var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));

                if (prodDef == null) continue;

                //************************************************************************
                //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                //************************************************************************
                var tradeValue = report.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(report.LongQty ?? report.ShortQty);

                marginUsed += FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes)/report.Leverage.Value;

                //calculate UPL
                var quote = cache.Quotes.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                if (quote != null)
                {
                    var upl = report.LongQty.HasValue ? tradeValue.Value*(quote.Bid/report.SettlPrice - 1) : tradeValue.Value*(1 - quote.Offer/report.SettlPrice);
                    totalUPL += FX.ConvertPlByOutright(upl, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes);
                }
                else
                {
                    CFDGlobal.LogWarning("cannot find quote:" + report.SecurityID +" when calculating UPL for totalUPL");
                }
            }

            return new BalanceDTO()
            {
                id = user.Id,
                balance = balance,
                total = balance + totalUPL,
                available = balance - marginUsed
            };
        }

        [HttpGet]
        [ActionName("plReport_obsolete")]
        [BasicAuth]
        public List<PLReportDTO> GetPLReport()
        {
            var user = GetUser();

            CheckAndCreateAyondoDemoAccount(user);

            var endTime = DateTime.UtcNow;
            var startTime = DateTimes.GetHistoryQueryStartTime(endTime);

            IList<PositionReport> positionOpenReports;
            IList<PositionReport> positionHistoryReports;
            using (var clientHttp = new AyondoTradeClient())
            {
                try
                {
                    positionOpenReports = clientHttp.GetPositionReport(user.AyondoUsername, user.AyondoPassword);
                    positionHistoryReports = clientHttp.GetPositionHistoryReport(user.AyondoUsername, user.AyondoPassword, startTime, endTime);
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            //var secIds = positionOpenReports.Select(o => o.SecurityID).Concat(positionHistoryReports.Select(o => o.SecurityID)).Distinct().Select(o => Convert.ToInt32(o)).ToList();
            //var dbSecurities = db.AyondoSecurities.Where(o => secIds.Contains(o.Id)).ToList();

            //var redisProdDefClient = RedisClient.As<ProdDef>();
            //var redisQuoteClient = RedisClient.As<Quote>();

            //var prodDefs = redisProdDefClient.GetAll();
            //var quotes = redisQuoteClient.GetAll();

            var indexPL = new PLReportDTO() {name = "指数"};
            var fxPL = new PLReportDTO() {name = "外汇"};
            var commodityPL = new PLReportDTO() {name = "商品"};
            var stockUSPL = new PLReportDTO() {name = "美股"};

            //open positions
            foreach (var report in positionOpenReports)
            {
                var secId = Convert.ToInt32(report.SecurityID);

                var prodDef = WebCache.Demo.ProdDefs.FirstOrDefault(o => o.Id == secId);

                if (prodDef == null) continue;

                //var dbSec = dbSecurities.FirstOrDefault(o => o.Id == secId);

                //************************************************************************
                //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                //************************************************************************
                var tradeValue = report.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(report.LongQty ?? report.ShortQty);

                var invest = FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2, "USD", WebCache.Demo.ProdDefs, WebCache.Demo.Quotes)/report.Leverage.Value;

                //calculate UPL
                decimal uplUSD = 0;
                var quote = WebCache.Demo.Quotes.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                if (quote != null)
                {
                    var upl = report.LongQty.HasValue ? tradeValue.Value * (quote.Bid / report.SettlPrice - 1) : tradeValue.Value * (1 - quote.Offer / report.SettlPrice);
                    uplUSD = FX.ConvertPlByOutright(upl, prodDef.Ccy2, "USD", WebCache.Demo.ProdDefs, WebCache.Demo.Quotes);
                }
                else
                {
                    CFDGlobal.LogWarning("cannot find quote:" + report.SecurityID + " when calculating UPL for PLReport");
                }

                if (prodDef.AssetClass == "Stock Indices")
                {
                    indexPL.invest += invest;
                    indexPL.pl += uplUSD;
                }
                else if (prodDef.AssetClass == "Currencies")
                {
                    fxPL.invest += invest;
                    fxPL.pl += uplUSD;
                }
                else if (prodDef.AssetClass == "Commodities")
                {
                    commodityPL.invest += invest;
                    commodityPL.pl += uplUSD;
                }
                else if (prodDef.AssetClass == "Single Stocks" && Products.IsUSStocks(prodDef.Symbol))
                {
                    stockUSPL.invest += invest;
                    stockUSPL.pl += uplUSD;
                }
            }

            var groupByPositions = positionHistoryReports.GroupBy(o => o.PosMaintRptID);

            //closed positions
            foreach (var positionGroup in groupByPositions)
            {
                var dto = new PositionHistoryDTO();
                dto.id = positionGroup.Key;

                var reports = positionGroup.ToList();

                if (reports.Count >= 2)
                {
                    var openReport = reports.OrderBy(o => o.CreateTime).First();
                    var closeReport = reports.OrderBy(o => o.CreateTime).Last();

                    if (Decimals.IsTradeSizeZero(closeReport.LongQty) || Decimals.IsTradeSizeZero(closeReport.ShortQty))
                    {
                        var secId = Convert.ToInt32(openReport.SecurityID);

                        var prodDef = WebCache.Demo.ProdDefs.FirstOrDefault(o => o.Id == secId);

                        if (prodDef == null) continue;

                        //var dbSec = dbSecurities.FirstOrDefault(o => o.Id == secId);

                        //************************************************************************
                        //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                        //************************************************************************
                        var tradeValue = openReport.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(openReport.LongQty ?? openReport.ShortQty);
                        var tradeValueUSD = tradeValue;
                        if (prodDef.Ccy2 != "USD")
                            tradeValueUSD = FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2, "USD", WebCache.Demo.ProdDefs, WebCache.Demo.Quotes);

                        var invest = tradeValueUSD.Value/openReport.Leverage.Value;
                        var pl = closeReport.PL.Value;

                        if (prodDef.AssetClass == "Stock Indices")
                        {
                            indexPL.invest += invest;
                            indexPL.pl += pl;
                        }
                        else if (prodDef.AssetClass == "Currencies")
                        {
                            fxPL.invest += invest;
                            fxPL.pl += pl;
                        }
                        else if (prodDef.AssetClass == "Commodities")
                        {
                            commodityPL.invest += invest;
                            commodityPL.pl += pl;
                        }
                        else if (prodDef.AssetClass == "Single Stocks" && Products.IsUSStocks(prodDef.Symbol))
                        {
                            stockUSPL.invest += invest;
                            stockUSPL.pl += pl;
                        }
                    }
                }
            }

            var result = new List<PLReportDTO> {stockUSPL, indexPL, fxPL, commodityPL};

            return result;
        }

        [HttpGet]
        [Route("plReport")]
        [Route("live/plReport")]
        [BasicAuth]
        public List<PLReportDTO> GetPLReport2()
        {
            var user = GetUser();

            if (!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddDays(-30);

            //closed position
            var positionHistoryReports = IsLiveUrl
                ? db.NewPositionHistory_live.Where(o => o.ClosedAt.HasValue && o.ClosedAt.Value > startTime && o.UserId == UserId).ToList().Select(o=>o as NewPositionHistoryBase).ToList()
                : db.NewPositionHistories.Where(o => o.ClosedAt.HasValue && o.ClosedAt.Value > startTime && o.UserId == UserId).ToList().Select(o => o as NewPositionHistoryBase).ToList();

            //open position
            IList<PositionReport> positionOpenReports;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    positionOpenReports = clientHttp.GetPositionReport(
                        IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                        IsLiveUrl ? null : user.AyondoPassword);
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            var indexPL = new PLReportDTO() { name = "指数" };
            var fxPL = new PLReportDTO() { name = "外汇" };
            var commodityPL = new PLReportDTO() { name = "商品" };
            var stockUSPL = new PLReportDTO() { name = "美股" };

            var cache = WebCache.GetInstance(IsLiveUrl);

            #region closed positions
            foreach (var closedReport in positionHistoryReports)
            {
                var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == closedReport.SecurityId);
                if (prodDef == null) continue;

                var invest = closedReport.InvestUSD.HasValue? closedReport.InvestUSD.Value : 0;
                var pl = closedReport.PL.HasValue? closedReport.PL.Value : 0;

                if (prodDef.AssetClass == "Stock Indices")
                {
                    indexPL.invest += invest;
                    indexPL.pl += pl;
                }
                else if (prodDef.AssetClass == "Currencies")
                {
                    fxPL.invest += invest;
                    fxPL.pl += pl;
                }
                else if (prodDef.AssetClass == "Commodities")
                {
                    commodityPL.invest += invest;
                    commodityPL.pl += pl;
                }
                else if (prodDef.AssetClass == "Single Stocks" && Products.IsUSStocks(prodDef.Symbol))
                {
                    stockUSPL.invest += invest;
                    stockUSPL.pl += pl;
                }
            }
            #endregion

            #region open position
            foreach (var report in positionOpenReports)
            {
                var secId = Convert.ToInt32(report.SecurityID);

                var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == secId);

                if (prodDef == null) continue;

                //var dbSec = dbSecurities.FirstOrDefault(o => o.Id == secId);

                //************************************************************************
                //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                //************************************************************************
                var tradeValue = report.SettlPrice * prodDef.LotSize / prodDef.PLUnits * (report.LongQty ?? report.ShortQty);

                var invest = FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes) / report.Leverage.Value;

                //calculate UPL
                decimal uplUSD = 0;
                var quote = cache.Quotes.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                if (quote != null)
                {
                    var upl = report.LongQty.HasValue ? tradeValue.Value * (quote.Bid / report.SettlPrice - 1) : tradeValue.Value * (1 - quote.Offer / report.SettlPrice);
                    uplUSD = FX.ConvertPlByOutright(upl, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes);
                }
                else
                {
                    CFDGlobal.LogWarning("cannot find quote:" + report.SecurityID + " when calculating UPL for PLReport");
                }

                if (prodDef.AssetClass == "Stock Indices")
                {
                    indexPL.invest += invest;
                    indexPL.pl += uplUSD;
                }
                else if (prodDef.AssetClass == "Currencies")
                {
                    fxPL.invest += invest;
                    fxPL.pl += uplUSD;
                }
                else if (prodDef.AssetClass == "Commodities")
                {
                    commodityPL.invest += invest;
                    commodityPL.pl += uplUSD;
                }
                else if (prodDef.AssetClass == "Single Stocks" && Products.IsUSStocks(prodDef.Symbol))
                {
                    stockUSPL.invest += invest;
                    stockUSPL.pl += uplUSD;
                }
            }
            #endregion
            var result = new List<PLReportDTO> { stockUSPL, indexPL, fxPL, commodityPL };

            return result; 
        }

        [HttpGet]
        [Route("stockAlert")]
        [Route("live/stockAlert")]
        [BasicAuth]
        public List<StockAlertDTO> GetStockAlerts()
        {
            var alerts = IsLiveUrl
                ?db.UserAlert_Live.Where(o => o.UserId == UserId && (o.HighEnabled.Value || o.LowEnabled.Value)).ToList().Select(o => o as UserAlertBase).ToList()
                : db.UserAlerts.Where(o => o.UserId == UserId && (o.HighEnabled.Value || o.LowEnabled.Value)).ToList().Select(o => o as UserAlertBase).ToList();
            return alerts.Select(o => Mapper.Map<StockAlertDTO>(o)).ToList();
        }

        [HttpGet]
        [Route("stockAlert/all")]
        [Route("live/stockAlert/all")]
        [BasicAuth]
        public List<StockAlertDTO> GetAllStockAlerts()
        {
            var alerts = IsLiveUrl
                ?db.UserAlert_Live.Where(o => o.UserId == UserId).ToList().Select(o => o as UserAlertBase).ToList()
                : db.UserAlerts.Where(o => o.UserId == UserId).ToList().Select(o => o as UserAlertBase).ToList();
            return alerts.Select(o => Mapper.Map<StockAlertDTO>(o)).ToList();
        }

        [HttpPut]
        [Route("stockAlert")]
        [Route("live/stockAlert")]
        [BasicAuth]
        public ResultDTO SetStockAlert(StockAlertDTO form)
        {
            var prodDef = WebCache.GetInstance(IsLiveUrl).ProdDefs.FirstOrDefault(o => o.Id == form.SecurityId);

            if (prodDef == null || prodDef.Name.EndsWith(" Outright"))
                return new ResultDTO() {success = false};

            var alert = IsLiveUrl
                ? (UserAlertBase)db.UserAlert_Live.FirstOrDefault(o => o.UserId == UserId && o.SecurityId == form.SecurityId)
                : (UserAlertBase)db.UserAlerts.FirstOrDefault(o => o.UserId == UserId && o.SecurityId == form.SecurityId);

            if (alert == null)
            {
                if (IsLiveUrl)
                    db.UserAlert_Live.Add(new UserAlert_Live()
                    {
                        UserId = UserId,
                        SecurityId = form.SecurityId,
                        HighPrice = form.HighPrice,
                        HighEnabled = form.HighEnabled,
                        LowPrice = form.LowPrice,
                        LowEnabled = form.LowEnabled
                    });
                else
                    db.UserAlerts.Add(new UserAlert()
                    {
                        UserId = UserId,
                        SecurityId = form.SecurityId,
                        HighPrice = form.HighPrice,
                        HighEnabled = form.HighEnabled,
                        LowPrice = form.LowPrice,
                        LowEnabled = form.LowEnabled
                    });
            }
            else
            {
                alert.HighPrice = form.HighPrice;
                alert.HighEnabled = form.HighEnabled;
                alert.LowPrice = form.LowPrice;
                alert.LowEnabled = form.LowEnabled;
            }

            db.SaveChanges();
            return new ResultDTO() {success = true};
        }

        /// <summary>
        /// for login user
        /// </summary>
        /// <param name="form"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("pushtokenauth")]
        [BasicAuth]
        public ResultDTO SetPushTokenAuth(PushDTO form)
        {
            if (string.IsNullOrEmpty(form.deviceToken))
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, ""));

            ResultDTO result = new ResultDTO() {success = true};

            //User user = db.Users.FirstOrDefault( o => o.Id == UserId);

            Device device = db.Devices.FirstOrDefault(o => o.deviceToken == form.deviceToken && o.deviceType == o.deviceType);
            if (device == null) //device token does not exist.
            {
                device = new Device();
                device.deviceToken = form.deviceToken;
                device.deviceType = form.deviceType;

                db.Devices.Add(device);
            }

            device.userId = UserId;
            device.UpdateTime = DateTime.UtcNow;
            db.SaveChanges();

            return result;
        }

        /// <summary>
        /// for guest user
        /// </summary>
        /// <param name="form"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("pushtoken")]
        public ResultDTO SetPushToken(PushDTO form)
        {
            if (string.IsNullOrEmpty(form.deviceToken))
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, ""));

            ResultDTO result = new ResultDTO() { success = true };

            if(string.IsNullOrEmpty(form.deviceToken))
            {
                result.success = false;
                result.message = "Empty DeviceToken";
                return result;
            }

            Device device = db.Devices.FirstOrDefault(o => o.deviceToken == form.deviceToken && o.deviceType == o.deviceType);
            if (device == null) //device token does not exist.
            {
                device = new Device();
                device.deviceToken = form.deviceToken;
                device.deviceType = form.deviceType;

                db.Devices.Add(device);
            }

            device.userId = null;
            device.UpdateTime = DateTime.UtcNow;
            db.SaveChanges();

            return result;
        }

        [HttpGet]
        [Route("message")]
        [Route("live/message")]
        [BasicAuth]
        public List<MessageDTO> GetMessages(int pageNum = 1, int pageSize = 20)
        {
            var messages = IsLiveUrl
                ? db.Message_Live.Where(o => o.UserId == UserId).OrderByDescending(o => o.CreatedAt).Skip((pageNum - 1) * pageSize).Take(pageSize).ToList().Select(o=>o as MessageBase).ToList()
                : db.Messages.Where(o => o.UserId == UserId).OrderByDescending(o => o.CreatedAt).Skip((pageNum - 1) * pageSize).Take(pageSize).ToList().Select(o => o as MessageBase).ToList();

            return messages.Select(o => new MessageDTO()
            {
                id = o.Id,
                userId = o.UserId,
                title = o.Title,
                body = o.Body,
                createdAt = o.CreatedAt,
                isReaded = o.IsReaded
            }).ToList();
        }

        [HttpGet]
        [Route("message/{id}")]
        [Route("live/message/{id}")]
        [BasicAuth]
        public ResultDTO SetMessageReaded(int id)
        {
            ResultDTO result = new ResultDTO() { success = true };

            var message = IsLiveUrl
                ? (MessageBase) db.Message_Live.FirstOrDefault(o => o.UserId == UserId && o.Id == id)
                : db.Messages.FirstOrDefault(o => o.UserId == UserId && o.Id == id);
            if(message != null)
            {
                message.IsReaded = true;
                db.SaveChanges();
            }

            return result;
        }

        [HttpGet]
        [Route("message/unread")]
        [Route("live/message/unread")]
        [BasicAuth]
        public int GetUnreadMessage()
        {
            int unread = IsLiveUrl
                ? db.Message_Live.Count(o => o.UserId == UserId && !o.IsReaded)
                : db.Messages.Count(o => o.UserId == UserId && !o.IsReaded);
            return unread;
        }
        
        [HttpGet]
        [Route("deposit/id")]
        [BasicAuth]
        public string GetDepositTransferId(decimal amount)
        {
            var user = GetUser();

            CheckAndCreateAyondoDemoAccount(user);

            string transferId;
            using (var clientHttp = new AyondoTradeClient())
            {
                transferId = clientHttp.NewDeposit(user.AyondoUsername, user.AyondoPassword, amount);
            }

            return transferId;
        }

        [HttpGet]
        [Route("demo/logout")]
        [Route("live/logout")]
        [BasicAuth]
        public ResultDTO LogoutAyondoDemo()
        {
            var user = GetUser();
            
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                clientHttp.LogOut(user.AyondoUsername);
            }

            return new ResultDTO(true);
        }

        private const string GZT_ACCESS_ID = "shmhxx";
        private const string GZT_ACCESS_KEY = "SHMHAKQHSA";
        private const string GZT_HOST = "http://124.192.161.110:8080/";

        [HttpPost]
        [Route("ocr")]
        [BasicAuth]
        public JObject OcrCheck(OcrFormDTO form)
        {
            var user = GetUser();

            //LIVE account is Created or Pending
            var liveStatus = GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
            if (liveStatus == UserLiveStatus.Active || liveStatus == UserLiveStatus.Pending)
            {
                var errorResult = new ResultDTO(false) {message = __(TransKey.LIVE_ACC_EXISTS)};
                return JObject.Parse(JsonConvert.SerializeObject(errorResult));
            }

            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.GetConfigurationSetting("GuoZhengTongHost") + "ocrCheck");
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Proxy = null;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);

            //
            form.accessId = GZT_ACCESS_ID;
            form.accessKey = GZT_ACCESS_KEY;
            form.timeStamp = DateTimes.GetChinaNow().ToString("yyyy-MM-dd HH:mm:ss");
            form.sign = Randoms.GetRandomAlphanumericString(8);
            
            var s = JsonConvert.SerializeObject(form); //string.Format(json, username, password);
            sw.Write(s);
            sw.Flush();
            sw.Close();

            var dtBegin = DateTime.UtcNow;

            WebResponse webResponse;
            try
            {
                webResponse = httpWebRequest.GetResponse();
            }
            catch (WebException e)
            {
                webResponse = e.Response;
            }

            var responseStream = webResponse.GetResponseStream();
            var sr = new StreamReader(responseStream);

            var str = sr.ReadToEnd();
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("OCR called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri //+ " Response: " + str + "Request:" + s
                                     );

            var jObject = JObject.Parse(str);

            var result = jObject["result"].Value<string>();

            if (result == "0")
            {
                var real_name = jObject["real_name"].Value<string>();
                var id_code = jObject["id_code"].Value<string>();
                var addr = jObject["addr"].Value<string>();
                var gender = jObject["gender"].Value<string>();
                var ethnic = jObject["ethnic"].Value<string>();
                var photo = jObject["photo"].Value<string>();
                var issue_authority = jObject["issue_authority"].Value<string>();
                var valid_period = jObject["valid_period"].Value<string>();
                var transaction_id = jObject["transaction_id"].Value<string>();

                var userInfo = db.UserInfos.FirstOrDefault(o => o.UserId == UserId);
                if (userInfo == null)
                {
                    var newInfo = new UserInfo()
                    {
                        UserId = UserId,

                        IdFrontImg = form.frontImg,
                        IdFrontImgExt = form.frontImgExt,
                        IdBackImg = form.backImg,
                        IdBackImgExt = form.backImgExt,

                        OcrAddr = HttpUtility.UrlDecode(addr),
                        OcrEthnic = HttpUtility.UrlDecode(ethnic),
                        OcrFaceImg = photo,
                        OcrGender = CFDGlobal.GenderChineseToBool(HttpUtility.UrlDecode(gender)),
                        OcrIdCode = id_code,
                        OcrIssueAuth = HttpUtility.UrlDecode(issue_authority),
                        OcrRealName = HttpUtility.UrlDecode(real_name),
                        OcrTransId = transaction_id,
                        OcrValidPeriod = valid_period,
                        OcrCalledAt = DateTime.UtcNow,

                        FaceCheckAt = null,
                        FaceCheckSimilarity = null,
                    };
                    db.UserInfos.Add(newInfo);
                }
                else
                {
                    userInfo.IdFrontImg = form.frontImg;
                    userInfo.IdFrontImgExt = form.frontImgExt;
                    userInfo.IdBackImg = form.backImg;
                    userInfo.IdBackImgExt = form.backImgExt;

                    userInfo.OcrAddr = HttpUtility.UrlDecode(addr);
                    userInfo.OcrEthnic = HttpUtility.UrlDecode(ethnic);
                    userInfo.OcrFaceImg = photo;
                    userInfo.OcrGender = CFDGlobal.GenderChineseToBool(HttpUtility.UrlDecode(gender));
                    userInfo.OcrIdCode = id_code;
                    userInfo.OcrIssueAuth = HttpUtility.UrlDecode(issue_authority);
                    userInfo.OcrRealName = HttpUtility.UrlDecode(real_name);
                    userInfo.OcrTransId = transaction_id;
                    userInfo.OcrValidPeriod = valid_period;
                    userInfo.OcrCalledAt = DateTime.UtcNow;

                    userInfo.FaceCheckAt = null;
                    userInfo.FaceCheckSimilarity = null;
                }

                db.SaveChanges();
            }
            else
            {
                var message = jObject["message"].Value<string>();
                message = HttpUtility.UrlDecode(message);

                CFDGlobal.LogInformation("OCR fail: " + result + " " + message);
            }

            return jObject;
        }

        [HttpPost]
        [Route("faceCheck")]
        [BasicAuth]
        public JObject OcrFaceCheck(OcrFaceCheckFormDTO form)
        {
            if (string.IsNullOrWhiteSpace(form.userId) || string.IsNullOrWhiteSpace(form.firstName) ||
                string.IsNullOrWhiteSpace(form.lastName))
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "required field missing"));
            }

            var user = GetUser();

            //LIVE account is Created or Pending
            var liveStatus = GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
            if (liveStatus == UserLiveStatus.Active || liveStatus == UserLiveStatus.Pending)
            {
                var errorResult = new ResultDTO(false) { message = __(TransKey.LIVE_ACC_EXISTS) };
                return JObject.Parse(JsonConvert.SerializeObject(errorResult));
            }

            var userInfo = db.UserInfos.FirstOrDefault(o => o.UserId == UserId);
            if (userInfo == null || userInfo.OcrTransId == null)
            {
                var errorResult = new ResultDTO(false) { message = __(TransKey.OCR_NO_TRANSACTION_ID) };
                return JObject.Parse(JsonConvert.SerializeObject(errorResult));
            }

            //todo: access control per user

            var httpWebRequest = WebRequest.CreateHttp(CFDGlobal.GetConfigurationSetting("GuoZhengTongHost") + "ocrFaceCheck");
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Proxy = null;
            var requestStream = httpWebRequest.GetRequestStream();
            var sw = new StreamWriter(requestStream);

            //
            form.accessId = GZT_ACCESS_ID;
            form.accessKey = GZT_ACCESS_KEY;
            form.timeStamp = DateTimes.GetChinaNow().ToString("yyyy-MM-dd HH:mm:ss");
            form.sign = Randoms.GetRandomAlphanumericString(8);
            //
            var firstName = form.firstName;
            var lastName = form.lastName;
            form.lastName = null;
            form.firstName = null;
            form.userName = HttpUtility.UrlEncode(lastName + firstName);
            //
            form.transaction_id = userInfo.OcrTransId;

            var s = JsonConvert.SerializeObject(form); //string.Format(json, username, password);
            sw.Write(s);
            sw.Flush();
            sw.Close();

            var dtBegin = DateTime.UtcNow;

            WebResponse webResponse;
            try
            {
                webResponse = httpWebRequest.GetResponse();
            }
            catch (WebException e)
            {
                webResponse = e.Response;
            }

            var responseStream = webResponse.GetResponseStream();
            var sr = new StreamReader(responseStream);

            var str = sr.ReadToEnd();
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("OCR FaceCheck called. Time: " + ts.TotalMilliseconds + "ms Url: " +
                                     httpWebRequest.RequestUri + " Response: " + str + "Request:" + s);

            var jObject = JObject.Parse(str);

            var result = jObject["result"].Value<string>();

            if (result == "0")
            {
                var similarity = jObject["verify_similarity"].Value<decimal>();

                userInfo.FirstName = firstName;
                userInfo.LastName = lastName;
                userInfo.IdCode = form.userId;

                userInfo.FaceCheckAt = DateTime.UtcNow;
                userInfo.FaceCheckSimilarity = similarity;
                db.SaveChanges();
            }
            else
            {
                var message = jObject["message"].Value<string>();
                message = HttpUtility.UrlDecode(message);

                CFDGlobal.LogInformation("OCR FaceCheck fail: " + result + " " + message);
            }

            return jObject;

            //var jObj = new JObject {["result"] = "0"};
            //return jObj;
        }

        //[HttpPut]
        //[Route("ocrResult")]
        //[BasicAuth]
        //public ResultDTO SubmitGZTOcrResult(GZTOcrResultFormDTO form)
        //{
        //    var userInfo = db.UserInfos.FirstOrDefault(o => o.UserId == UserId);
        //    if (userInfo == null)
        //    {
        //        var newInfo = new UserInfo()
        //        {
        //            UserId = UserId,
        //            OcrAddr = form.addr,
        //            OcrEthnic = form.ethnic,
        //            OcrFaceImg = form.photo,
        //            OcrGender = CFDGlobal.GenderChineseToBool(form.gender),
        //            OcrIdCode = form.idCode,
        //            OcrIssueAuth = form.issueAuth,
        //            OcrRealName = form.realName,
        //            OcrTransId = form.transId,
        //            OcrValidPeriod = form.validPeriod,
        //        };
        //        db.UserInfos.Add(newInfo);
        //        db.SaveChanges();
        //    }
        //    else
        //    {
        //        userInfo.OcrAddr = form.addr;
        //        userInfo.OcrEthnic = form.ethnic;
        //        userInfo.OcrFaceImg = form.photo;
        //        userInfo.OcrGender = CFDGlobal.GenderChineseToBool(form.gender);
        //        userInfo.OcrIdCode = form.idCode;
        //        userInfo.OcrIssueAuth = form.issueAuth;
        //        userInfo.OcrRealName = form.realName;
        //        userInfo.OcrTransId = form.transId;
        //        userInfo.OcrValidPeriod = form.validPeriod;
        //        db.SaveChanges();
        //    }

        //    return new ResultDTO() {success = true};
        //}

        [HttpGet]
        [Route("live/checkUsername")]
        [BasicAuth]
        public ResultDTO CheckAyondoLiveUsername(string username)
        {
            var jObject = AMSCheckUsername(username, true);

            var isAvailable = jObject["IsAvailable"].Value<bool>();
            bool isValid = jObject["IsValid"].Value<bool>();

            var result = new ResultDTO() {success = false};

            if (!isValid)
            {
                result.message = __(TransKey.USERNAME_INVALID);
                return result;
            }

            if (!isAvailable)
            {
                result.message = __(TransKey.USERNAME_UNAVAILABLE);
                return result;
            }

            result.success = true;
            return result;
        }
         
        [HttpPost]
        [Route("live/signup")]
        [BasicAuth]
        public ResultDTO CreateLiveAccount(LiveSignupFormDTO form)
        {
            var user = GetUser();

            //phone bound?
            if (user.Phone == null)
            {
                return new ResultDTO(false) {message = __(TransKey.PHONE_NOT_BOUND)};
            }

            //LIVE account is Created or Pending
            var liveStatus = GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
            if (liveStatus == UserLiveStatus.Active || liveStatus == UserLiveStatus.Pending)
            {
                return new ResultDTO(false) { message = __(TransKey.LIVE_ACC_EXISTS)};
            }

            //no OCR result
            var userInfo = db.UserInfos.FirstOrDefault(o => o.UserId == UserId);
            if (userInfo == null || userInfo.OcrTransId == null || userInfo.FaceCheckAt==null)
            {
                return new ResultDTO(false);
            }

            var jObject = AMSLiveAccount(form, user, userInfo);

            if (jObject["Error"] != null)
            {
                var error = jObject["Error"].Value<string>();

                CFDGlobal.LogInformation("LIVE register error:" + error);

                return new ResultDTO
                {
                    message = error,
                    success = false,
                };
            }

            var guid = jObject["Guid"].Value<string>();

            user.AyLiveUsername = form.username;
            user.AyLivePassword = form.password;
            user.AyLiveAccountGuid = guid;
            db.SaveChanges();

            userInfo.Email = form.email;
            //userInfo.RealName = form.realName;
            //userInfo.FirstName = form.firstName;
            //userInfo.LastName = form.lastName;
            userInfo.Gender = form.gender;
            userInfo.Birthday = form.birthday;
            userInfo.Ethnic = form.ethnic;
            //userInfo.IdCode = form.idCode;
            userInfo.Addr = form.addr;
            userInfo.IssueAuth = form.issueAuth;
            userInfo.ValidPeriod = form.validPeriod;
            userInfo.AnnualIncome = form.annualIncome;
            userInfo.NetWorth = form.netWorth;
            userInfo.InvestPct = form.investPct;
            userInfo.EmpStatus = form.empStatus;
            userInfo.InvestFrq = form.investFrq;
            userInfo.HasProExp = form.hasProExp;
            userInfo.HasAyondoExp = form.hasAyondoExp;
            userInfo.HasOtherQualif = form.hasOtherQualif;
            userInfo.ExpOTCDeriv = form.expOTCDeriv;
            userInfo.ExpDeriv = form.expDeriv;
            userInfo.ExpShareBond = form.expShareBond;
            db.SaveChanges();

            return new ResultDTO(true);
        }

        //todo: for test use only
        [HttpGet]
        [Route("live/delete")]
        [BasicAuth]
        public ResultDTO DeleteLiveAccount()
        {
            var user = GetUser();

            user.AyLiveUsername = null;
            user.AyLivePassword = null;
            user.AyLiveAccountGuid = null;
            user.AyLiveAccountStatus = null;
            user.AyLiveAccountId = null;
            db.SaveChanges();

            var delete = db.UserInfos.Where(o => o.UserId == UserId).Delete();

            return new ResultDTO(true);
        }

        [HttpGet]
        [Route("live/resetPwd")]
        [BasicAuth]
        public ResultDTO ResetPassword()
        {
            var user = GetUser();

            var liveStatus = GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
            if (liveStatus != UserLiveStatus.Active)
                return new ResultDTO(false);

            var httpWebRequest = WebRequest.CreateHttp("https://www.tradehub.net/live/ams/proxy/ForgotPassword?UserName=" + user.AyLiveUsername);
            httpWebRequest.Method = "POST";
            httpWebRequest.Proxy = null;

            var dtBegin = DateTime.UtcNow;

            var webResponse = httpWebRequest.GetResponse();
            var responseStream = webResponse.GetResponseStream();
            var sr = new StreamReader(responseStream);

            var str = sr.ReadToEnd();
            var ts = DateTime.UtcNow - dtBegin;
            CFDGlobal.LogInformation("tradehub ForgotPassword called. Time: " + ts.TotalMilliseconds + "ms Url: " + httpWebRequest.RequestUri + " Response: " + str);

            return new ResultDTO(true);
        }

        [HttpPost]
        [Route("live/refaccount")]
        [BasicAuth]
        /// <summary>
        /// bind Bank Card to a Live User
        /// </summary>
        /// <param name="form"></param>
        /// <returns></returns>
        public ResultDTO ReferenceAccount(LiveUserBankCardOriginalFormDTO originalForm)
        {
            var user = GetUser();

            //LIVE account is Created or Pending
            var liveStatus = GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);

            //in which status can user bind a bank card?
            if (liveStatus != UserLiveStatus.Active)
            {
                return new ResultDTO(false);
            }

            LiveUserBankCardFormDTO form = Convert2AyondoForm(originalForm);
            var jObject = AMSBindBankCard(form);
            if (jObject["Error"] != null)
            {
                return new ResultDTO
                {
                    message = jObject["Error"].Value<string>(),
                    success = false,
                };
            }

            user.BankCardNumber = form.AccountNumber;
            if (jObject["ReferenceAccountGuid"] != null)
            {
                user.ReferenceAccountGuid = jObject["ReferenceAccountGuid"].Value<string>();
            }
            db.SaveChanges();

            return new ResultDTO(true);
        }


        /// <summary>
        /// 将用户提交的帮卡信息转换为Ayondo需要的格式
        /// </summary>
        /// <param name="originalForm"></param>
        /// <returns></returns>
        private LiveUserBankCardFormDTO Convert2AyondoForm(LiveUserBankCardOriginalFormDTO originalForm)
        {
            Bitmap template = new Bitmap(HttpContext.Current.Server.MapPath("~/bin/template/BankStatementTemplate.png"));
            Graphics g = Graphics.FromImage(template);
            Font font = new Font("宋体", 32);
            //写入文字
            //银行名称、银行地址、SWIFT CODE、收款人姓名、收款人账号
            SolidBrush sbrush = new SolidBrush(Color.Black);
            g.DrawString(originalForm.NameOfBank, font, sbrush, new PointF(50, 65));
            g.DrawString(originalForm.AddressOfBank, font, sbrush, new PointF(50, 170));
            g.DrawString(originalForm.SwiftCode, font, sbrush, new PointF(50, 275));
            g.DrawString(originalForm.AccountHolder, font, sbrush, new PointF(50, 380));
            g.DrawString(originalForm.AccountNumber, font, sbrush, new PointF(50, 485));

            MemoryStream ms = new MemoryStream();
            template.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            
            //转换为base64格式
            byte[] arr = new byte[ms.Length];
            ms.Position = 0;
            ms.Read(arr, 0, (int)ms.Length);
            ms.Close();
            string imgBase64 = Convert.ToBase64String(arr);

            LiveUserBankCardFormDTO form = new LiveUserBankCardFormDTO() {
                AccountHolder = originalForm.AccountHolder,
                AccountNumber = originalForm.AccountNumber,
                NameOfBank = originalForm.NameOfBank,
                BankStatementContent = imgBase64,
                BankStatementFileName = string.Format("bankstatement_{0}.jpg", originalForm.AccountHolder),
                Guid = originalForm.Guid
            };

            return form;
        }

        private string GetUserLiveAccountRejectReason(string ayLiveAccountStatus)
        {
            switch (ayLiveAccountStatus)
            {
                //rejected
                case "AbortedByExpiry":
                    return __(TransKey.LIVE_ACC_REJ_AbortedByExpiry);
                case "AbortedByPolicy":
                    return __(TransKey.LIVE_ACC_REJ_AbortedByPolicy);
                case "RejectedByDD":
                    return __(TransKey.LIVE_ACC_REJ_RejectedByDD);
                case "RejectedMifid":
                    return __(TransKey.LIVE_ACC_REJ_RejectedMifid);

                default:
                    return null;
            }
        }

        private UserLiveStatus GetUserLiveAccountStatus(string ayLiveUsername, string ayLiveAccountStatus)
        {
            if (string.IsNullOrWhiteSpace(ayLiveUsername))
                return UserLiveStatus.None;

            switch (ayLiveAccountStatus)
            {
                //pending
                case null:
                case "PendingMifid":
                case "PendingClassification":
                case "PendingDocuments":
                case "PendingIdentityConflict":
                case "PendingReview":
                case "PendingUnlock":
                case "PendingUnlockRetry":
                    return UserLiveStatus.Pending;
                    break;

                //rejected
                case "AbortedByExpiry":
                case "AbortedByPolicy":
                case "RejectedByDD":
                case "RejectedMifid":
                    return UserLiveStatus.Rejected;
                    break;

                //created
                case "Active":
                case "Closed":
                case "Locked":
                case "PendingFunding":
                case "PendingLogin":
                case "PendingTrading":
                    return UserLiveStatus.Active;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(ayLiveAccountStatus), ayLiveAccountStatus, null);
            }
        }

        private bool IsLoginBlocked(string phone)
        {
            var oneDayAgo = DateTime.UtcNow.AddDays(-1);
            var phoneList = db.PhoneSignupHistories.Where(item => item.CreateAt >= oneDayAgo && item.Phone == phone).ToList();

            //3 in 1 minute
            //10 in 1 hour
            //20 in 1 day
            if (phoneList.Count(item => (DateTime.UtcNow - item.CreateAt) <= TimeSpan.FromMinutes(1)) >= 3
                || phoneList.Count(item => (DateTime.UtcNow - item.CreateAt) <= TimeSpan.FromHours(1)) >= 10
                || phoneList.Count >= 20)
            {
                return true;
            }

            return false;
        }

    }
}