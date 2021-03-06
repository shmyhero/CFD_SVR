﻿using System;
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
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using CFD_COMMON.IdentityVerify;
using ServiceStack.Common;
using ServiceStack.Common.Extensions;
using Pingpp;
using System.Security.Cryptography.X509Certificates;

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

                    #region
                    Random ran = new Random();
                    string url = CFDGlobal.USER_PIC_BLOB_CONTAINER_URL + DEFAULT_USER_PIC_FILE_NAMES[ran.Next(DEFAULT_USER_PIC_FILE_NAMES.Length)];
                    user.PicUrl = url;
                    #endregion

                    db.SaveChanges();

                    var rewardService = new RewardService(db);
                    rewardService.DemoRegReward(user.Id, user.Phone);

                    result.success = true;
                    result.isNewUser = true;
                    result.userId = user.Id;
                    result.token = user.Token;

                    #region 第一次用手机号注册，如果该手机号被推荐过，则给该用户30元奖励金
                    //需求2.1.6 被推荐人不用入金,只要注册，也可以给推荐人30元奖励
                    var referHistory = db.ReferHistorys.FirstOrDefault(o => o.ApplicantNumber == form.phone);
                    decimal amount = RewardService.REWARD_REFERER;

                    if (referHistory != null && referHistory.IsRewarded != true)
                    {
                        referHistory.IsRewarded = true;
                        referHistory.RewardedAt = DateTime.Now;
                        db.ReferRewards.Add(new ReferReward() { UserID = user.Id, Amount = amount, CreatedAt = DateTime.UtcNow });
                        db.ReferRewards.Add(new ReferReward() { UserID = referHistory.RefereeID, Amount = RewardService.REWARD_REFEREE, CreatedAt = DateTime.UtcNow });
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
                result.message = Resources.Resource.INVALID_VERIFY_CODE;// __(TransKey.INVALID_VERIFY_CODE);
            }

            return result;
        }

        [HttpPost]
        [ActionName("signupByChannel")]
        public SignupResultDTO signupByChannel(SignupByChannelDTO form)
        {
            var signupByPhoneResult = SignupByPhone(form);
            if(signupByPhoneResult.success && (signupByPhoneResult.isNewUser == true)) //手机注册成功后，并且是新用户，记录下渠道、活动
            {
                var user = db.Users.FirstOrDefault(u => u.Id == signupByPhoneResult.userId);
                if(user!=null)
                {
                    user.ChannelID = form.channelID;
                    user.ActivityID = form.activityID;
                    db.SaveChanges();
                }
            }

            return signupByPhoneResult;
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

            userDto.liveAccStatus = CFDUsers.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
            if (userDto.liveAccStatus == UserLiveStatus.Rejected)
                userDto.liveAccRejReason = GetUserLiveAccountRejectReason(user.AyLiveAccountStatus);
            userDto.liveUsername = user.AyLiveUsername;
            userDto.liveEmail = db.UserInfos.FirstOrDefault(o => o.UserId == UserId)?.Email;
            userDto.bankCardStatus = user.BankCardStatus;
            userDto.showData = user.ShowData ?? CFDUsers.DEFAULT_SHOW_DATA;
            userDto.showOpenCloseData = user.ShowOpenCloseData ?? CFDUsers.DEFAULT_SHOW_DATA;
            userDto.firstDayClicked = user.FirstDayClicked.HasValue ? user.FirstDayClicked.Value : false;
            userDto.firstDayRewarded = user.FirstDayRewarded.HasValue ? user.FirstDayRewarded.Value : false;
            userDto.promotionCode = user.PromotionCode;
            userDto.rank = user.LiveRank?? 0;
            bool canMobileDeposit = true;
            Misc mobileDeposit = db.Miscs.OrderByDescending(o => o.Id).FirstOrDefault(o => o.Key == "MobileDeposit");
            if (mobileDeposit != null)
            {
                if (!string.IsNullOrEmpty(mobileDeposit.Value) && mobileDeposit.Value.ToLower() == "true")
                {
                    canMobileDeposit = true;
                }
                else
                {
                    canMobileDeposit = false;
                }
            }
            userDto.mobileDeposit = canMobileDeposit;

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
        public ResultDTO SetNickname(string nickname, string code = "")
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
            if(!string.IsNullOrEmpty(user.PicUrl) && !isSystemPic(user.PicUrl.Split('/').Last())) //delete existing blob before upload
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

        /// <summary>
        /// 是否为系统默认的头像
        /// </summary>
        /// <param name="picName"></param>
        /// <returns></returns>
        private bool isSystemPic(string picName)
        {
            //List<string> systemPics = new List<string>();
            //systemPics.AddRange(new string[] {"1.jpg", "2.jpg", "3.jpg", "4.jpg", "5.jpg", "6.jpg", "7.jpg", "8.jpg", "9.jpg", "10.jpg", "12.jpg", });
            return DEFAULT_USER_PIC_FILE_NAMES.Contains(picName);
        }

        private static readonly string[] DEFAULT_USER_PIC_FILE_NAMES = {"1.jpg", "2.jpg", "3.jpg", "4.jpg", "5.jpg", "6.jpg", "7.jpg", "8.jpg", "9.jpg", "10.jpg", "12.jpg",};

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

            BalanceReport balance;
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

            //update balanceID and actorID
            if (IsLiveUrl)
            {
                if (!string.IsNullOrEmpty(balance.BalanceId) && user.AyLiveBalanceId.ToString() != balance.BalanceId)
                    user.AyLiveBalanceId = Convert.ToInt64(balance.BalanceId);
                if (!string.IsNullOrEmpty(balance.ActorId) && user.AyLiveActorId.ToString() != balance.ActorId)
                    user.AyLiveActorId = Convert.ToInt64(balance.ActorId);
                db.SaveChanges();
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
                decimal available = balance.Value - marginUsed;

                if (refundSetting != null)
                {
                    //最小手续费
                    decimal minimum = JObject.Parse(refundSetting.Value)["min"].Value<decimal>();
                    //按百分比计算的手续费
                    decimal percentage = JObject.Parse(refundSetting.Value)["rate"].Value<decimal>() * available;
                    //手续费按大的算
                    maxRefundable = GetAvailableWithdraw(balance.Value, totalUPL, balance.Value - marginUsed);  //minimum > percentage ? (available - minimum) : (available - percentage);
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
                balance = balance.Value,
                total = balance.Value + totalUPL,
                available = balance.Value - marginUsed,
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

            //var endTime = DateTime.UtcNow;
            //var startTime = endTime.AddDays(-30);

            //closed position
            //var positionHistoryReports = IsLiveUrl
            //    ? db.NewPositionHistory_live.Where(o => o.ClosedAt.HasValue && o.ClosedAt.Value > startTime && o.UserId == UserId).ToList().Select(o=>o as NewPositionHistoryBase).ToList()
            //    : db.NewPositionHistories.Where(o => o.ClosedAt.HasValue && o.ClosedAt.Value > startTime && o.UserId == UserId).ToList().Select(o => o as NewPositionHistoryBase).ToList();
            var positionHistoryReports = IsLiveUrl
              ? db.NewPositionHistory_live.Where(o => o.ClosedAt.HasValue && o.UserId == UserId).ToList().Select(o => o as NewPositionHistoryBase).ToList()
              : db.NewPositionHistories.Where(o => o.ClosedAt.HasValue && o.UserId == UserId).ToList().Select(o => o as NewPositionHistoryBase).ToList();


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
                if (user == null || !(user.ShowData ?? CFDUsers.DEFAULT_SHOW_DATA))
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

        /// <summary>
        /// 达人榜首页盈亏分布
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{userID}/live/plSpread")]
        [BasicAuth]
        public List<PLSpreadDTO> GetOthersPLSpread(int userID)
        {
            List<PLSpreadDTO> spreads = new List<PLSpreadDTO>();

            var user = db.Users.FirstOrDefault(o => o.Id == userID);
            if(!(user.ShowData ??CFDUsers.DEFAULT_SHOW_DATA))
            {
                return spreads;
            }
            //找出平仓笔数最多的三个产品
            var prodIDs = db.NewPositionHistory_live.Where(n => n.UserId == userID && n.InvestUSD.HasValue && n.PL.HasValue && n.ClosedAt.HasValue)
                .GroupBy(n => n.SecurityId).Select(s=>new {
                    prodID = s.Key,
                    count = s.Count(),
                }).OrderByDescending(s=>s.count).Take(3).ToList();

            prodIDs.ForEach(p => {
                //平均收益
                var pl = db.NewPositionHistory_live.Where(n => n.UserId == userID && n.InvestUSD.HasValue && n.PL.HasValue && n.ClosedAt.HasValue && n.SecurityId == p.prodID).Sum(n => n.PL);
                var investment = db.NewPositionHistory_live.Where(n => n.UserId == userID && n.InvestUSD.HasValue && n.PL.HasValue && n.ClosedAt.HasValue && n.SecurityId == p.prodID).Sum(n => n.InvestUSD);
                //胜率
                decimal wins = db.NewPositionHistory_live.Where(n => n.UserId == userID && n.InvestUSD.HasValue && n.PL.HasValue && n.ClosedAt.HasValue && n.SecurityId == p.prodID && n.PL > 0).Count();
                // var totals = db.NewPositionHistory_live.Where(n => n.UserId == UserId && n.InvestUSD.HasValue && n.PL.HasValue && n.ClosedAt.HasValue && n.SecurityId == p.prodID).Count();

                var prodDef = WebCache.Live.ProdDefs.FirstOrDefault(prod => prod.Id == p.prodID);
                spreads.Add(new PLSpreadDTO() {
                    name = Translator.GetProductNameByThreadCulture(prodDef.Name),
                    symbol = prodDef.Symbol,
                    count = p.count,
                     pl = pl.Value / investment.Value,
                     rate = wins / p.count
                });

            });

            return spreads;
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
            throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,__(TransKey.PAYMENT_METHOD_DISABLED)));

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
                        amount, TransferType.CUP_DEPOSIT);
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

        [HttpGet]
        [Route("deposit/adyen")]
        [Route("live/deposit/adyen")]
        [BasicAuth]
        public NewAdyenDepositDTO NewAdyenDeposit(decimal amount)
        {
            var user = GetUser();

            if (!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            string transferId;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    transferId = clientHttp.NewDeposit(
                        IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                        IsLiveUrl ? null : user.AyondoPassword,
                        amount, TransferType.ADYEN_CC_DEPOSIT);
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            if (IsLiveUrl)
            {
                db.DepositHistories.Add(new DepositHistory() { UserID = user.Id, TransferID = Convert.ToInt64(transferId), Type = "adyen", CreatedAt = DateTime.Now, ClaimAmount = amount });
                db.SaveChanges();

                //var userInfo = db.UserInfos.FirstOrDefault(o => o.UserId == UserId);
                //if (userInfo != null)
                //{
                //    result.firstName = userInfo.FirstName;
                //    result.lastName = userInfo.LastName;
                //    result.email = userInfo.Email;
                //    result.addr = userInfo.Addr;
                //}
            }

            var result = new NewAdyenDepositBaseDTO()
            {
                merchantAccount = "AyoMarLimTHCN",
                paymentAmount = (amount*100).ToString("F0"),
                sessionValidity = DateTime.UtcNow.AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                shopperLocale = "en_GB",
                currencyCode = "USD",
                skinCode = "UtmJpnab",
                merchantReference = transferId,
                brandCode = "moneybookers",
                //brandCode = "visa",
                //issuerId = "1121",
                shipBeforeDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                //merchantReturnData = "",
                //shopperEmail = "",

                //countryCode = "US",
            };

            var keyValues =
                result.GetType()
                    .GetProperties()
                    .OrderBy(o => o.Name)
                    .Select(o => new KeyValuePair<string, string>(o.Name, (string)o.GetValue(result, null)))
                    .Where(o=>o.Value!=null)
                    .ToList();

            StringBuilder sb = new StringBuilder();
            //keys
            for (int i = 0; i < keyValues.Count; i++)
            {
                sb.Append(keyValues[i].Key);
                sb.Append(':');
            }
            //values
            for (int i = 0; i < keyValues.Count; i++)
            {
                var value = keyValues[i].Value;
                if (value == null) value = "";

                sb.Append(value.Replace("\\", "\\\\").Replace(":", "\\:"));
                if (i != keyValues.Count - 1)
                    sb.Append(':');
            }
            var dataString = sb.ToString();
            //var dataString =
            //    "currencyCode:merchantAccount:merchantReference:paymentAmount:sessionValidity:shipBeforeDate:shopperLocale:skinCode:USD:AyoMarLimTHCN:SKINTEST-1503472799708:199:2017-08-23T07\\:50\\:11Z:2017-08-29:en_GB:UtmJpnab";
            var bytes = Encoding.UTF8.GetBytes(dataString);

            var HMAC_KEY = IsLiveUrl? "43AEC933ADD2761E30C956CB4254011276F487BC04A64CFD000B38B76C2D2516" : "2BC504F6B19E96F429F4FF70E420EC89F5EBC3E6B0D93CEAA8E445ADC60C247D";
            byte[] binaryHmacKey = Converts.HexStringToBytes(HMAC_KEY);

            // Create an HMAC SHA-256 key from the raw key bytes

            // Get an HMAC SHA-256 Mac instance and initialize with the signing key

            // calculate the hmac on the binary representation of the signing string

            var hmacsha256 = new HMACSHA256(binaryHmacKey);
            var hash = hmacsha256.ComputeHash(bytes);

            var base64String = Convert.ToBase64String(hash);

            return new NewAdyenDepositDTO
            {
                merchantAccount = result.merchantAccount,
                paymentAmount = result.paymentAmount,
                sessionValidity = result.sessionValidity,
                shopperLocale = result.shopperLocale,
                currencyCode = result.currencyCode,
                skinCode = result.skinCode,
                merchantReference = result.merchantReference,
                brandCode = result.brandCode,
                //issuerId = result.issuerId,
                shipBeforeDate = result.shipBeforeDate,

                merchantSig = base64String,
                signingString = dataString,

                //countryCode = result.countryCode,
            };
        }

        [HttpGet]
        [Route("deposit/focal")]
        [Route("live/deposit/focal")]
        [BasicAuth]
        public NewFocalDepositDTO NewFocalDeposit(decimal amount)
        {
            var user = GetUser();

            if (!IsLiveUrl) CheckAndCreateAyondoDemoAccount(user);

            string transferId;
            using (var clientHttp = new AyondoTradeClient(IsLiveUrl))
            {
                try
                {
                    transferId = clientHttp.NewDeposit(
                        IsLiveUrl ? user.AyLiveUsername : user.AyondoUsername,
                        IsLiveUrl ? null : user.AyondoPassword,
                        amount, TransferType.CUP_DEPOSIT);
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            if (IsLiveUrl)
            {
                db.DepositHistories.Add(new DepositHistory() { UserID = user.Id, TransferID = Convert.ToInt64(transferId), Type = "focal", CreatedAt = DateTime.Now, ClaimAmount = amount });
                db.SaveChanges();

                //var userInfo = db.UserInfos.FirstOrDefault(o => o.UserId == UserId);
                //if (userInfo != null)
                //{
                //    result.firstName = userInfo.FirstName;
                //    result.lastName = userInfo.LastName;
                //    result.email = userInfo.Email;
                //    result.addr = userInfo.Addr;
                //}
            }

            var site = IsLiveUrl ? "8d4e7dda-87a6-11e7-b212-0242ac110002" : "adfc6dd4-87a7-11e7-be8a-0242ac110002";

            var cache = WebCache.GetInstance(IsLiveUrl);
            var fx = cache.ProdDefs.FirstOrDefault(o => o.Name == "USD/CNY Outright");
            var quote = cache.Quotes.FirstOrDefault(o => o.Id == fx.Id);
            var baseDTO = new NewFocalDepositBaseDTO() 
            {
                Amount = (amount *quote.Offer).ToString("F2"),
                Currency = "CNY",
                PaymentType = "cup",
                Site = site,
                TransRef = transferId,
            };

            var dataString = baseDTO.Site + baseDTO.Amount + baseDTO.Currency + baseDTO.PaymentType + baseDTO.TransRef;
            //var dataString =
            //    "currencyCode:merchantAccount:merchantReference:paymentAmount:sessionValidity:shipBeforeDate:shopperLocale:skinCode:USD:AyoMarLimTHCN:SKINTEST-1503472799708:199:2017-08-23T07\\:50\\:11Z:2017-08-29:en_GB:UtmJpnab";
            var bytes = Encoding.UTF8.GetBytes(dataString);

            var HMAC_KEY = IsLiveUrl ? "ayondo" : "ayondo";

            var hmacsha256 = new HMACSHA256(Encoding.UTF8.GetBytes(HMAC_KEY));
            var hash = hmacsha256.ComputeHash(bytes);

            var hexString = Converts.BytesToHexString(hash);

            var result = new NewFocalDepositDTO()
            {
                Amount=baseDTO.Amount,
                Currency = baseDTO.Currency,
                Site = baseDTO.Site,
                PaymentType = baseDTO.PaymentType,
                TransRef = baseDTO.TransRef,

                Merchant = "9cf7890c-87a6-11e7-bc4d-0242ac110002",
                AttemptMode = "1",
                TestTrans = IsLiveUrl?"0":"1",
                lang="zh_CN",
                Product = "lots of stuff",
                Signature = hexString,
                
            };

            var userInfo = db.UserInfos.FirstOrDefault(o => o.UserId == user.Id);
            if (userInfo != null)
            {
                result.customer_email = userInfo.Email;
                result.customer_first_name = userInfo.FirstName;
                result.customer_last_name = userInfo.LastName;
                result.customer_country = "CN";
                result.customer_address1 = userInfo.Addr;

                var addr = userInfo.Addr;
                var idx = addr.IndexOf('市');
                if (idx < 0) idx = addr.IndexOf('县');
                if (idx < 0) idx = addr.Length - 1;
                result.customer_city = addr.Substring(0, idx + 1);

                result.customer_id_type = "1";
                result.customer_id_number = userInfo.IdCode;
            }

            return result;
        }

        [HttpGet]
        [Route("live/deposit/pingpp")]
        [BasicAuth]
        public Pingpp.Models.Charge NewPingppDeposit(decimal amount, string channel, string payment, decimal rewardAmount)
        {
            string[] acceptedChannels = new string[] {"isv_qr"};
            string[] acceptedPayments = new string[] {"alipay", "wx"};
            if (acceptedChannels.ToList().IndexOf(channel) == -1)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "错误的渠道"));
            }
            if (acceptedPayments.ToList().IndexOf(payment) == -1)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "错误的支付方式"));
            }

            var today = DateTime.UtcNow.Date;
            int orderRawNo = db.PingOrders.Where(p => p.CreatedAt > today).Count() + 1;
            string orderNo = DateTime.Now.ToString("yyyyMMdd") + orderRawNo.ToString().PadLeft(8, '0');

            var pOrder = new PingOrder()
            {
                UserId = UserId,
                //FxRate = fxRate,
                //FxRateAt = exchangeRateProd.Time,
                CreatedAt = DateTime.UtcNow,
                AmountCNY = amount,
                Channel = channel,
                OrderNumber = orderNo
            };
            db.PingOrders.Add(pOrder);

            CFDGlobal.LogInformation("NewPingppDeposit - rewardAmount:" + rewardAmount + " UserID:" + this.UserId);

            if (rewardAmount > 0)
            {
                db.OrderRewardUsages.Add(new OrderRewardUsage()
                {
                    CreatedAt = DateTime.UtcNow,
                    OrderNumber = orderNo,
                    RewardAmountUSD = rewardAmount,
                    RewardFxRate = CommonController.rewardFxRate,
                    UserId = this.UserId
                });
            }

            db.SaveChanges();

            //Pingpp.Pingpp.SetApiKey("sk_test_XXHirPKGqnfDrX5e1GL40CyP");
            Pingpp.Pingpp.SetApiKey("sk_live_jfbb9O5OinP8y9G4i15CmjnD");
            string appId = "app_90OKKC9ePOC0La9K";

            var extra = new Dictionary<string, object>();

            switch (channel)
            {
                case "isv_qr":
                    extra.Add("pay_channel", payment);
                    //extra.Add("result_url", "https://cn.tradehero.mobi/test_form/finish.html");
                    break;
            }
            //terminal_id是必填项，随便写一个
            extra.Add("terminal_id", "T0000001");

            var param = new Dictionary<string, object>
            {
                {"order_no", orderNo},
                {"amount", amount*100}, //Ping++以分为单位，所以要乘100
                {"channel", channel},
                {"currency", "cny"},
                {"subject", "支付"},
                {"body", "payment"},
                {"client_ip", "127.0.0.1"},
                {"app", new Dictionary<string, string> {{"id", appId}}},
                {"extra", extra}
            };

            try
            {
                Pingpp.Pingpp.SetPrivateKey(@"-----BEGIN RSA PRIVATE KEY-----
MIIEogIBAAKCAQEA5lywlsjUzbQa5ENFzxJx46BPRnMdWIKGy88YM7bD8vfrIeG+
5Fh21KenDJ8KgUVwkmohlSgDEIQSFnVWjSGWZvGCyTSnFDqaFchSep523UCKxI2e
cLnuFPU5swVw2/4wk5yLdUFq7f9BMZF0r/NBUyy57dUh4+3FE8UxUrLAIsVotYhz
aw69zeeJbT+ncmjD3YLnLKHrxmdUqycE/zPV8YIYv/Wx8M2MLWV/M/0rkWMFO7id
Jk41wcS5OwpP9TmD8gfbo3EJszu7SECNhhk9pU2mrMOtIKxnnr0gU8kHdCZ1gb1m
BKwigqswXFmngW77sHDsbP1LLQV4YrqU/x5UAwIDAQABAoIBAA0bfwzFVp5xvgn4
7fLvWL2b9IbMrAHKQ4M7QGRI5PNhOebOon75raFB/NZSAlYCrnoWQdzrzujUqvbO
LGORYq5u1YM/VLZo8zWEFXVWqOrD7mFqsXY2jh5xKZxPFfHej8MGaET+uOfb20jH
vvz3+WKTK+0lcG04rTsHwFu1Qgt4Vm8vonvxFF8Wa33pWeezGzePbToOB+bNRqBA
GU66lTK4XKGEjauNLB7OdncX2VML5iDkWYIi10xYMx9zUZczj2zwizgw0H5v3aNm
u13h9j0Bo82GtIjjXr1dgZlA+0ZRUUvo3+i0qNAkY9y10TRnOtXripys51yoQmBo
FK4IiEkCgYEA81RMJ4v+UfqaRVTRLkjYt4r5JA+PUp4DxekVwc1h2gJEiIQrgGNP
mpz+kqtupJMpF/X6TbcMxDbeisznykT2bRblykxQkWPSCt/1a7iKgAHi+BwxQsCB
CzZH48eI4rpyHex1qd3p0qYqKlXr7rKw8ODZk6ex3YRa9qPBdoVVAd8CgYEA8luI
YmsBS+Qiuy6vbgmSC3PtUiZBE/xwpAD4goC1OWzVZUQnEMyGRd58qu72b7o/EEyB
zRn3i4xxpfnPPt+e+FcwD8eTNSbz4Ozm6tcCRbT/8Ug/V++5s0UV84UAmtp6og3R
35qr9vjGOTthcG8mcbVow8hqzKIrkY8F82x0Gl0CgYA4NIaKs/mAsiQkU50l1cnJ
S56Ux8tRSBKTCm3uICS0GMX/ypfJxibDDfR3qIWcGinp0PWKMfgO8qWg5ge8XwWU
2S8m9U2+55HC1Ux5H11OiCEHMmvmgVTNZDJi2NozlOF7K/1ZyVqTP7KJqOMgdcIN
QcLAKoIZKtNgGR884ztpfQKBgDKrnHonMSAy1GgaPKde7N/kHuwb/2M0VkCTy2FN
k5YsAPmpJBnJCRG2kI4UZAW8BM9dj43YLf9JH8G51vCoRE5bvDqwWUC1oiuWnDjh
NyJn01MY7dVu0359pTdCyXuWzijvhr+fUPDT1m3E0nx1YK5JZVv5nQqnpUBLjMz2
EdgpAoGAJAdymob2NTa9YGWsdBBGCZyp3+h9MGYRVjoZ74l0BF38rVIAXGbfea31
UWFg1cB1hoCC6PAca2VGFSerJou3pgPHkxMbrsQdxSl+/DdHIAOWPPoW3AYLeWdM
fbSHXx0gw0hHzpKZTbL18TeMDhWQXm1c2D/9Gr0kxGRIIWXPRYE=
-----END RSA PRIVATE KEY-----
");
                var charge = Pingpp.Models.Charge.Create(param);

                return charge;
            }
            catch (Exception ex)
            {
                CFDGlobal.LogInformation("pingpp failed: " + ex.Message);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "参数错误"));
            }
        }

        [HttpGet]
        [Route("live/deposit/kuaiqian")]
        [BasicAuth]
        public ResultDTO NewKuaiQianDeposit(decimal amount)
        {
            var user = GetUser();
            string orderNo = "KQ" + DateTime.Now.ToString("yyyyMMddHHmmss");

            var kOrder = new KuaiQianOrder()
            {
                UserId = UserId,
                CreatedAt = DateTime.UtcNow,
                OrderNumber = orderNo,
                 OrderAmount = amount,
            };
            db.KuaiQianOrders.Add(kOrder);

            CFDGlobal.LogInformation("NewKuaiQianDeposit - amount:" + amount + " UserID:" + this.UserId);           
            db.SaveChanges();

            string merchantAcctId = "1001213884201";
            string inputCharset = "1";
            string pageUrl = "";
            string bgUrl = "http://300f8c59436243fe920fce09eb87d765.chinacloudapp.cn/api/kuaiqian/success";
            string version = "v2.0";
            string language = "1";
            string signType = "4";
            string payerName = user.Nickname;
            string payerContactType = "1";
            string payerContact = user.Phone;
            string orderId = DateTime.Now.ToString("yyyyMMddHHmmss");
            string orderAmount = ((int)(amount * 100M)).ToString();
            string orderTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            string productName = "TradeHero";
            string productNum = "1";
            string payType = "00";

            string signMsgVal = "";
            signMsgVal = appendParam(signMsgVal, "inputCharset", inputCharset);
            signMsgVal = appendParam(signMsgVal, "pageUrl", pageUrl);
            signMsgVal = appendParam(signMsgVal, "bgUrl", bgUrl);
            signMsgVal = appendParam(signMsgVal, "version", version);
            signMsgVal = appendParam(signMsgVal, "language", language);
            signMsgVal = appendParam(signMsgVal, "signType", signType);
            signMsgVal = appendParam(signMsgVal, "merchantAcctId", merchantAcctId);
            signMsgVal = appendParam(signMsgVal, "payerName", payerName);
            signMsgVal = appendParam(signMsgVal, "payerContactType", payerContactType);
            signMsgVal = appendParam(signMsgVal, "payerContact", payerContact);
            signMsgVal = appendParam(signMsgVal, "orderId", orderId);
            signMsgVal = appendParam(signMsgVal, "orderAmount", orderAmount);
            signMsgVal = appendParam(signMsgVal, "orderTime", orderTime);
            signMsgVal = appendParam(signMsgVal, "productName", productName);
            signMsgVal = appendParam(signMsgVal, "productNum", productNum);
            //signMsgVal = appendParam(signMsgVal, "productId", productId);
            //signMsgVal = appendParam(signMsgVal, "productDesc", productDesc);
            //signMsgVal = appendParam(signMsgVal, "ext1", ext1);
            //signMsgVal = appendParam(signMsgVal, "ext2", ext2);
            signMsgVal = appendParam(signMsgVal, "payType", payType);
            //signMsgVal = appendParam(signMsgVal, "redoFlag", redoFlag);
            //signMsgVal = appendParam(signMsgVal, "pid", pid);

            string signMsg = "";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(signMsgVal);
            X509Certificate2 cert = new X509Certificate2(HttpContext.Current.Server.MapPath("~/Certificate/99bill-rsa.pfx"), "123456", X509KeyStorageFlags.MachineKeySet);
            RSACryptoServiceProvider rsapri = (RSACryptoServiceProvider)cert.PrivateKey;
            RSAPKCS1SignatureFormatter f = new RSAPKCS1SignatureFormatter(rsapri);
            byte[] result;
            f.SetHashAlgorithm("SHA1");
            SHA1CryptoServiceProvider sha = new SHA1CryptoServiceProvider();
            result = sha.ComputeHash(bytes);
            signMsg = System.Convert.ToBase64String(f.CreateSignature(result)).ToString();


            ResultDTO resultDTO = new ResultDTO();
            resultDTO.success = true;
            resultDTO.message = signMsgVal + "&signMsg=" + signMsg;

            return resultDTO;
        }

        public string appendParam(string returnStr, string paramId, string paramValue)
        {
            if (returnStr != "")
            {
                if (paramValue != "")
                {
                    returnStr += "&" + paramId + "=" + paramValue;
                }
            }
            else
            {
                if (paramValue != "")
                {
                    returnStr = paramId + "=" + paramValue;
                }
            }
            return returnStr;
        }

        [HttpGet]
        [Route("live/deposit/pingpp/result/{order}")]
        [BasicAuth]
        public bool GetPingDepositResult(string order)
        {
            var item = db.PingOrders.FirstOrDefault(p => p.UserId == this.UserId && p.OrderNumber == order);
            if(item.WebHookResult == "charge.succeeded")
            {
                return true;
            }

            return false;
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

        [HttpPost]
        [Route("live/login")]
        [BasicAuth]
        public ResultDTO AyondoLiveLogin(AyondoLiveLoginFormDTO form)
        {
            var user = GetUser();
            string msg=null;

            using (var clientHttp = new AyondoTradeClient(true))
            {
                //try
                //{
                //    var logOut = clientHttp.LogOut(user.AyLiveUsername);
                //}
                //catch (FaultException e)
                //{
                //    CFDGlobal.LogLine("logout error");
                //    CFDGlobal.LogException(e);
                //    msg = e.Message;
                //}

                //Thread.Sleep(3000);

                try
                {
                    var balanceReport = clientHttp.GetBalance(form.username, form.password);
                }
                catch (FaultException e)
                {
                    //CFDGlobal.LogLine("get balance error");
                    //CFDGlobal.LogException(e);
                    msg = e.Message;
                }
            }

            if(msg!=null)
                return new ResultDTO(false) {message = msg};

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
            var liveStatus = CFDUsers.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
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
            var liveStatus = CFDUsers.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
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
            var liveStatus = CFDUsers.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
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
                || CFDUsers.GetUserLiveAccountStatus(user.AyLiveAccountStatus) == UserLiveStatus.Rejected //has been rejected, new application guid is required
                )
            {
                var initResult = AMSLiveAccountInitiate();
                var accountGuid = initResult["data"]["accountGuid"].Value<string>();

                user.AyLiveAccountGuid = accountGuid;

                //clear previous mifid result
                userInfo.MifidGuid = null;

                db.SaveChanges();
            }

            //if no Mifid Test result, submit Mifid info
            if (userInfo.MifidGuid == null)
            {
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

                CFDGlobal.LogInformation("MiFID result: account " + user.AyLiveAccountGuid + " mifid " + mifidGuid +
                                         " ruleset " +
                                         rulesetId + " score " + appropriatenessScore + " resolution " +
                                         appropriatenessResolution);

                //save to db
                userInfo.MifidGuid = mifidGuid;
                userInfo.MifidRulesetId = rulesetId;
                userInfo.AppropriatenessScore = appropriatenessScore;
                userInfo.AppropriatenessResolution = appropriatenessResolution;
                db.SaveChanges();
            }

            //When Mifid Test Failed
            if (userInfo.AppropriatenessResolution == "Failed" && form.confirmMifidOverride == null)
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

            var json = AMSLiveAccountComplete(user.AyLiveAccountGuid, userInfo.MifidGuid, form, user, userInfo);

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
            userInfo.Birthday = CFDUsers.GetBirthdayFromIdCode(userInfo.IdCode, ".");

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

            var liveStatus = CFDUsers.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);
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
            var liveStatus = CFDUsers.GetUserLiveAccountStatus(user.AyLiveUsername, user.AyLiveAccountStatus);

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
            EFT ： 出金申请
            WeCollect - CUP ： Wecollect入金
            Adyen - Skrill
            Bank Wire ： 大于0是退回，小于0是出金受理
            Transaction Fee ： 入金手续费 （可能也包含出金）
            Trade Result ： 交易
            Financing ： 隔夜费
            Dividend ： 分红
            Bonus : 交易金
             */

            //只显示以下类型的数据
            List<string> limitedTypes = new List<string>();
            limitedTypes.AddRange(Transfer.UserVisibleTypes);

            if (!user.AyLiveAccountId.HasValue)
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.Live_Acc_Not_Exist)));

            List<TransferDTO> results = new List<TransferDTO>();

            var transfers = db.AyondoTransferHistory_Live.Where(t => t.TradingAccountId == user.AyLiveAccountId && limitedTypes.Contains(t.TransferType)).OrderByDescending(o=>o.ApprovalTime).ToList();
            transfers.ForEach(t =>
            {
                var result = Transfer.getTransDescriptionColor(t.TransferType, t.Amount.HasValue? t.Amount.Value : 0);
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
                        showData = o.Following.ShowData ?? CFDUsers.DEFAULT_SHOW_DATA,
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
            //如果showData为false，则showOpenCloseData一定为False
            user.ShowOpenCloseData = form.showData? form.showOpenCloseData:false;
            db.SaveChanges();

            return new ResultDTO(true);
        }

        [HttpGet]
        [Route("live/report")]
        [IPAuth]
        public List<UserReportDTO> GetUserReport()
        {
            var users = db.Users.Where(o => o.AyLiveUsername != null).OrderByDescending(o=>o.AyLiveApplyAt).ToList();

            var userIds = users.Select(o => o.Id).ToList();
            var userInfos = db.UserInfos.Where(o => userIds.Contains(o.UserId)).ToList();
            var devices = db.Devices.Where(o => o.userId.HasValue && userIds.Contains(o.userId.Value)).ToList();

            var chinaToday = DateTimes.GetChinaToday();

            return users.Select(o =>
            {
                var userInfo = userInfos.FirstOrDefault(i => i.UserId == o.Id);
                var lastDevice =
                    devices.Where(d => d.userId == o.Id).OrderByDescending(d => d.UpdateTime).FirstOrDefault();

                int? userAge = null;
                int? genderInt = null;
                string addr=null;
                if (userInfo != null)
                {
                    var year = userInfo.IdCode.Substring(6, 4).ToInt();
                    var month = userInfo.IdCode.Substring(10, 2).ToInt();
                    var day = userInfo.IdCode.Substring(12, 2).ToInt();
                    var birth = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Local);

                    userAge = chinaToday.Year - year;
                    if (birth.AddYears(userAge.Value) > chinaToday)
                        userAge--;

                    genderInt = userInfo.IdCode.Substring(16, 1).ToInt();

                    addr = userInfo.Addr;
                }

                var dto= new UserReportDTO()
                {
                    id = o.Id,
                    age = userAge,
                    gender = genderInt%2,
                    accountId = o.AyLiveAccountId == null ? null : o.AyLiveAccountId.ToString(),
                    status = o.AyLiveAccountStatus,
                    applyAt =
                        o.AyLiveApplyAt == null
                            ? (DateTime?) null
                            : DateTime.SpecifyKind(o.AyLiveApplyAt.Value, DateTimeKind.Utc),
                    addr = addr,
                    nickname = o.Nickname,
                    phone = o.Phone,
                    approveAt =
                        o.AyLiveApproveAt == null
                            ? (DateTime?) null
                            : DateTime.SpecifyKind(o.AyLiveApproveAt.Value, DateTimeKind.Utc),
                    username = o.AyLiveUsername,
                };
                if (lastDevice != null)
                {
                    if (lastDevice.deviceType == 1)
                        dto.lastDeviceType = "android";
                    else if (lastDevice.deviceType == 2)
                        dto.lastDeviceType = "ios";
                    else
                        dto.lastDeviceType = "unknown";
                }
                return dto;
            }).ToList();
        }

        [HttpGet]
        [Route("live/report/dailyApprovedCount")]
        [IPAuth]
        public List<UserDailyApprovedCountDTO> GetDailyAyAccApprovedCounts()
        {
            var users = db.Users.Where(o=>o.AyLiveApproveAt!=null).ToList();

            var groupBy = users.GroupBy(o => o.AyLiveApproveAt.Value.AddHours(8).Date).ToList();

            return groupBy.Select(o => new UserDailyApprovedCountDTO() { date = DateTime.SpecifyKind(o.Key, DateTimeKind.Local), count = o.Count() }).OrderBy(o => o.date).ToList();
        }

        [HttpGet]
        [Route("live/report/dailyTransfer")]
        [IPAuth]
        public List<UserDailyTransferDTO> GetDailyTransferReport()
        {
            var result = db.AyondoTransferHistory_Live.Where(Transfer.IsDeposit())
                .GroupBy(o => DbFunctions.TruncateTime(DbFunctions.AddHours(o.ApprovalTime.Value, 8).Value))
                .Select(o => new UserDailyTransferDTO() {date = o.Key, deposit = o.Sum(p => p.Amount)})
                .ToList()
                .OrderBy(o => o.date)
                .Select(o => new UserDailyTransferDTO()
                {
                    date = DateTime.SpecifyKind(o.date.Value, DateTimeKind.Local),
                    deposit = o.deposit
                }).ToList();
            return result;
        }

        [HttpGet]
        [Route("live/report/transfer")]
        [IPAuth]
        public List<TransferReportDTO> GetTransferReport()
        {
            var result = db.AyondoTransferHistory_Live.Where(Transfer.IsDeposit())
                .Join(db.Users, o => o.TradingAccountId, o => o.AyLiveAccountId,
                    (o, u) => new TransferReportDTO()
                    {
                        amount = o.Amount,
                        ayLiveUsername = u.AyLiveUsername,
                        nickname = u.Nickname,
                        picUrl = u.PicUrl,
                        time = o.ApprovalTime,
                        type=o.TransferType,
                    })
                .OrderByDescending(o => o.time)
                .ToList();
            foreach (var o in result)
            {
                o.time = DateTime.SpecifyKind(o.time.Value, DateTimeKind.Utc);
            }
            return result;
        }

        [HttpGet]
        [Route("live/report/transfer/cumulative")]
        [IPAuth]
        public List<CumulativeTransferReportDTO> GetTransferCumulativeReport()
        {
            var deposits = db.AyondoTransferHistory_Live.Where(Transfer.IsDeposit())
                .GroupBy(o => DbFunctions.TruncateTime(DbFunctions.AddHours(o.ApprovalTime.Value, 8).Value))
                .Select(o => new  { date = o.Key, sum = o.Sum(p => p.Amount) })
                .ToList()
                .OrderBy(o => o.date)
                .Select(o => new
                {
                    date = DateTime.SpecifyKind(o.date.Value, DateTimeKind.Local),
                    deposit = o.sum
                }).ToList();

            var withdrawals = db.AyondoTransferHistory_Live.Where(o=>o.TransferType=="EFT")
               .GroupBy(o => DbFunctions.TruncateTime(DbFunctions.AddHours(o.ApprovalTime.Value, 8).Value))
               .Select(o => new { date = o.Key, sum = -o.Sum(p => p.Amount) })
               .ToList()
               .OrderBy(o => o.date)
               .Select(o => new
               {
                   date = DateTime.SpecifyKind(o.date.Value, DateTimeKind.Local),
                   withdrawal = o.sum
               }).ToList();

            var result = new List<CumulativeTransferReportDTO>();

            var beginDate = deposits.First().date > withdrawals.First().date
                ? withdrawals.First().date
                : deposits.First().date;
            var endDate = DateTimes.GetChinaToday();
            decimal cumulativeDeposit = 0;
            decimal cumulativeWithdrawal = 0;
            for (DateTime d = beginDate; d <= endDate; d = d.AddDays(1))
            {
                var deposit = deposits.FirstOrDefault(o => o.date == d);
                if (deposit != null)
                    cumulativeDeposit += deposit.deposit.Value;
                
                var withdrawal = withdrawals.FirstOrDefault(o => o.date == d);
                if (withdrawal != null)
                    cumulativeWithdrawal += withdrawal.withdrawal.Value;

                result.Add(new CumulativeTransferReportDTO() { date = d, deposit = cumulativeDeposit, withdrawal = cumulativeWithdrawal });
            }
            
            return result;
        }

        [HttpGet]
        [Route("live/report/thHoldingAcc")]
        [IPAuth]
        public THHoldingAccReportDTO GetTHHoldingAccReport()
        {
            var result=new THHoldingAccReportDTO();

            using (var clientHttp = new AyondoTradeClient(true))
            {
                try
                {
                    var report = clientHttp.GetBalance("TradeHeroHoldingAC", "dY$Tqn4KQ#");
                    result.balance = report.Value;
                }
                catch (FaultException<OAuthLoginRequiredFault>)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.OAUTH_LOGIN_REQUIRED)));
                }
            }

            result.transfers = db.AyondoTransferHistory_Live.Where(o => o.TradingAccountId == 104347406265)
                .Join(db.AyondoTransferHistory_Live, o => o.TransferId, o => o.TransferId,
                    delegate(AyondoTransferHistory_Live o, AyondoTransferHistory_Live u)
                    {
                        if (o.Id == u.Id) return null;
                        else
                        return new TransferReportDTO()
                        {
                            amount = o.Amount,
                            ayLiveUsername = u.Username,
                            time =DateTime.SpecifyKind(o.ApprovalTime.Value,DateTimeKind.Utc),
                            type = o.TransferType,
                        };
                    }).Where(o=>o!=null)
                .OrderByDescending(o => o.time)
                .ToList();

            return result;
        }

        [HttpGet]
        [Route("live/report/weeklytransfer")]
        [IPAuth]
        public string GetWeeklyTransferReport()
        {
            string queryString = @"select  * into #a from (
select sum(amount) deposite, accountId, u.Nickname,u.id, a.Timestamp from [dbo].[AyondoTransferHistory_Live] a join [user] u on u.AyLiveAccountId = a.TradingAccountId 
where (transfertype= 'WeCollect - CUP' or transfertype = 'Adyen - Skrill' or (transfertype='bank wire' and amount>=0 and len(transferid)=36) )
--and u.id =2026
 and (u.id > 3202) and u.id not in (3229, 3246, 3333, 3590, 5963,6098,6052)  and timestamp > dateadd(week, -1,getdate())
group by AccountId, u.Nickname,u.id,a.Timestamp) a order by deposite desc
---  select * from #a
select avg(deposite) 平均入金量 from #a  
 select identity(int,1,1) id, sum(deposite) d into #t from (select  distinct deposite,id from #a ) a group by id order by d
 declare @a int select @a= count(d)/2 from #t
  select top 1 d 入金中位数 from ( select top (@a) d from #t order by d) t order by d desc

 --select identity(int,1,1) id, investUSD into #t from [NewPositionHistory_live] where (userid > 3202) and userid not in (3229, 3246, 3333, 3590, 5963,6098,6052) order by investUSD   
 -- declare @a int select @a= count(investUSD)/2 from #t
 -- select top 1 investUSD from (select top (@a) investUSD from #t order by investUSD) t order by investUSD desc

select avg(d) 平均入金笔数 from (select count(deposite) d, id from #a group by id) a     select max(deposite) 最大一笔入金 from #a --按每笔充值分组
  select min(deposite) 最小一笔入金 from #a
  select max(de) 按用户充值分组最大入金数 from  (select sum(deposite) de,id from #a group by id) a --按用户充值分组
  select min(de) 按用户充值分组最小入金数 from  (select sum(deposite) de,id from #a group by id) a

  -- withdraw?  drop table #b
     select  * into #b from (
select sum(amount) withdraw, accountId, u.Nickname,u.id, a.Timestamp from [dbo].[AyondoTransferHistory_Live] a join [user] u on u.AyLiveAccountId = a.TradingAccountId  
where  transfertype= 'eft'   -- or ( transfertype='bank wire' and amount>=0 and len(transferid)=36 and u.id = 2026)
and (u.id > 3202) and u.id not in (3229, 3246, 3333, 3590, 5963,6098,6052)  and timestamp > dateadd(week, -1,getdate())
group by AccountId, u.Nickname,u.id,a.Timestamp) a order by withdraw desc

 -- select * from #b
select avg(withdraw) 近一周平均出金 from #b  -- select  (withdraw) from #b order by withdraw  

 select identity(int,1,1) iid, w into #b2 from ( select  id, sum(withdraw) w from #b group by id) a  order by w
 declare @a1 int select @a1= count(iid)  from #b2   --select @a
 begin
	if @a1%2 = 0
   select w 出金中位数 from #b2 where iid = @a1/2
   else if @a1%2 = 1
   select w 出金中位数 from #b2 where iid = (@a1+1)/2
  end 

-- select count(d) from (select count(withdraw) d, id from #b group by id) a   
select count(distinct id) 总出金人数 from #b --总出金人数
 select min(w) 最大出金 from  (select sum(withdraw) w from #b group by id) a    select max(w) 最小出金 from  (select sum(withdraw) w from #b group by id) a  where w <0   --  select sum(withdraw) w, id from #b group by id order by w";

            SqlDataAdapter adapter = new SqlDataAdapter(queryString, CFDGlobal.GetDbConnectionString("CFDEntities"));

            DataSet ds = new DataSet();
            adapter.Fill(ds);

            var sb = new StringBuilder();

            foreach (DataTable table in ds.Tables)
            {
                sb.Append("<table>");

                sb.Append("<tr>");
                foreach (DataColumn column in table.Columns)
                {
                    sb.Append("<td>" + column + "</td>");
                }

                sb.Append("</tr>");

                foreach (DataRow dr in table.Rows)
                {
                    sb.Append("<tr>");
                    foreach (var o in dr.ItemArray)
                    {
                        sb.Append("<td>" + o + "</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }

            return sb.ToString();
        }

        [HttpPost]
        [Route("language")]
        [BasicAuth]
        public ResultDTO SetLanguage(MeDTO form)
        {
            ResultDTO dto = new ResultDTO(true);
            var user = db.Users.FirstOrDefault(u => u.Id == this.UserId);
            if(user == null)
            {
                dto.success = false;
                dto.message = "User不存在";
                return dto;
            }

            user.language = form.language;
            db.SaveChanges();
            return dto;
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

        //[HttpGet]
        //[Route("live/deposit/alipay")]
        //[BasicAuth]
        //public HttpResponseMessage NewAlipayDeposit(decimal amount)
        //{
        //    IAopClient client = new DefaultAopClient("https://openapi.alipay.com/gateway.do", "app_id", "merchant_private_key", "json", "1.0", "RSA2", "alipay_public_key", "GBK", false);
        //    AlipayTradeWapPayRequest request = new AlipayTradeWapPayRequest();
        //    request.BizContent = "{" +
        //    "    \"body\":\"对一笔交易的具体描述信息。如果是多种商品，请将商品描述字符串累加传给body。\"," +
        //    "    \"subject\":\"大乐透\"," +
        //    "    \"out_trade_no\":\"70501111111S001111119\"," +
        //    "    \"timeout_express\":\"90m\"," +
        //    "    \"total_amount\":"+amount+"," +
        //    "    \"product_code\":\"QUICK_WAP_WAY\"" +
        //    "  }";
        //    AlipayTradeWapPayResponse response = client.pageExecute(request);
        //    string form = response.Body;
        //    //Response.Write(form);

        //    return new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent(form) };
        //}
    }
}