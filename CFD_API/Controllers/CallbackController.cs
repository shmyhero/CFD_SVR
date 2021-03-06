﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using CFD_API.Caching;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.OpenSsl;
using CFD_COMMON.Utils;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using System.Transactions;

namespace CFD_API.Controllers
{
    [RoutePrefix("api")]
    public class CallbackController : CFDController
    {
        public CallbackController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [HttpPut]
        [Route("Live/live-account/{accountGuid}/status")]
        public LifecycleCallbackDTO LiveAccountStatusCallback(string accountGuid, LifecycleCallbackFormDTO form)
        {
            var authorization = Request.Headers.Authorization;

            //if (authorization != null)
            //    CFDGlobal.LogWarning("Lifecycle Callback header: " + authorization.Scheme + " " + authorization.Parameter);

            if (authorization == null || authorization.Parameter == null || authorization.Parameter != CFDGlobal.AMS_CALLBACK_AUTH_TOKEN)
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "invalid auth token"));

            if (form != null)
            {
                CFDGlobal.LogInformation("AMS Callback live-account status: " + (accountGuid ?? "") + " " + (form.status ?? ""));

                if (!string.IsNullOrWhiteSpace(accountGuid) && !string.IsNullOrWhiteSpace(form.status))
                {
                    var user = db.Users.FirstOrDefault(o => o.AyLiveAccountGuid == accountGuid);
                    if (user != null)
                    {
                        if (CFDUsers.GetUserLiveAccountStatus(user.AyLiveAccountStatus) != UserLiveStatus.Active &&
                            CFDUsers.GetUserLiveAccountStatus(form.status) == UserLiveStatus.Active)
                        {
                            user.AyLiveApproveAt = DateTime.UtcNow;
                        }

                        user.AyLiveAccountStatus = form.status;
                        
                        #region 实盘注册成功后发送短信提醒用户,并赠送50元交易金
                        var liveReward = db.LiveRegisterRewards.FirstOrDefault(o => o.UserId == user.Id);
                        if(form.status.ToLower() == "pendinglogin" && liveReward == null)
                        {
                            decimal amount = RewardService.REWARD_LIVE_REG;
                        
                            YunPianMessenger.SendSms(string.Format("【盈交易】恭喜完成开户，{0}元交易金已打入您的账户，完成首笔入金就可以交易啦！", amount), user.Phone);
                            db.LiveRegisterRewards.Add(new CFD_COMMON.Models.Entities.LiveRegisterReward() {
                                UserId = user.Id,
                                Amount = amount,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                        #endregion

                        db.SaveChanges();

                    }
                }
            }

            return new LifecycleCallbackDTO();
        }

        [HttpGet]
        [Route("demo/oauth")]
        public HttpResponseMessage AyondoDemoOAuth()
        {
            var queryNameValuePairs = Request.GetQueryNameValuePairs();
            //CFDGlobal.LogInformation(oauth_token+" "+state+" "+expires_in);

            var currentUrl = Request.RequestUri.GetLeftPart(UriPartial.Path);

            var errorResponse = Request.CreateResponse(HttpStatusCode.Redirect);
            errorResponse.Headers.Location = new Uri(currentUrl + "/error");

            var error = queryNameValuePairs.FirstOrDefault(o => o.Key == "error").Value;
            if (!string.IsNullOrWhiteSpace(error))
            {
                string log = queryNameValuePairs.Aggregate("Demo OAuth error: ",
                    (current, pair) => current + (pair.Key + " " + pair.Value + ", "));
                CFDGlobal.LogInformation(log);

                //return "ERROR";
                return errorResponse;
            }

            var oauth_token = queryNameValuePairs.FirstOrDefault(o => o.Key == "oauth_token").Value;
            if (!string.IsNullOrWhiteSpace(oauth_token))
            {
                var bytes = Convert.FromBase64String(oauth_token);

                var decryptEngine = new Pkcs1Encoding(new RsaEngine());
                using (var txtreader = new StringReader(CFDGlobal.OAUTH_TOKEN_PUBLIC_KEY))
                {
                    var keyParameter = (AsymmetricKeyParameter)new PemReader(txtreader).ReadObject();
                    decryptEngine.Init(false, keyParameter);
                }

                var decrypted = Encoding.UTF8.GetString(decryptEngine.ProcessBlock(bytes, 0, bytes.Length));

                var split = decrypted.Split(':');
                var username1 = split[0];
                var username2 = split[1]; //ayondo username
                var expiry = split[2];
                var checksum = split[3];

                //// check if cfd userid and ayondo username are bound
                //var state = queryNameValuePairs.FirstOrDefault(o => o.Key == "state").Value;
                //int userId;
                //var tryParse = int.TryParse(state, out userId);

                //if (!tryParse)
                //{
                //    CFDGlobal.LogInformation("oauth DEMO error: state tryParse to int32 failed " + state);
                //    return errorResponse;
                //}

                //var user = db.Users.FirstOrDefault(o => o.Id == userId);
                //if (user == null || user.AyondoUsername != username2)
                //{
                //    CFDGlobal.LogInformation("oauth DEMO error: cfd user id and ayondo demo username doesn't match "+ user.AyondoUsername + " " + username2);
                //    return errorResponse;
                //}

                using (var client = new AyondoTradeClient())
                {
                    var account = client.LoginOAuth(username2, oauth_token);

                    CFDGlobal.LogInformation("Demo OAuth logged in: " + username2 + " " + account);
                }

                //return "OK";
                var okResponse = Request.CreateResponse(HttpStatusCode.Redirect);
                okResponse.Headers.Location = new Uri(currentUrl + "/ok");
                return okResponse;
            }

            return errorResponse;
        }

        [HttpGet]
        [Route("demo/oauth/ok")]
        public string AyondoDemoOAuthOK()
        {
            return "OK";
        }

        [HttpGet]
        [Route("demo/oauth/error")]
        public string AyondoDemoOAuthError()
        {
            return "ERROR";
        }

        [HttpGet]
        [Route("live/oauth")]
        public HttpResponseMessage AyondoLiveOAuth()
        {
            var queryNameValuePairs = Request.GetQueryNameValuePairs();
            //CFDGlobal.LogInformation(oauth_token+" "+state+" "+expires_in);

            var currentUrl = Request.RequestUri.GetLeftPart(UriPartial.Path);

            var errorResponse = Request.CreateResponse(HttpStatusCode.Redirect);
            //errorResponse.Headers.Location = new Uri(currentUrl + "/error");
            errorResponse.Headers.Location = new Uri(
                CFDGlobal.TH_WEB_HOST + "tradehub/live/login.html?client_id=62d275a211&redirect_uri=https://"+ CFDGlobal.WEB_API_PUB_HOST + "/api/live/oauth&loginError=error");

            var error = queryNameValuePairs.FirstOrDefault(o => o.Key == "error").Value;
            if (!string.IsNullOrWhiteSpace(error))
            {
                string log = queryNameValuePairs.Aggregate("Live OAuth error: ",
                    (current, pair) => current + (pair.Key + " " + pair.Value + ", "));
                CFDGlobal.LogWarning(log + " Callback URL: " + Request.RequestUri.AbsoluteUri);

                //return "ERROR";
                return errorResponse;
            }

            var oauth_token = queryNameValuePairs.FirstOrDefault(o => o.Key == "oauth_token").Value;

            if (!string.IsNullOrWhiteSpace(oauth_token))
            {
                var bytes = Convert.FromBase64String(oauth_token);

                var decryptEngine = new Pkcs1Encoding(new RsaEngine());
                using (var txtreader = new StringReader(CFDGlobal.OAUTH_TOKEN_PUBLIC_KEY_Live))
                {
                    var keyParameter = (AsymmetricKeyParameter)new PemReader(txtreader).ReadObject();
                    decryptEngine.Init(false, keyParameter);
                }

                var decrypted = Encoding.UTF8.GetString(decryptEngine.ProcessBlock(bytes, 0, bytes.Length));

                var split = decrypted.Split(':');
                var username1 = split[0];
                var username2 = split[1];//ayondo username
                var expiry = split[2];
                var checksum = split[3];

                //// check if cfd userid and ayondo username are bound
                //var state = queryNameValuePairs.FirstOrDefault(o => o.Key == "state").Value;
                //int userId;
                //var tryParse = int.TryParse(state, out userId);

                //if (!tryParse)
                //{
                //    CFDGlobal.LogInformation("oauth LIVE error: state tryParse to int32 failed " + state);
                //    return errorResponse;
                //}

                //var user = db.Users.FirstOrDefault(o => o.Id == userId);
                //if (user == null || user.AyLiveUsername != username2)
                //{
                //    CFDGlobal.LogInformation("oauth LIVE error: cfd user id and ayondo live username doesn't match "+ user.AyLiveUsername + " " + username2);
                //    return errorResponse;
                //}

                string account;
                using (var client = new AyondoTradeClient(true))
                {
                    try
                    {
                        account = client.LoginOAuth(username2, oauth_token);
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogWarning("live oauth login failed. username: " + username2);
                        CFDGlobal.LogExceptionAsWarning(e);
                        return errorResponse;
                    }
                }
                CFDGlobal.LogLine("Live OAuth login: " + username2 + " " + account);

                //update ayondo account id if not same
                try
                {
                    long accountId = 0;
                    if (long.TryParse(account, out accountId))
                    {
                        var user = db.Users.FirstOrDefault(o => o.AyLiveUsername == username2);
                        if (user != null && user.AyLiveAccountId != accountId)
                        {
                            user.AyLiveAccountId = accountId;
                            db.SaveChanges();
                        }
                    }
                }
                catch (Exception e)
                {
                    CFDGlobal.LogWarning("live oauth login - saving account id to db failed");
                    CFDGlobal.LogExceptionAsWarning(e);
                }

                //return "OK";
                var okResponse = Request.CreateResponse(HttpStatusCode.Redirect);
                //okResponse.Headers.Location = new Uri(currentUrl + "/ok");
                okResponse.Headers.Location = new Uri(CFDGlobal.TH_WEB_HOST + "tradehub/live/loginload.html");
                return okResponse;
            }

            return errorResponse;
        }

        [HttpGet]
        [Route("live/oauth/ok")]
        public string AyondoLiveOAuthOK()
        {
            return "OK";
        }

        [HttpGet]
        [Route("live/oauth/error")]
        public string AyondoLiveOAuthError()
        {
            return "ERROR";
        }

        [HttpPut]
        [Route("live/lifecycle")]
        public LifecycleCallbackDTO AyondoLiveAccountLifecycleCallback(LifecycleCallbackFormDTO form)
        {
            var authorization = Request.Headers.Authorization;

            //if (authorization != null)
            //    CFDGlobal.LogWarning("Lifecycle Callback header: " + authorization.Scheme + " " + authorization.Parameter);

            if (authorization == null || authorization.Parameter == null || authorization.Parameter != CFDGlobal.AMS_CALLBACK_AUTH_TOKEN)
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "invalid auth token"));

            if (form != null)
            {
                CFDGlobal.LogWarning("Lifecycle Callback form: " + (form.Guid ?? "") + " " + (form.Status ?? ""));

                if (!string.IsNullOrWhiteSpace(form.Guid) && !string.IsNullOrWhiteSpace(form.Status))
                {
                    var user = db.Users.FirstOrDefault(o => o.AyLiveAccountGuid == form.Guid);
                    if (user != null)
                    {
                        user.AyLiveAccountStatus = form.Status;
                        db.SaveChanges();
                    }
                }
            }

            return new LifecycleCallbackDTO();
        }

