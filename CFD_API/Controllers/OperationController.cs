using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
using Newtonsoft.Json.Linq;
using ServiceStack.Redis;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/operation")]
    public class OperationController : CFDController
    {
        public OperationController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
            : base(db, mapper, redisClient)
        {
        }

        public string Test()
        {
            return null;
        }

        [HttpPost]
        public ResultDTO Push()
        {
            ResultDTO result = new ResultDTO() { success = true };
            var requestString = Request.Content.ReadAsStringAsync().Result;
            var requestObj = JObject.Parse(requestString);
            var phoneList = requestObj["phone"].Value<string>().Split(',').ToList();

            var tokenListQuery = from u in db.Users
                                             join d in db.Devices on u.Id equals d.userId
                                             where phoneList.Contains(u.Phone) 
                                             select new { d.deviceToken, u.Id, u.AyondoAccountId, u.AutoCloseAlert };

            var tokenList = tokenListQuery.ToList();
            string msg = requestObj["message"].Value<string>();

            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            string format = "{{\"type\":\"0\", \"title\":\"盈交易测试\", \"StockID\":0, \"CName\":\"\", \"message\":\"{0}\"}}";
            foreach(var token in tokenList)
            {
                list.Add(new KeyValuePair<string, string>(token.deviceToken, string.Format(format,msg)));
            }

            var push = new GeTui();
            var response = push.PushBatch(list);
            result.message = response;
            return result;
        }
    }
}