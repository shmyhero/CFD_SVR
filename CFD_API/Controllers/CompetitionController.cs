using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using CFD_COMMON.Models.Context;
using ServiceStack.Redis;

namespace CFD_API.Controllers
{
    public class CompetitionController : CFDController
    {
        public CompetitionController(CFDEntities db, IMapper mapper, IRedisClient redisClient) : base(db, mapper, redisClient)
        {
        }


    }
}
