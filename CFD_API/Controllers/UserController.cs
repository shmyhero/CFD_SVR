using System;
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

namespace CFD_API.Controllers
{
    public class UserController : CFDController
    {
        public UserController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
            : base(db, mapper, redisClient)
        {
        }

        [HttpPost]
        //[RequireHttps]
        [ActionName("signupByPhone")]
        public SignupResultDTO SignupByPhone(SignupByPhoneFormDTO form)
        {
            var dtValidSince = DateTime.UtcNow.AddMinutes(-60);
            var verifyCodes = db.VerifyCodes.Where(o => o.Phone == form.phone && o.Code == form.verifyCode && o.SentAt > dtValidSince);

            var result = new SignupResultDTO();

            if (verifyCodes.Any())
            {
                var user = db.Users.FirstOrDefault(o => o.Phone == form.phone);

                if (user == null) //phone doesn't exist
                {
                    var userService = new UserService(db);
                    userService.CreateUserByPhone(form.phone);

                    //refetch
                    user = db.Users.FirstOrDefault(o => o.Phone == form.phone);

                    var nickname = "u" + user.Id.ToString("00000000");
                    user.Nickname = nickname;

                    //check duplicate nickname and generate random suffix
                    int tryCount = 0;
                    while (db.Users.Any(o => o.Id != user.Id && o.Nickname == user.Nickname))
                    {
                        user.Nickname = nickname + (new Random().Next(10000));

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
                        CreateAyondoAccount(user);
                    }
                    catch (Exception e)
                    {
                        CFDGlobal.LogException(e);
                    }
            }
            else
            {
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
            var result = new SignupResultDTO();

            var user = db.Users.FirstOrDefault(o => o.WeChatOpenId == form.openid);

            if (user == null) //openid not exist
            {
                var userService = new UserService(db);
                userService.CreateUserByWeChat(form.openid, form.unionid);

                //refetch
                user = db.Users.FirstOrDefault(o => o.WeChatOpenId == form.openid);

                user.Nickname = form.nickname.Trim();

                //check duplicate nickname and generate random suffix
                int tryCount = 0;
                while (db.Users.Any(o => o.Id != user.Id && o.Nickname == user.Nickname))
                {
                    user.Nickname = form.nickname + (new Random().Next(10000));

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
                    CreateAyondoAccount(user);
                }
                catch (Exception e)
                {
                    CFDGlobal.LogException(e);
                }

            return result;
        }

        private static readonly string AMS_HEADER_AUTH = "Bearer RDFFMzY2NDktMDlDRC00OTg4LUEwNjAtRUM0NDIxMTNDMDBCMDQ5QUU3NjgtRTUyMy00RkE0LTk5MTQtNTMwQUM1RjY5MDY5";
        private static readonly string AMS_HOST = "https://www.ayondo-ams.com/tradeherocn/";

        public void CreateAyondoAccount(User user)
        {
            //Must be 5-20 alphanumeric characters (letter and numerals only).
            //Usernames cannot be purely numeric.
            var username_base = "thcn" + user.Id;

            //At least 4 chars. Allowed chars: [0-9a-zA-Z\!\#\*\$\-\/\=\?\@\.\,\:\;]
            var password = Randoms.GetRandomAlphanumericString(6);

            bool isAvailable = false;
            int tryCount = 0;
            string username = username_base;
            do
            {
                var httpWebRequest = HttpWebRequest.CreateHttp(AMS_HOST + "check-username?AccountType=Demo&UserName=" + username);
                httpWebRequest.Headers["Authorization"] = AMS_HEADER_AUTH;
                var webResponse = httpWebRequest.GetResponse();
                var responseStream = webResponse.GetResponseStream();
                var sr = new StreamReader(responseStream);

                var jObject = JObject.Parse(sr.ReadToEnd());

                tryCount++;

                if (jObject["Error"] != null)
                {
                    CFDGlobal.LogInformation("AMS check-username error: " + jObject["Error"].Value<string>());
                }
                else
                {
                    isAvailable = jObject["IsAvailable"].Value<bool>();
                    bool isValid = jObject["IsValid"].Value<bool>();

                    if (!isAvailable || !isValid)
                    {
                        CFDGlobal.LogInformation("Ayondo check-user: " + username + " isAvailable:" + isAvailable + " isValid:" + isValid);

                        //generate new username for next attempt
                        username = username_base + Randoms.GetRandomAlphabeticString(4);
                    }
                }
            } while (!isAvailable && tryCount < 3); // retry if unavailable and tryCount < 3

            if (isAvailable)
            {
                var httpWebRequest = HttpWebRequest.CreateHttp(AMS_HOST + "DemoAccount");
                httpWebRequest.Headers["Authorization"] = AMS_HEADER_AUTH;
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentType = "application/json; charset=UTF-8";
                var requestStream = httpWebRequest.GetRequestStream();
                var sw = new StreamWriter(requestStream);

                //Escape the "{", "}" (by duplicating them) in the format string:
                var json =
                    @"{{
'AddressCity': 'TestCity',
'AddressCountry': 'CN',
'AddressLine1': 'Teststr. 123',
'AddressLine2': null,
'AddressZip': '12345',
'ClientIP': '127.0.0.1',
'Currency': 'USD',
'FirstName': 'User',
'Gender': 'Male',
'IsTestRecord': true,
'Language': 'EN',
'LastName': 'THCN',
'Password': '{1}',
'PhonePrimary': '0044 123445',
'SalesRepGuid':null,
'UserName': '{0}',
'ProductType': 'CFD'
}}";

                var s = string.Format(json, username, password);
                sw.Write(s);
                sw.Flush();
                sw.Close();

                var webResponse = httpWebRequest.GetResponse();
                var responseStream = webResponse.GetResponseStream();
                var sr = new StreamReader(responseStream);

                var jObject = JObject.Parse(sr.ReadToEnd());

                if (jObject["Error"] != null)
                {
                    CFDGlobal.LogWarning("AMS create account error: " + jObject["Error"].Value<string>() + " userId:" + user.Id + " ayondoUsername:" + username);
                }
                else
                {
                    var guid = jObject["Guid"].Value<string>();
                    CFDGlobal.LogInformation("Ayondo user created: userId:" + user.Id + " username:" + username + " password:" + password + " guid:" + guid);

                    user.AyondoUsername = username;
                    user.AyondoPassword = password;
                    db.SaveChanges();
                }
            }
        }

        [HttpGet]
        [BasicAuth]
        [ActionName("resetAyondoAccount")]
        public void ResetAyondoAccount()
        {
            var user = GetUser();
            CreateAyondoAccount(user);
        }

        [HttpGet]
        //[RequireHttps]
        [ActionName("me")]
        [BasicAuth]
        public UserDTO GetMe(LoginFormDTO form)
        {
            var user = GetUser();

            var userDto = Mapper.Map<UserDTO>(user);

            return userDto;
        }

        [HttpPost]
        //[RequireHttps]
        [ActionName("nickname")]
        [BasicAuth]
        public ResultDTO SetNickname(string nickname)
        {
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

        [HttpGet]
        [ActionName("balance")]
        [BasicAuth]
        public BalanceDTO GetBalance()
        {
            var clientHttp = new AyondoTradeClient();

            var user = GetUser();

            if (string.IsNullOrEmpty(user.AyondoUsername))
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.NO_AYONDO_ACCOUNT)));

