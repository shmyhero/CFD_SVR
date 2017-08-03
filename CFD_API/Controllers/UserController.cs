using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
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
using CFD_COMMON.IdentityVerify;
using ServiceStack.Common;

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
        private const int NICKNAME_MAX_LENGTH = 8;

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
            var testAccountSetting = db.Miscs.FirstOrDefault(o => o.Key == "TestAccount");

            List<string> testAccounts = new List<string>();
            if(testAccountSetting != null)
            {
                testAccounts = testAccountSetting.Value.Split(';').ToList();
            }

            //auth success
            if (Phone.IsTrustedPhone(form.phone) || verifyCodes.Any() || testAccounts.Contains(form.phone)) //这批号码是专门给Ayondo用的，因为他们收不到验证码。
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

                    //是否已经注册过Partner(合作伙伴)，如果是就用Partner的PromotionCode更新User的PromotionCode
                    var partner = db.Partners.FirstOrDefault(p => p.Phone == user.Phone);
                    if(partner != null)
                    {
                        user.PromotionCode = partner.PromotionCode;
                    }

                    db.SaveChanges();

                    result.success = true;
                    result.isNewUser = true;
                    result.userId = user.Id;
                    result.token = user.Token;

                    #region 第一次用手机号注册，如果该手机号被推荐过，则给该用户30元奖励金
                    var referHistory = db.ReferHistorys.FirstOrDefault(o => o.ApplicantNumber == form.phone);
                    decimal amount = RewardService.REWARD_REFERER;

                    if (referHistory != null)
                    {
                        db.ReferRewards.Add(new ReferReward() { UserID = user.Id, Amount = amount, CreatedAt = DateTime.UtcNow });
                        db.SaveChanges();
                    }
                    #endregion
                }
                else //phone exists
                {
                    user.Token = UserService.NewToken();
                    db.SaveChanges();

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
                if (!string.IsNullOrWhiteSpace(form.headimgurl))
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
                        CFDGlobal.LogWarning("Fail saving wechat picture to azure blob. userid: " + user.Id +
                                             " pic_url: " + form.headimgurl);
                        CFDGlobal.LogExceptionAsWarning(ex);
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
                user.Token = UserService.NewToken();
                db.SaveChanges();

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
        public MeDTO GetMe()
        {
            var user = GetUser();

            var userDto = Mapper.Map<MeDTO>(user);

            var rewardService = new RewardService(db);
            decimal amount = rewardService.DemoRegReward(UserId, user.Phone);
            if(amount > 0)
            {
                userDto.rewardAmount = amount;
            }

            userDto.liveAccStatus = UserLive.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
            if (userDto.liveAccStatus == UserLiveStatus.Rejected)
                userDto.liveAccRejReason = GetUserLiveAccountRejectReason(user.AyLiveAccountStatus);
            userDto.liveUsername = user.AyLiveUsername;
            userDto.liveEmail = db.UserInfos.FirstOrDefault(o => o.UserId == UserId)?.Email;
            userDto.bankCardStatus = user.BankCardStatus;
            userDto.showData = user.ShowData ?? false;
            userDto.firstDayClicked = user.FirstDayClicked.HasValue ? user.FirstDayClicked.Value : false;
            userDto.firstDayRewarded = user.FirstDayRewarded.HasValue ? user.FirstDayRewarded.Value : false;
            userDto.promotionCode = user.PromotionCode;
            return userDto;
        }

        [HttpGet]
        [Route("me/detail")]
        [BasicAuth]
        public MyInfoDTO GetMyUserInfo()
        {
            var userInfo = db.UserInfos.FirstOrDefault(o => o.UserId == UserId);

            if (userInfo == null) return null;

            var userInfoDto = Mapper.Map<MyInfoDTO>(userInfo);

            return userInfoDto;
        }

        [HttpGet]
        [Route("switchTo/{environment}")]
        [BasicAuth]
        public ResultDTO SwitchTo(string environment)
        {
            var user = GetUser();

            var lower = environment.ToLower();

            switch (lower)
            {
                case "live":
                    user.IsOnLive = true;
                    break;
                case "demo":
                    user.IsOnLive = false;
                    break;
                default:
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,""));
            }

            db.SaveChanges();

            return new ResultDTO(true);
        }

        /// <summary>
        /// 修改昵称和推广码
        /// </summary>
        /// <param name="nickname"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        [HttpPost]
        //[RequireHttps]
        [ActionName("nickname")]
        [BasicAuth]
        public ResultDTO SetNickname(string nickname, string code)
        {           
            var user = GetUser();

            if (!string.IsNullOrEmpty(nickname))
            {
                nickname = nickname.Trim();
                if (nickname.Length > NICKNAME_MAX_LENGTH)
                    return new ResultDTO() { success = false, message = __(TransKey.NICKNAME_TOO_LONG) };

                if (db.Users.Any(o => o.Id != UserId && o.Nickname == nickname))
                    return new ResultDTO
                    {
                        success = false,
                        message = __(TransKey.NICKNAME_EXISTS)
                    };
                
                user.Nickname = nickname;
            }

            if(!string.IsNullOrEmpty(code))
            {
                code = code.Trim();
                if (!string.IsNullOrEmpty(user.PromotionCode))
                {
                    return new ResultDTO
                    {
                        success = false,
                        message = "推广码已存在"
                    };
                }

                user.PromotionCode = code;
            }
            
            db.SaveChanges();

            return new ResultDTO {success = true};
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        [HttpPost]
        //[RequireHttps]
        [ActionName("updateFirstLoginInfo")]
        [BasicAuth]
        public ResultDTO SetFirstLoginInfo(FirstLoginInfoDTO form)
        {
            var user = GetUser();

            if (!string.IsNullOrEmpty(form.nickName))
            {
                form.nickName = form.nickName.Trim();
                if (form.nickName.Length > NICKNAME_MAX_LENGTH)
                    return new ResultDTO() { success = false, message = __(TransKey.NICKNAME_TOO_LONG) };

                if (db.Users.Any(o => o.Id != UserId && o.Nickname == form.nickName))
                    return new ResultDTO
                    {
                        success = false,
                        message = __(TransKey.NICKNAME_EXISTS)
                    };

                user.Nickname = form.nickName;
            }

            if (!string.IsNullOrEmpty(form.promotionCode))
            {
                form.promotionCode = form.promotionCode.Trim();
                if (!string.IsNullOrEmpty(user.PromotionCode))
                {
                    return new ResultDTO
                    {
                        success = false,
                        message = "推荐码已存在"
                    };
                }

                //如果该手机号已经注册过合伙人，并且合伙人的上级推荐码和App的不一致，就返回异常
                var partner = db.Partners.FirstOrDefault(p => p.Phone == user.Phone);
                if(partner != null && !string.IsNullOrEmpty(partner.ParentCode))
                {
                    //根据PartnerCode找到上级合伙人
                    var parentPartner = db.Partners.FirstOrDefault(p=>p.PartnerCode == partner.ParentCode);
                    if(parentPartner.PromotionCode != form.promotionCode)
                    return new ResultDTO
                    {
                        success = false,
                        message = "与注册的合伙人上级推荐码不一致"
                    };
                }

                user.PromotionCode = form.promotionCode;
            }

            db.SaveChanges();

            return new ResultDTO { success = true };
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
        [Route("live/alert/{setting}")]
        [BasicAuth]
        public ResultDTO SetSystemAlert(bool setting)
        {
            var user = GetUser();

            if (IsLiveUrl)
                user.AutoCloseAlert_Live = setting;
            else
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

                var rewardService = new RewardService(db);
                rewardService.DemoBindPhoneReward(UserId);

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

            #region 获取可提现余额以及备注
            decimal maxRefundable = 0;
            decimal miniRefundable = 5;
            string refundComment = "出金将收取0元手续费";
            if(IsLiveUrl)
            {
                Misc refundSetting = db.Miscs.OrderByDescending(o => o.Id).FirstOrDefault(o => o.Key == "RefundFee");
                Misc refundCommentSetting = db.Miscs.OrderByDescending(o => o.Id).FirstOrDefault(o => o.Key == "RefundFeeComment");
                decimal available = balance - marginUsed;

                if (refundSetting != null)
                {
                    //最小手续费
                    decimal minimum = JObject.Parse(refundSetting.Value)["min"].Value<decimal>();
                    //按百分比计算的手续费
                    decimal percentage = JObject.Parse(refundSetting.Value)["rate"].Value<decimal>() * available;
                    //手续费按大的算
                    maxRefundable = GetAvailableWithdraw(balance, totalUPL, balance - marginUsed);  //minimum > percentage ? (available - minimum) : (available - percentage);
                    //最小可出金金额
                    miniRefundable = JObject.Parse(refundSetting.Value)["miniRefundable"].Value<decimal>();
                }

                if (refundCommentSetting != null)
                {
                    refundComment = refundCommentSetting.Value;
                }

            }
            #endregion

            return new BalanceDTO()
            {
                id = user.Id,
                balance = balance,
                total = balance + totalUPL,
                available = balance - marginUsed,
                refundable = maxRefundable > 0 ? ((int)(maxRefundable * 100))/100.00M : 0, //截取两位小数，但不四舍五入。 不能用Math.Round
                minRefundable = miniRefundable,
                comment = refundComment
            };
        }

        /// <summary>
        /// Open Positions/Pending Orders or not? – If no, then the client can withdraw 100%
        /// Select the Lowest value between Cash Balance or Margin Available
        /// Select the lowest value between 80% and Liquidity
        /// example:
        /// Cash Balance: 92408
        /// Open P/L: -1310
        /// Account Value: 91097
        /// Margin Available: 6725
        /// Margin Used: 85683
        /// Liquidity: 98.23%  计算方法: ((92408 * 80%) - 1310 ) / (92408 * 80%)
        /// 
        /// available withdraw is: 5378.9
        /// </summary>
        /// <param name="availableMargin"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("live/withdraw/available")]
        [BasicAuth]
        public decimal GetAvailableWithdraw(decimal cashBalance, decimal pl, decimal availableMargin)
        {
            var user = GetUser();
            IList<PositionReport> openPositions = null;
            using (var wcfClient = new AyondoTradeClient(true))
            {
                try
                {
                    openPositions = wcfClient.GetPositionReport(user.AyLiveUsername, null, false);
                }
                catch (FaultException<OAuthLoginRequiredFault>)//when oauth is required
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            if(openPositions.Count == 0) //没有持仓
            {
                return cashBalance;
            }
            else //有持仓
            {
                decimal liquidity = ((cashBalance * 0.8M) + pl) / (cashBalance * 0.8M);

                return availableMargin * (liquidity < 0.8M ? liquidity : 0.8M);
            }
            
        }

        [HttpGet]
        [Route("plReport2")]
        [Route("live/plReport2")]
        [BasicAuth]
        public List<PLReportDTO> GetPLReport()
        {
            var user = GetUser();

            if(!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            var endTime = DateTime.UtcNow;
            var startTime = DateTimes.GetHistoryQueryStartTime(endTime);

            IList<PositionReport> positionOpenReports;
            IList<PositionReport> positionHistoryReports;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    positionOpenReports = clientHttp.GetPositionReport(IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername, IsLiveUrl ? null : user.AyondoPassword);
                    positionHistoryReports = clientHttp.GetPositionHistoryReport(IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername, IsLiveUrl ? null : user.AyondoPassword, startTime, endTime);
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
            var stockUSPL = new PLReportDTO() {name = "股票"};

            var cache = WebCache.GetInstance(IsLiveUrl);
            //open positions
            foreach (var report in positionOpenReports)
            {
                var secId = Convert.ToInt32(report.SecurityID);

                var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == secId);

                if (prodDef == null) continue;

                //var dbSec = dbSecurities.FirstOrDefault(o => o.Id == secId);

                //************************************************************************
                //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                //************************************************************************
                var tradeValue = report.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(report.LongQty ?? report.ShortQty);

                var invest = FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes)/report.Leverage.Value;

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

                        var prodDef = cache.ProdDefs.FirstOrDefault(o => o.Id == secId);

                        if (prodDef == null) continue;

                        //var dbSec = dbSecurities.FirstOrDefault(o => o.Id == secId);

                        //************************************************************************
                        //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                        //************************************************************************
                        var tradeValue = openReport.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(openReport.LongQty ?? openReport.ShortQty);
                        var tradeValueUSD = tradeValue;
                        if (prodDef.Ccy2 != "USD")
                            tradeValueUSD = FX.ConvertByOutrightMidPrice(tradeValue.Value, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes);

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
            var stockUSPL = new PLReportDTO() { name = "股票" };

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
                else if (prodDef.AssetClass == "Single Stocks" && (Products.IsUSStocks(prodDef.Symbol) || Products.IsHKStocks(prodDef.Symbol)))
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
                else if (prodDef.AssetClass == "Single Stocks" && (Products.IsUSStocks(prodDef.Symbol) || Products.IsHKStocks(prodDef.Symbol)))
                {
                    stockUSPL.invest += invest;
                    stockUSPL.pl += uplUSD;
                }
            }
            #endregion
            var result = new List<PLReportDTO> { stockUSPL, indexPL, fxPL, commodityPL };

            return result; 
        }

        /// <summary>
        /// 达人榜看别人的收益
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{userID}/live/plReport")]
        [BasicAuth]
        public List<PLReportDTO> GetOthersPLReport(int userID)
        {
            var indexPL = new PLReportDTO() { name = "指数" };
            var fxPL = new PLReportDTO() { name = "外汇" };
            var commodityPL = new PLReportDTO() { name = "商品" };
            var stockUSPL = new PLReportDTO() { name = "股票" };

            if (userID != UserId) //not myself
            {
                var user = db.Users.FirstOrDefault(o => o.Id == userID);
                if (user == null || !(user.ShowData ?? false))
                    return new List<PLReportDTO> {stockUSPL, indexPL, fxPL, commodityPL};
            }

            var cache = WebCache.GetInstance(IsLiveUrl);
            var indicesIDs = cache.ProdDefs.Where(p => p.AssetClass == "Stock Indices").Select(p => p.Id);
            var currencyIDs = cache.ProdDefs.Where(p => p.AssetClass == "Currencies").Select(p => p.Id);
            var commodityIDs = cache.ProdDefs.Where(p => p.AssetClass == "Commodities").Select(p => p.Id);
            var stockIDs = cache.ProdDefs.Where(p => p.AssetClass == "Single Stocks" && Products.IsUSStocks(p.Symbol)).Select(p => p.Id);

            //这里要用Contact，不要用Union。因为Union会排除重复项。
            #region 平仓
            var plReports = (from m in ( //TODO: improve this T-SQL, too long
                                (from n in db.NewPositionHistory_live
                                 where n.SecurityId.HasValue && indicesIDs.Contains(n.SecurityId.Value)
                                 && n.UserId == userID && n.PL.HasValue && n.InvestUSD.HasValue && n.ClosedAt.HasValue
                                 select new { AssetType = "指数", PL = n.PL, Invest = n.InvestUSD }).Concat(
                                from n in db.NewPositionHistory_live
                                where n.SecurityId.HasValue && currencyIDs.Contains(n.SecurityId.Value)
                                && n.UserId == userID && n.PL.HasValue && n.InvestUSD.HasValue && n.ClosedAt.HasValue
                                select new { AssetType = "外汇", PL = n.PL, Invest = n.InvestUSD }).Concat(
                                from n in db.NewPositionHistory_live
                                where n.SecurityId.HasValue && commodityIDs.Contains(n.SecurityId.Value)
                                && n.UserId == userID && n.PL.HasValue && n.InvestUSD.HasValue && n.ClosedAt.HasValue
                                select new { AssetType = "商品", PL = n.PL, Invest = n.InvestUSD }).Concat(
                                from n in db.NewPositionHistory_live
                                where n.SecurityId.HasValue && stockIDs.Contains(n.SecurityId.Value)
                                && n.UserId == userID && n.PL.HasValue && n.InvestUSD.HasValue && n.ClosedAt.HasValue
                                select new { AssetType = "股票", PL = n.PL, Invest = n.InvestUSD })
                            )
                             group m by m.AssetType into g
                             select new PLReportDTO
                             {
                                 name = g.Key,
                                 pl = g.Sum(m => m.PL).Value,
                                 invest = g.Sum(m => m.Invest).Value
                             }).ToList();

            //如果没有数据，需要补充
            if(plReports.FirstOrDefault(r => r.name == "指数") == null)
            {
                plReports.Add(new PLReportDTO() { name = "指数" });
            }
            if (plReports.FirstOrDefault(r => r.name == "外汇") == null)
            {
                plReports.Add(new PLReportDTO() { name = "外汇" });
            }
            if (plReports.FirstOrDefault(r => r.name == "商品") == null)
            {
                plReports.Add(new PLReportDTO() { name = "商品" });
            }
            if (plReports.FirstOrDefault(r => r.name == "股票") == null)
            {
                plReports.Add(new PLReportDTO() { name = "股票" });
            }

            #endregion

            //TODO: why not query db only once to get both open and closed histories
            #region 持仓
            var positions = db.NewPositionHistory_live.Where(p => p.UserId == userID && !p.ClosedAt.HasValue).OrderByDescending(p => p.Id).ToList();

            positions.ForEach(p => {
                var prodDef = cache.ProdDefs.FirstOrDefault(pd => pd.Id == p.SecurityId);
                var quote = cache.Quotes.FirstOrDefault(o => o.Id == p.SecurityId.Value);
                if (prodDef != null)
                {
                    #region 计算PL
                    var tradeValue = p.InvestUSD * p.Leverage;
                    if (quote != null)
                    {
                        decimal upl = p.LongQty.HasValue ? tradeValue.Value * (quote.Bid / p.SettlePrice.Value - 1) : tradeValue.Value * (1 - quote.Offer / p.SettlePrice.Value);
                        var uplUSD = FX.ConvertPlByOutright(upl, prodDef.Ccy2, "USD", cache.ProdDefs, cache.Quotes);
                        PLReportDTO report = null;
                        if (indicesIDs.Contains(prodDef.Id)) //指数
                        {
                           report = plReports.FirstOrDefault(r => r.name == "指数");
                        }
                        else if (currencyIDs.Contains(prodDef.Id)) //外汇
                        {
                            report = plReports.FirstOrDefault(r => r.name == "外汇");
                        }
                        else if (commodityIDs.Contains(prodDef.Id)) //商品
                        {
                            report = plReports.FirstOrDefault(r => r.name == "商品");
                        }
                        else if (stockIDs.Contains(prodDef.Id)) //股票
                        {
                            report = plReports.FirstOrDefault(r => r.name == "股票");
                        }

                        if (report != null)
                        {
                            report.pl += uplUSD;
                            report.invest += p.InvestUSD.HasValue? p.InvestUSD.Value : 0;
                        }
                    }
                    #endregion
                }
                
            });

            #endregion
          
            return plReports;
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
        [Route("live/deposit/id")]
        [BasicAuth]
        public NewDepositDTO GetDepositTransferId(decimal amount)
        {
            var user = GetUser();

            if(!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            string transferId;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    transferId = clientHttp.NewDeposit(
                        IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                        IsLiveUrl ? null : user.AyondoPassword,
                        amount);
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            var result = new NewDepositDTO {transferId = transferId};

            if(IsLiveUrl)
            {
                db.DepositHistories.Add(new DepositHistory() { UserID = user.Id, TransferID = Convert.ToInt64(transferId), CreatedAt = DateTime.Now, ClaimAmount = amount });
                db.SaveChanges();

                var userInfo = db.UserInfos.FirstOrDefault(o => o.UserId == UserId);
                if (userInfo != null)
                {
                    result.firstName = userInfo.FirstName;
                    result.lastName = userInfo.LastName;
                    result.email = userInfo.Email;
                    result.addr = userInfo.Addr;
                }
            }

            return result;
        }

        ///// <summary>
        ///// 根据transferId获取用户的姓名、邮箱
        ///// TODO: to be deleted @1.1.11
        ///// </summary>
        ///// <param name="transferId"></param>
        ///// <returns></returns>
        //[HttpGet]
        //[Route("live/deposit/userinfo")]
        //public string GetUserInfoByTransferId(string transferId)
        //{
        //    string format = "{{'first_name':'{0}', 'last_name':'{1}', 'email':'{2}', 'addr':'{3}'}}";
        //    var query = from u in db.UserInfos
        //                join d in db.DepositHistories on u.UserId equals d.UserID
        //                into x
        //                from y in x.DefaultIfEmpty()
        //                where y.TransferID == Convert.ToInt64(transferId)
        //                select new { u.FirstName, u.LastName, u.Email, u.Addr };
        //    var userInfo = query.FirstOrDefault();
        //    if(userInfo != null)
        //    {
        //        return string.Format(format, userInfo.FirstName, userInfo.LastName, userInfo.Email, userInfo.Addr);
        //    }

        //    return string.Format(format, string.Empty, string.Empty, string.Empty, string.Empty);
        //}

        [HttpGet]
        [Route("demo/logout")]
        [Route("live/logout")]
        [BasicAuth]
        public ResultDTO LogoutAyondo()
        {
            var user = GetUser();
            
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                clientHttp.LogOut(IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername);
            }

            return new ResultDTO(true);
        }

        private const string GZT_ACCESS_ID = "shmhxx";
        private const string GZT_ACCESS_KEY = "SHMHAKQHSA";
        //private const string GZT_HOST = "http://124.192.161.110:8080/";

        [HttpPost]
        [Route("ocr")]
        [BasicAuth]
        public JObject OcrCheck(OcrFormDTO form)
        {
            var user = GetUser();

            //LIVE account is Created or Pending
            var liveStatus = UserLive.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
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
                var userImage = db.UserImages.FirstOrDefault(o => o.UserId == UserId);
                if (userInfo == null)
                {
                    var newInfo = new UserInfo()
                    {
                        UserId = UserId,
                        OcrAddr = HttpUtility.UrlDecode(addr),
                        OcrEthnic = HttpUtility.UrlDecode(ethnic),
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
                    userInfo.OcrAddr = HttpUtility.UrlDecode(addr);
                    userInfo.OcrEthnic = HttpUtility.UrlDecode(ethnic);
                    
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

                if(userImage == null)
                {
                    UserImage newUserImage = new UserImage()
                    {
                        UserId = UserId,
                        IdFrontImg = form.frontImg,
                        IdFrontImgExt = form.frontImgExt,
                        IdBackImg = form.backImg,
                        IdBackImgExt = form.backImgExt,
                        OcrFaceImg = photo,
                    };
                    db.UserImages.Add(newUserImage);
                }
                else
                {
                    userImage.IdFrontImg = form.frontImg;
                    userImage.IdFrontImgExt = form.frontImgExt;
                    userImage.IdBackImg = form.backImg;
                    userImage.IdBackImgExt = form.backImgExt;
                    userImage.OcrFaceImg = photo;
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
            var liveStatus = UserLive.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
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

            var firstName = form.firstName;
            var lastName = form.lastName;

            //todo: access control per user
            JObject response = null;
            IProfileVerify pv = null;
            string host = CFDGlobal.GetConfigurationSetting("ProfileVerify");

            form.accessId = GZT_ACCESS_ID;
            form.accessKey = GZT_ACCESS_KEY;
            form.timeStamp = DateTimes.GetChinaNow().ToString("yyyy-MM-dd HH:mm:ss");
            form.sign = Randoms.GetRandomAlphanumericString(8);
            form.transaction_id = userInfo.OcrTransId;
            form.lastName = null;
            form.firstName = null;

            switch (host)
            {
                case "GuoZhengTongHost":
                    form.userName = HttpUtility.UrlEncode(lastName + firstName);
                    pv = new GuozhengtongVerification();
                    break;
                case "MinshHost":
                    form.userName = lastName + firstName;
                    pv = new MinshVerification();
                    break;
                default: pv = new GuozhengtongVerification(); break;
            }

            response = pv.Verify(form);

            //如果不是国政通接口，并且查询失败
            if (host != "GuoZhengTongHost" && response["result"].Value<int>() != 0)
            {
                pv = new GuozhengtongVerification();
                response = pv.Verify(form);
            }
            
            
            var result = response["result"].Value<string>();

            if (result == "0")
            {
                var similarity = response["verify_similarity"].Value<decimal>();

                userInfo.FirstName = firstName;
                userInfo.LastName = lastName;
                userInfo.IdCode = form.userId;

                userInfo.FaceCheckAt = DateTime.UtcNow;
                userInfo.FaceCheckSimilarity = similarity;
                db.SaveChanges();
            }
            else
            {
                var message = response["message"].Value<string>();
                message = HttpUtility.UrlDecode(message);

                CFDGlobal.LogInformation("OCR FaceCheck fail: " + response + " " + message);
            }

            return response;

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

            var isAvailable = jObject["data"]["isAvailable"].Value<bool>();
            bool isValid = jObject["data"]["isValid"].Value<bool>();

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

            //根据PromotionCode找到对应的Partner的GUID
            form.salesRepGuid = GetPartnerGUID(user.PromotionCode);

            //phone bound?
            if (user.Phone == null)
            {
                return new ResultDTO(false) {message = __(TransKey.PHONE_NOT_BOUND)};
            }

            //LIVE account is Created or Pending
            var liveStatus = UserLive.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
            if (liveStatus == UserLiveStatus.Active || liveStatus == UserLiveStatus.Pending)
            {
                return new ResultDTO(false) { message = __(TransKey.LIVE_ACC_EXISTS)};
            }

            //no OCR result
            var userInfo = db.UserInfos
                .Include(o=>o.UserImage)
                .FirstOrDefault(o => o.UserId == UserId);
            if (userInfo == null || userInfo.OcrTransId == null || userInfo.FaceCheckAt == null)
            {
                return new ResultDTO(false);
            }

            //Create Application
            if (user.AyLiveAccountGuid == null //new live user
                || UserLive.GetUserLiveAccountStatus(user.AyLiveAccountStatus) == UserLiveStatus.Rejected //has been rejected, new application guid is required
                )
            {
                var initResult = AMSLiveAccountInitiate();
                var accountGuid = initResult["data"]["accountGuid"].Value<string>();

                user.AyLiveAccountGuid = accountGuid;
                db.SaveChanges();
            }

            //Mifid Test
            var mifidResult = DoMifidTest(user.AyLiveAccountGuid, form);

            if (mifidResult is JArray)
            {
                CFDGlobal.LogWarning("LIVE mifid test error:" + mifidResult);

                return new ResultDTO
                {
                    error = mifidResult,
                    success = false,
                };
            }

            var mifidGuid = mifidResult["data"]["mifidGuid"].Value<string>();
            var rulesetId = mifidResult["data"]["rulesetId"].Value<string>();
            var appropriatenessScore = mifidResult["data"]["appropriatenessScore"].Value<decimal>();
            var appropriatenessResolution = mifidResult["data"]["appropriatenessResolution"].Value<string>();

            CFDGlobal.LogInformation("MiFID result: account " + user.AyLiveAccountGuid + " mifid " + mifidGuid + " ruleset " +
                                     rulesetId + " score " + appropriatenessScore + " resolution " +
                                     appropriatenessResolution);

            //When Mifid Test Failed
            if (appropriatenessResolution == "Failed" && form.confirmMifidOverride == null)
            {
                return new ResultDTO() {success = false, message = "MifidTestFailed"};
            }

            #region 上传身份证照和地址证明
            //把身份证的正面照和反面照合并成一张照片
            var frontBitmap = GetBaimapFromBase64(userInfo.UserImage.IdFrontImg);
            var backBitmap = GetBaimapFromBase64(userInfo.UserImage.IdBackImg);
            string strCombinedBase64 = CombineImage(frontBitmap, backBitmap);

            var idUploadResult = AMSLiveAccountDocument(user.AyLiveAccountGuid, strCombinedBase64, "image/jpeg", "Identity");
            CFDGlobal.LogInformation("id upload result:" + idUploadResult.Item2);
            if (!idUploadResult.Item1)
            {
                CFDGlobal.LogWarning("LIVE register id upload error:" + idUploadResult.Item2);

                return new ResultDTO
                {
                    error = idUploadResult.Item2,
                    success = false,
                };
            }

            ////由于地址证明变成了两张图片，因此这里先把两张图片合并成一张(如果有两张的话)，再做上传
            //string strPOA = string.Empty;
            //if(string.IsNullOrEmpty(userInfo.UserImage.ProofOfAddressII))
            //{
            //    strPOA = userInfo.UserImage.ProofOfAddress;
            //}
            //else
            //{
            //    var poa1 = GetBaimapFromBase64(userInfo.UserImage.ProofOfAddress);
            //    var poa2 = GetBaimapFromBase64(userInfo.UserImage.ProofOfAddressII);
            //    strPOA = CombineImage(poa1, poa2);
            //}

            //var poaUploadResult = AMSLiveAccountDocument(user.AyLiveAccountGuid, strPOA, "image/jpeg", "Address");
            //CFDGlobal.LogInformation("poa upload result:" + poaUploadResult.Item2);
            //if (!poaUploadResult.Item1)
            //{
            //    CFDGlobal.LogWarning("LIVE register poa upload error:" + poaUploadResult.Item2);

            //    return new ResultDTO
            //    {
            //        error = poaUploadResult.Item2,
            //        success = false,
            //    };
            //}
            #endregion

            var json = AMSLiveAccountComplete(user.AyLiveAccountGuid, mifidGuid, form, user, userInfo);

            if (json is JArray)
            {
                //var error = jObject["error"].Value<string>();

                CFDGlobal.LogWarning("LIVE register error:" + json);

                return new ResultDTO
                {
                    error = json,
                    success = false,
                };
            }

            var guid = json["data"]["accountGuid"].Value<string>();

            user.AyLiveUsername = form.username;
            user.AyLivePassword = Encryption.GetCypherText_3DES_CBC_MD5ofPW_IVPrefixed(form.password, Encryption.SHARED_SECRET_CFD);
            user.AyLiveAccountGuid = guid;
            user.AyLiveApplyAt = DateTime.UtcNow;
            db.SaveChanges();

            userInfo.Email = form.email;
            //userInfo.RealName = form.realName;
            //userInfo.FirstName = form.firstName;
            //userInfo.LastName = form.lastName;
            userInfo.Gender = form.gender;

            //
            //userInfo.Birthday = form.birthday;
            userInfo.Birthday = UserLive.GetBirthdayFromIdCode(userInfo.IdCode, ".");

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

            userInfo.SourceOfFunds = form.sourceOfFunds;
            userInfo.EmployerName = form.employerName;
            userInfo.EmployerSector = form.employerSector;
            userInfo.MonthlyIncome = form.monthlyIncome;
            userInfo.Investments = form.investments;
            userInfo.HasTraining = form.hasTraining;
            userInfo.HasDemoAcc = form.hasDemoAcc;
            userInfo.OtherQualif = form.otherQualif;
            userInfo.HasTradedHighLev = form.hasTradedHighLev;
            userInfo.HasTradedMidLev = form.hasTradedMidLev;
            userInfo.HasTradedNoLev = form.hasTradedNoLev;
            userInfo.HighLevBalance = form.highLevBalance;
            userInfo.HighLevFrq = form.highLevFrq;
            userInfo.HighLevRisk = form.highLevRisk;
            userInfo.MidLevBalance = form.midLevBalance;
            userInfo.MidLevFrq = form.midLevFrq;
            userInfo.MidLevRisk = form.midLevRisk;
            userInfo.NoLevBalance = form.noLevBalance;
            userInfo.NoLevFrq = form.noLevFrq;
            userInfo.NoLevRisk = form.noLevRisk;

            userInfo.MifidGuid = mifidGuid;
            userInfo.MifidRulesetId = rulesetId;
            userInfo.AppropriatenessScore = appropriatenessScore;
            userInfo.AppropriatenessResolution = appropriatenessResolution;

            db.SaveChanges();

            return new ResultDTO(true);
        }

        private Bitmap GetBaimapFromBase64(string base64)
        {
            byte[] b = Convert.FromBase64String(base64);
            MemoryStream ms = new MemoryStream(b);
            Bitmap bitmap = new Bitmap(ms);
            return bitmap;
        }

        private string CombineImage(Bitmap bm1, Bitmap bm2)
        {
            int width = bm1.Width > bm2.Width ? bm1.Width : bm2.Width;
            int height = bm1.Height + bm2.Height;
            Image imgFinal = new Bitmap(width, height);
            Graphics g = Graphics.FromImage(imgFinal);
            g.DrawImage(bm1, 0, 0, bm1.Width, bm1.Height);      
            g.DrawImage(bm2, 0, bm1.Height, bm2.Width, bm2.Height);

            MemoryStream ms = new MemoryStream();
            imgFinal.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            byte[] arr = new byte[ms.Length];
            ms.Position = 0;
            ms.Read(arr, 0, (int)ms.Length);
            ms.Close();
            string strbaser64 = Convert.ToBase64String(arr);

            GC.Collect();

            return strbaser64;
        }

        //todo: for test use only
        [HttpGet]
        [Route("live/delete")]
        [BasicAuth]
        [IPAuth]
        public ResultDTO DeleteLiveAccount()
        {
            var user = GetUser();

            user.AyLiveUsername = null;
            user.AyLivePassword = null;
            user.AyLiveAccountGuid = null;
            user.AyLiveAccountStatus = null;
            user.AyLiveAccountId = null;
            //user.BankCardNumber = null;
            //user.BankCardRejectReason = null;
            //user.BankCardStatus = null;
            //user.BankName = null;
            //user.Branch = null;
            //user.Province = null;
            //user.City = null;
            //user.ReferenceAccountGuid = null;
            db.SaveChanges();

            db.UserInfos.Where(o => o.UserId == UserId).Delete();

            //db.DepositHistories.Where(o => o.UserID == UserId).Delete();
            //db.NewPositionHistory_live.Where(o => o.UserId == UserId).Delete();
            //db.WithdrawalHistories.Where(o => o.UserId == UserId).Delete();

            return new ResultDTO(true);
        }

        [HttpGet]
        [Route("live/resetPwd")]
        [BasicAuth]
        public ResultDTO ResetPassword()
        {
            var user = GetUser();

            var liveStatus = UserLive.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
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

            var userInfo = db.UserInfos.FirstOrDefault(o=>o.UserId == user.Id);
            if(userInfo == null)
            {
                CFDGlobal.LogInformation("ReferenceAccount: User has no personal info");
                return new ResultDTO(false) { message = "该用户没有对应的身份信息" };
            }

            //LIVE account is Created or Pending
            var liveStatus = UserLive.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);

            //in which status can user bind a bank card?
            if (liveStatus != UserLiveStatus.Active)
            {
                return new ResultDTO(false);
            }

            LiveUserBankCardFormDTO form = Convert2AyondoForm(originalForm);
            form.idCardNumber = userInfo.IdCode;

            var bank = db.Banks.FirstOrDefault(b => b.CName == originalForm.NameOfBank);
            if(bank == null)
            {
                CFDGlobal.LogInformation("ReferenceAccount: bank name is wrong:" + originalForm.NameOfBank);
                return new ResultDTO(false) { message = "银行名称不存在" };
            }
            form.bic = bank.BIC;

            //首次绑定银行卡，用POST。 如果银行卡已存在，更新用PUT
            //string method = string.IsNullOrEmpty(user.BankCardNumber) ? "POST" : "PUT";
            //按照Ayondo的文档，如果更新的话用PUT，但测试下来PUT会报错，还是用POST
            string method = "POST";

            var jObject = AMSBindBankCard(form, user.AyLiveAccountGuid, method);
            //接口异常时返回示例如下:
            //{"errorCode":"UNEXPECTED_ERROR","message":"XXXXXXXX","accountGuid":"22db2731-8ef5-4a73-beb6-690885b13cd2"}
            //正常返回示例如下:
            //{"data":{referenceAccountGuid:"22db2731-8ef5-4a73-beb6-690885b13cd2"}}
            if (jObject["errorCode"] != null)
            {
                CFDGlobal.LogInformation(string.Format("ReferenceAccount failed for '{0}', message:'{1}'", user.AyLiveAccountGuid, jObject["message"].Value<string>()));
                return new ResultDTO
                {
                    message = jObject["message"].Value<string>(),
                    success = false,
                };
            }

            user.BankCardStatus = BankCardUpdateStatus.Submitted;
            user.BankCardNumber = form.accountNumber;
            user.BankName = form.nameOfBank;
            user.Branch = form.branch;
            user.Province = form.province;
            user.City = form.city;
            if (jObject["data"]["referenceAccountGuid"] != null)
            {
                user.ReferenceAccountGuid = jObject["data"]["referenceAccountGuid"].Value<string>();
            }

            db.SaveChanges();

            return new ResultDTO(true);
        }

        /// <summary>
        /// 解绑银行卡，把相关信息置空
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("live/withdraw/unbind")]
        [BasicAuth]
        public ResultDTO UnBind()
        {
            var user = GetUser();
            user.BankCardNumber = string.Empty;
            user.BankCardStatus = BankCardUpdateStatus.Deleted;
            user.BankCardRejectReason = string.Empty;
            user.BankName = string.Empty;
            user.Branch = string.Empty;
            user.Province = string.Empty;
            user.City = string.Empty;
            user.ReferenceAccountGuid = string.Empty;
            db.SaveChanges();

            return new ResultDTO(true);
        }

        [HttpPost]
        [Route("live/withdraw")]
        [BasicAuth]
        public string WithDraw(LiveUserRefundDTO form)
        {
            var user = GetUser();

            if(user.BankCardStatus!= BankCardUpdateStatus.Approved)
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.NO_APPROVED_BANK_CARD)));

            CFDGlobal.LogInformation(string.Format("Withdraw request, {0}, {1}, {2}", user.AyLiveUsername, user.Id, form.Amount));

            string transferId;
            using (var clientHttp = new AyondoTradeClient(true))
            {
                try
                {
                    transferId = clientHttp.NewWithdraw(user.AyLiveUsername, user.AyLivePassword, form.Amount);
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
                catch (FaultException<MDSTransferErrorFault> e)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, Translator.AyondoMDSTransferErrorMessageTranslate(e.Detail.Text)));
                }
            }

            CFDGlobal.LogInformation(string.Format("Withdraw request transferID, {0}", transferId));

            //db.TransferHistorys.Add(new TransferHistory() { TransferType="Withdraw",Amount=form.Amount, UserID=this.UserId, BankCard=user.BankCardNumber, CreatedAt = DateTime.Now });
            db.WithdrawalHistories.Add(new WithdrawalHistory()
            {
                RequestAmount = form.Amount,
                UserId = UserId,
                TransferId = Convert.ToInt64(transferId),
                CreateAt = DateTime.UtcNow,
                BankCardNumber = user.BankCardNumber,
            });
            db.SaveChanges();

            return transferId;
        }

        [HttpGet]
        [Route("live/withdraw/info")]
        [BasicAuth]
        public LiveUserInfoDTO GetUserInfo()
        {
            var userInfo = (from x in db.Users 
                           join y in db.UserInfos on x.Id equals y.UserId
                            join z in db.Banks on x.BankName equals z.CName
                            into t1
                            from t2 in t1.DefaultIfEmpty()
                            where x.Id == UserId
                           select new
                           {
                               y.FirstName, y.LastName, y.IdCode, x.BankCardNumber, x.BankName, x.BankCardStatus, x.BankCardRejectReason, Icon = t2 == null? "" : t2.Icon, x.Branch,x.Province,x.City,
                           y.Addr
                           }).FirstOrDefault();

            if(userInfo == null)
            {
                return null;
            }

            var lastWithdrawRecord = db.WithdrawalHistories.OrderByDescending(o=>o.CreateAt).FirstOrDefault(o => (o.UserId == UserId && o.BankCardNumber == userInfo.BankCardNumber));

            LiveUserInfoDTO dto = new LiveUserInfoDTO()
            {
                firstName = userInfo.FirstName,
                lastName = userInfo.LastName,
                identityID = userInfo.IdCode,
                bankCardNumber = userInfo.BankCardNumber,
                bankIcon = userInfo.Icon,
                bankName = userInfo.BankName,
                bankCardStatus = GetBankCardStatus(userInfo.BankCardStatus),
                bankCardRejectReason = userInfo.BankCardRejectReason,
                branch = userInfo.Branch,
                province = userInfo.Province,
                city = userInfo.City,
                lastWithdraw = lastWithdrawRecord == null? decimal.Zero : lastWithdrawRecord.RequestAmount,
                lastWithdrawAt = lastWithdrawRecord == null ? null : lastWithdrawRecord.CreateAt,
                pendingDays = "1",//认为1个工作日内会做好银行卡的审核
                addr = userInfo.Addr,
            };

            return dto;
        }

        /// <summary>
        /// 对传给前端的银行卡状态做转换。Submitted -> PendingReview、Deleted -> String.Empty
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private string GetBankCardStatus(string status)
        {
            switch(status)
            {
                case BankCardUpdateStatus.Deleted: status = string.Empty; break;
                case BankCardUpdateStatus.Submitted: status = BankCardUpdateStatus.PendingReview; break;
                default: break;
            }

            return status;
        }

        /// <summary>
        /// 出入金历史纪录
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("live/transfers")]
        [BasicAuth]
        public List<TransferDTO> GetTransferHistory()
        {
            var user = GetUser();

            /*
            EFT ： 出金
            WeCollect - CUP ： Wecollect入金
            Bank Wire ： 运营赠金
            Transaction Fee ： 入金手续费 （可能也包含出金）
            Trade Result ： 交易
            Financing ： 隔夜费
            Dividend ： 分红
             */

            //只显示以下类型的数据
            List<string> limitedTypes = new List<string>();
            limitedTypes.AddRange(new string[] { "EFT", "WeCollect - CUP", "Bank Wire", "Transaction Fee", });

            if (!user.AyLiveAccountId.HasValue)
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.Live_Acc_Not_Exist)));

            List<TransferDTO> results = new List<TransferDTO>();

            var transfers = db.AyondoTransferHistory_Live.Where(t => t.TradingAccountId == user.AyLiveAccountId && limitedTypes.Contains(t.TransferType)).OrderByDescending(o=>o.ApprovalTime).ToList();
            transfers.ForEach(t =>
            {
                var result = getTransDescriptionColor(t.TransferType);
                results.Add(new TransferDTO()
                {
                    amount = t.Amount.HasValue ? t.Amount.Value : 0,
                    date = t.ApprovalTime.HasValue ? t.ApprovalTime.Value.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss") : "--",
                    transferType = result.Item1,
                    color = result.Item2
                });
            }

            );

            return results;
        }

        private Tuple<string,string> getTransDescriptionColor(string transType)
        {
            Tuple<string, string> result = new Tuple<string, string>(string.Empty,string.Empty);
            switch(transType.ToLower().Trim())
            {
                case "eft": result = new Tuple<string, string>("出金", "#000000"); break;
                case "wecollect - cup": result = new Tuple<string, string>("入金", "#1c8d13"); break;
                case "bank wire": result = new Tuple<string, string>("交易金入金", "#1c8d13"); break;
                case "transaction fee": result = new Tuple<string, string>("手续费", "#000000"); break;
                case "trade result": result = new Tuple<string, string>("交易", "#000000"); break;
                case "financing": result = new Tuple<string, string>("隔夜费", "#000000"); break;
                case "dividend": result = new Tuple<string, string>("分红", "#000000"); break;
            }

            return result;
        }

        [HttpPut]
        [Route("follow/{followingId}")]
        [BasicAuth]
        public ResultDTO SetFollowing(int followingId)
        {
            if (UserId == followingId)
                return new ResultDTO(false);

            var any = db.UserFollows.Any(o => o.UserId == UserId && o.FollowingId == followingId);

            if (!any)
            {
                db.UserFollows.Add(new UserFollow()
                {
                    UserId = UserId,
                    FollowingId = followingId,
                    FollowAt = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            return new ResultDTO(true);
        }

        [HttpDelete]
        [Route("follow/{followingId}")]
        [BasicAuth]
        public ResultDTO DeleteFollowing(int followingId)
        {
            if (UserId == followingId)
                return new ResultDTO(false);

            db.UserFollows.Where(o => o.UserId == UserId && o.FollowingId == followingId).Delete();

            return new ResultDTO(true);
        }

        [HttpGet]
        [Route("following")]
        [BasicAuth]
        public List<UserDTO> GetFollowingUsers()
        {
            var user = GetUser();
            var result =
                db.UserFollows.Include(o => o.Following)
                    .Where(o => o.UserId == UserId)
                    .OrderByDescending(o => o.FollowAt)
                    .Select(o => new UserDTO()
                    {
                        id = o.Following.Id,
                        nickname = o.Following.Nickname,
                        picUrl = o.Following.PicUrl,
                        showData = o.Following.ShowData ?? false,
                        rank = o.Following.LiveRank ?? 0,
                    }).ToList();

            if (result.Count > 0)
            {
                var userIds = result.Select(o => o.id).ToList();

                var twoWeeksAgo = DateTimes.GetChinaToday().AddDays(-13);
                var twoWeeksAgoUtc = twoWeeksAgo.AddHours(-8);

                var datas =
                    db.NewPositionHistory_live.Where(
                        o => userIds.Contains(o.UserId.Value) && o.ClosedAt != null && o.ClosedAt >= twoWeeksAgoUtc)
                        .GroupBy(o => o.UserId).Select(o => new UserDTO()
                        {
                            id = o.Key.Value,

                            posCount = o.Count(),
                            winRate = (decimal) o.Count(p => p.PL > 0)/o.Count(),
                            roi = o.Sum(p => p.PL.Value)/o.Sum(p => p.InvestUSD.Value),
                        }).ToList();

                foreach (var userDto in result)
                {
                    if (!userDto.showData)
                        userDto.rank = 0;

                    var data = datas.FirstOrDefault(o => o.id == userDto.id);

                    if (data == null)//this guy has no data
                    {
                        userDto.roi = 0;
                        userDto.posCount = 0;
                        userDto.winRate = 0;
                    }
                    else
                    {
                        userDto.roi = data.roi;
                        if (userDto.showData)
                        {
                            userDto.posCount = data.posCount;
                            userDto.winRate = data.winRate;
                        }
                    }
                }

                //result = result.OrderByDescending(o => o.roi).ToList();
            }

            return result;
        }

        [HttpGet]
        [Route("live/timestamp")]
        [BasicAuth]
        public TimeStampDTO GetTimeStamp()
        {
            long timeStamp = DateTime.Now.ToUnixTime();
            int nonce = new Random(DateTime.Now.Millisecond).Next(0, 100000);

            db.TimeStampNonces.Add(new TimeStampNonce() { TimeStamp = timeStamp, Nonce = nonce, UserID = this.UserId, CreatedAt = DateTime.UtcNow, Expiration = SqlDateTime.MaxValue.Value });
            db.SaveChanges();
            return new TimeStampDTO() { timeStamp = timeStamp, nonce = nonce };
        }

        [HttpPost]
        [Route("live/poa")]
        [BasicAuth]
        public ResultDTO UploadProofOfAddress(ProofOfAddressDTO form)
        {
            var userImage = db.UserImages
                    .FirstOrDefault(o => o.UserId == UserId);
            if (userImage == null)
            {
                CFDGlobal.LogInformation("upload proof of address: User has no personal info");
                return new ResultDTO(false) { message = "该用户没有对应的身份信息" };
            }

            userImage.ProofOfAddress = form.imageBase64;
            userImage.ProofOfAddressII = form.imageBase64II;
            db.SaveChanges();

            return new ResultDTO(true);
        }

        [HttpPost]
        [Route("live/profit")]
        [BasicAuth]
        public ResultDTO ProfitListSetting(ProfitListSettingDTO form)
        {
            var user = GetUser();
            user.ShowData = form.showData;

            db.SaveChanges();

            return new ResultDTO(true);
        }

        [HttpGet]
        [Route("live/report")]
        [IPAuth]
        public List<UserReportDTO> GetUserReport()
        {
            var users = db.Users.Include(o=>o.UserInfo).Where(o => o.AyLiveUsername != null).ToList();

            //var userIds = users.Select(o => o.Id).ToList();
            //db.UserInfos.Include(o=>)

            var chinaToday = DateTimes.GetChinaToday();

            return users.Select(o=>
            {
                var year = o.UserInfo.IdCode.Substring(6, 4).ToInt();
                var month = o.UserInfo.IdCode.Substring(10, 2).ToInt();
                var day = o.UserInfo.IdCode.Substring(12, 2).ToInt();
                var birth=new DateTime(year,month,day,0,0,0,DateTimeKind.Local);

                var userAge = chinaToday.Year - year;
                if (birth.AddYears(userAge) > chinaToday)
                    userAge--;

                var genderInt = o.UserInfo.IdCode.Substring(16, 1).ToInt();

                return new UserReportDTO()
                {
                    id = o.Id,
                    age = userAge,
                    gender = genderInt%2,
                    accountId = o.AyLiveAccountId==null?null:o.AyLiveAccountId.ToString(),
                    status = o.AyLiveAccountStatus,
                };
            }).ToList();
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
            //g.DrawString(originalForm.AddressOfBank, font, sbrush, new PointF(50, 170));
            //g.DrawString(originalForm.SwiftCode, font, sbrush, new PointF(50, 275));
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

            LiveUserBankCardFormDTO form = new LiveUserBankCardFormDTO()
            {
                accountHolder = originalForm.AccountHolder,
                accountNumber = originalForm.AccountNumber,
                nameOfBank = originalForm.NameOfBank,
                bankStatementContent = imgBase64,
                bankStatementFilename = string.Format("bankstatement_{0}.jpg", originalForm.AccountHolder),
                iban = string.Empty,//该字段Ayondo文档说是optional的，但测下来必须有值。所以给个空值
                info = string.Empty,//该字段Ayondo文档说是optional的，但测下来必须有值。所以给个空值
                branch = originalForm.Branch,
                province = originalForm.Province,
                city = originalForm.City
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
                case "RejectedDD":
                    return __(TransKey.LIVE_ACC_REJ_RejectedByDD);
                case "RejectedMifid":
                    return __(TransKey.LIVE_ACC_REJ_RejectedMifid);
                case "RejectedDuplicate":
                    return __(TransKey.LIVE_ACC_REJ_RejectedDuplicate);

                default:
                    return null;
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