        [HttpPut]
        [Route("Live/live-account/{accountGuid}/reference-account/{referenceAccountGuid}/status")]
        public ResultDTO UpdateReferenceAccount(string accountGuid, string referenceAccountGuid, BankCardUpdateDTO form)
        {
            var authorization = Request.Headers.Authorization;

            if (authorization == null || authorization.Parameter == null || authorization.Parameter != CFDGlobal.AMS_CALLBACK_AUTH_TOKEN)
            {
                CFDGlobal.LogWarning("update reference account: invalid token");
                //throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "invalid auth token"));
                return new ResultDTO(false);
            }

            if (string.IsNullOrEmpty(referenceAccountGuid))
            {
                CFDGlobal.LogWarning("update reference account: GUID is null");
                //throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "GUID is null"));
                return new ResultDTO(false);
            }

            CFDGlobal.LogInformation("reference account: GUID:" + referenceAccountGuid);

            var user = db.Users.FirstOrDefault(o => o.ReferenceAccountGuid == referenceAccountGuid);
            if (user == null)
            {
                CFDGlobal.LogWarning("update reference account: can't find user by given reference account guid:" + referenceAccountGuid);
                //throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "can't find user by guid"));
                return new ResultDTO(false);
            }
            user.BankCardStatus = form.status;

            if (form.status == BankCardUpdateStatus.Rejected)
            {
                user.BankCardRejectReason = form.rejectionType == "Other" ? form.rejectionInfo : form.rejectionType;
            }

            if(form.status == BankCardUpdateStatus.PendingReview)
            {
                user.BankCardSubmitAt = DateTime.Now; //记录申请时间。
            }
            else
            {
                user.BankCardApprovedAt = DateTime.Now; //记录审核时间。

                try
                {
                    #region 发送短信和保存Message
                    Message_Live msg = new Message_Live();
                    msg.UserId = user.Id;
                    msg.Title = "绑卡消息";
                    if (form.status == BankCardUpdateStatus.Approved)
                    {
                        YunPianMessenger.SendSms("【盈交易】恭喜！您的银行卡绑定成功啦！", user.Phone);

                        msg.Body = "恭喜您！您的银行卡绑定成功啦！";
                        msg.CreatedAt = DateTime.UtcNow;
                        msg.IsReaded = false;
                    }
                    else
                    {
                        YunPianMessenger.SendSms("【盈交易】好遗憾呀！您的银行卡绑定失败，马上查看原因，或联系客服！", user.Phone);
                        msg.Body = "好遗憾呀！您的银行卡绑定失败，马上查看原因，或联系客服！";
                        msg.CreatedAt = DateTime.UtcNow;
                        msg.IsReaded = false;
                    }
                    db.Message_Live.Add(msg);
                    #endregion
                }
                catch(Exception ex)
                {
                    CFDGlobal.LogException(ex);
                }

            }

            db.SaveChanges();

            return new ResultDTO(true);
        }