            var balance = clientHttp.GetBalance(user.AyondoUsername, user.AyondoPassword);
            var positionReports = clientHttp.GetPositionReport(user.AyondoUsername, user.AyondoPassword);

            //var redisProdDefClient = RedisClient.As<ProdDef>();
            //var redisQuoteClient = RedisClient.As<Quote>();

            //var prodDefs = redisProdDefClient.GetAll();
            //var quotes = redisQuoteClient.GetAll();

            decimal marginUsed = 0;
            decimal totalUPL = 0;
            foreach (var report in positionReports)
            {
                var prodDef = WebCache.ProdDefs.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));

                if (prodDef == null) continue;

                //************************************************************************
                //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                //************************************************************************
                var tradeValue = report.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(report.LongQty ?? report.ShortQty);
                var tradeValueUSD = tradeValue;
                if (prodDef.Ccy2 != "USD")
                    tradeValueUSD = FX.Convert(tradeValue.Value, prodDef.Ccy2, "USD", WebCache.ProdDefs, WebCache.Quotes);

                marginUsed += tradeValueUSD.Value/report.Leverage.Value;

                //calculate UPL
                var quote = WebCache.Quotes.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                if (quote != null)
                {
                    totalUPL += report.LongQty.HasValue ? tradeValueUSD.Value*(quote.Bid/report.SettlPrice - 1) : tradeValueUSD.Value*(1 - quote.Offer/report.SettlPrice);
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
        [ActionName("plReport")]
        [BasicAuth]
        public List<PLReportDTO> GetPLReport()
        {
            var user = GetUser();
            if (string.IsNullOrEmpty(user.AyondoUsername))
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, __(TransKey.NO_AYONDO_ACCOUNT)));

