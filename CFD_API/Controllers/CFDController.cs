using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.Web;
using System.Web.Http;
using AutoMapper;
using AyondoTrade;
using CFD_COMMON;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using ServiceStack.Redis;

namespace CFD_API.Controllers
{
    public class CFDController : ApiController
    {
        public CFDEntities db { get; protected set; }
        public IMapper Mapper { get; protected set; }
        public IRedisClient RedisClient { get; protected set; }

        public CFDController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
        {
            this.db = db;
            this.Mapper = mapper;
            this.RedisClient = redisClient;
        }

        public CFDController(CFDEntities db, IMapper mapper)
        {
            this.db = db;
            this.Mapper = mapper;
        }

        protected CFDController(CFDEntities db)
        {
            this.db = db;
        }

        public int UserId
        {
            get
            {
                return Convert.ToInt32(HttpContext.Current.User.Identity.Name);
            }

            ////for unit testing
            //get; set;
        }

        public DateTime RequestStartAt { get; set; }

        /// <summary>
        /// localization
        /// </summary>
        /// <param name="transKey"></param>
        /// <returns></returns>
        public string __(TransKey transKey)
        {
            return Translator.Translate(transKey);
        }

        public User GetUser()
        {
            return db.Users.FirstOrDefault(o => o.Id == UserId);
        }

        public void CheckAyondoAccount(User user)
        {
            if (string.IsNullOrEmpty(user.AyondoUsername))
            {
                CFDGlobal.LogWarning("No Ayondo Account. userId: " + user.Id);

                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                    __(TransKey.NO_AYONDO_ACCOUNT)));
            }
        }
    }
}