        [HttpPost]
        [Route("alipay/notify")]
        public HttpResponseMessage AlipayNofityCallback()
        {
            return new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent("success")};
        }

        [HttpPost]
        [Route("pingpp/success")]
        public HttpResponseMessage PingppSuccess()
        {
            //using (var client = new AyondoTradeClient(true))
            //{
            //    var balance = client.GetBalance("TradeHeroHoldingAC", "dY$Tqn4KQ#");
            //    var balance2 = client.GetBalance("jiangyi1985", "tradehero");
            //    //var newCashTransferId = client.NewCashTransfer("TradeHeroHoldingAC", "dY$Tqn4KQ#", 1, "");
            //}

            string requestStr = Request.Content.ReadAsStringAsync().Result;
            CFDGlobal.LogInformation("ping++ success, request body:" + requestStr);

            var jObject = JObject.Parse(requestStr);
            var type = jObject.SelectToken("type").ToString();
            var orderNumberStr = jObject["data"]["object"].SelectToken("order_no").ToString();
            var amountNet = jObject["data"]["object"].SelectToken("amount_settle").Value<decimal>();
            decimal thFeeRate = 0.01M; //TradeHero收取的手续费
            Misc feeSetting = db.Miscs.OrderByDescending(o => o.Id).FirstOrDefault(o => o.Key == "PingFeeRate");
            if (feeSetting != null)
            {
                thFeeRate = decimal.Parse(feeSetting.Value);
            }

            //using (var transactionScope = new TransactionScope())
            //{
            var pOrder = db.PingOrders.FirstOrDefault(p => p.OrderNumber == orderNumberStr);
            if (pOrder != null)
            {
                pOrder.WebHookAt = DateTime.UtcNow;
                pOrder.WebHookResult = type;

                if (type == "charge.succeeded")
                {
                    var cache = WebCache.GetInstance(true);
                    var prod = cache.ProdDefs.FirstOrDefault(p => p.Name == "USD/CNY Outright");
                    var quote = cache.Quotes.FirstOrDefault(o => o.Id == prod.Id);
                    if (quote != null)
                    {
                        pOrder.FxRate = quote.Offer;
                        pOrder.FxRateAt = quote.Time;
                        pOrder.AmountNet = amountNet;
                        pOrder.AmountAdjusted = pOrder.AmountCNY * (1 - thFeeRate);
                        pOrder.AmountUSD = Decimals.RoundIfExceed(pOrder.AmountAdjusted.Value / quote.Offer, 2);
                    }
                    else
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "获取汇率失败"));
                    }

                    //找到对应的订单，且该订单未被支付过
                    var orderRewardUsage = db.OrderRewardUsages.FirstOrDefault(o => o.OrderNumber == orderNumberStr && !o.PingPaidAt.HasValue && !o.AyTransReqSentAt.HasValue);
                    if (orderRewardUsage != null)
                    {
                        orderRewardUsage.PingPaidAt = DateTime.UtcNow;
                    }
                }

                db.SaveChanges();
            }

            //transactionScope.Complete();
            //}

            return new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent("success")};
        }

        [HttpPost]
        [Route("kuaiqian/success")]
        public HttpResponseMessage KuaiQianSuccess()
        {
            string requestStr = Request.Content.ReadAsStringAsync().Result;
            CFDGlobal.LogInformation("kuaiqian payment success, request body:" + requestStr);

            var jObject = JObject.Parse(requestStr);
            //快钱的支付方式
            var payType = jObject.Property("payType")==null? string.Empty : jObject["payType"].Value<string>();
            //银行代码
            var bankId = jObject.Property("bankId") == null ? string.Empty : jObject["bankId"].Value<string>();
            //银行卡号
            var bankCardId = jObject.Property("bankCardId") == null ? string.Empty : jObject["bankCardId"].Value<string>();
            //订单号
            var orderNumber = jObject.Property("orderId") == null ? string.Empty : jObject["orderId"].Value<string>();
            //订单金额
            decimal orderAmount = jObject.Property("orderAmount") == null ? 0 : jObject["orderAmount"].Value<decimal>();
            //快钱的交易Id
            string dealId = jObject.Property("dealId") == null ? string.Empty : jObject["dealId"].Value<string>();
            //支付金额
            decimal payAmount = jObject.Property("payAmount") == null ? 0 : jObject["payAmount"].Value<decimal>();
            //快钱收取的手续费
            decimal fee = jObject.Property("fee") == null ? 0 : jObject["fee"].Value<decimal>();
            //处理结果， 10支付成功，11 支付失败，00订单申请成功，01 订单申请失败
            string payResult = jObject.Property("payResult") == null ? string.Empty : jObject["payResult"].Value<string>();

            decimal thFeeRate = 0.01M; //TradeHero收取的手续费
            Misc feeSetting = db.Miscs.OrderByDescending(o => o.Id).FirstOrDefault(o => o.Key == "PingFeeRate");
            if (feeSetting != null)
            {
                thFeeRate = decimal.Parse(feeSetting.Value);
            }

            var kOrder = db.KuaiQianOrders.FirstOrDefault(k => k.OrderNumber == orderNumber);
            if (kOrder != null)
            {
                var cache = WebCache.GetInstance(true);
                var prod = cache.ProdDefs.FirstOrDefault(p => p.Name == "USD/CNY Outright");
                var quote = cache.Quotes.FirstOrDefault(o => o.Id == prod.Id);
                if (quote != null)
                {
                    kOrder.PayType = payType;
                    kOrder.BankId = bankId;
                    kOrder.BankCardId = bankCardId;
                    kOrder.OrderNumber = orderNumber;
                    kOrder.DealId = dealId;
                    kOrder.OrderAmount = orderAmount;
                    kOrder.PayAmount = payAmount;
                    kOrder.KuaiQianFee = fee;
                    kOrder.ReceiveAt = DateTime.UtcNow;
                    kOrder.ReceiveResult = payResult;

                    kOrder.FxRate = quote.Offer;
                    kOrder.FxRateAt = quote.Time;
                    kOrder.PayAmountAdjusted = kOrder.PayAmount * (1 - thFeeRate);
                    kOrder.PayAmountUSD = Decimals.RoundIfExceed(kOrder.PayAmountAdjusted.Value / quote.Offer, 2);

                }
                else
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "获取汇率失败"));
                }

                ////找到对应的订单，且该订单未被支付过
                //var orderRewardUsage = db.OrderRewardUsages.FirstOrDefault(o => o.OrderNumber == orderNumber && !o.PingPaidAt.HasValue && !o.AyTransReqSentAt.HasValue);
                //if (orderRewardUsage != null)
                //{
                //    orderRewardUsage.PingPaidAt = DateTime.UtcNow;
                //}

                db.SaveChanges();
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("success") };
        }
    }
}