            var clientHttp = new AyondoTradeClient();

            var endTime = DateTime.UtcNow;
            var startTime = DateTimes.GetHistoryQueryStartTime(endTime);

            var positionOpenReports = clientHttp.GetPositionReport(user.AyondoUsername, user.AyondoPassword);
            var positionHistoryReports = clientHttp.GetPositionHistoryReport(user.AyondoUsername, user.AyondoPassword, startTime, endTime);
            var groupByPositions = positionHistoryReports.GroupBy(o => o.PosMaintRptID);

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

                var prodDef = WebCache.ProdDefs.FirstOrDefault(o => o.Id == secId);

                if (prodDef == null) continue;

                //var dbSec = dbSecurities.FirstOrDefault(o => o.Id == secId);

                //************************************************************************
                //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                //************************************************************************
                var tradeValue = report.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(report.LongQty ?? report.ShortQty);
                var tradeValueUSD = tradeValue;
                if (prodDef.Ccy2 != "USD")
                    tradeValueUSD = FX.Convert(tradeValue.Value, prodDef.Ccy2, "USD", WebCache.ProdDefs, WebCache.Quotes);

                var invest = tradeValueUSD.Value/report.Leverage.Value;

                //calculate UPL
                decimal pl = 0;
                var quote = WebCache.Quotes.FirstOrDefault(o => o.Id == Convert.ToInt32(report.SecurityID));
                if (quote != null)
                {
                    pl += report.LongQty.HasValue ? tradeValueUSD.Value * (quote.Bid / report.SettlPrice - 1) : tradeValueUSD.Value * (1 - quote.Offer / report.SettlPrice);
                }
                else
                {
                    CFDGlobal.LogWarning("cannot find quote:" + report.SecurityID + " when calculating UPL for PLReport");
                }

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
                else if (prodDef.AssetClass == "Single Stocks" && Products.IsUsStocks(prodDef.Symbol))
                {
                    stockUSPL.invest += invest;
                    stockUSPL.pl += pl;
                }
            }

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

                        var prodDef = WebCache.ProdDefs.FirstOrDefault(o => o.Id == secId);

                        if (prodDef == null) continue;

                        //var dbSec = dbSecurities.FirstOrDefault(o => o.Id == secId);

                        //************************************************************************
                        //TradeValue (to ccy2) = QuotePrice * (MDS_LOTSIZE / MDS_PLUNITS) * quantity
                        //************************************************************************
                        var tradeValue = openReport.SettlPrice*prodDef.LotSize/prodDef.PLUnits*(openReport.LongQty ?? openReport.ShortQty);
                        var tradeValueUSD = tradeValue;
                        if (prodDef.Ccy2 != "USD")
                            tradeValueUSD = FX.Convert(tradeValue.Value, prodDef.Ccy2, "USD", WebCache.ProdDefs, WebCache.Quotes);

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
                        else if (prodDef.AssetClass == "Single Stocks" && Products.IsUsStocks(prodDef.Symbol))
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
    }
}