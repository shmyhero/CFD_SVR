using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using AutoMapper;
using CFD_API.Caching;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using CFD_COMMON.Localization;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/score")]
    public class ScoreController : CFDController
    {
        public ScoreController(CFDEntities db, IMapper mapper) : base(db, mapper)
        { 
        }

        [HttpGet]
        [Route("")]
        [BasicAuth]
        public ScoreDTO GetScore()
        {
            ScoreDTO dto = new ScoreDTO();

            var scoreHistorys = (from sh in db.ScoreHistorys
                                 where sh.UserID == UserId
                                 group sh by sh.Source into shg
                                 select new
                                 {
                                     Source = shg.Key,
                                     Score = shg.Sum(sh => sh.Score)
                                 }).ToList();

            scoreHistorys.ForEach( shg => {
                switch(shg.Source)
                {
                    case ScoreSource.AppShare: dto.share += shg.Score; break;
                    case ScoreSource.WechatCircle: dto.share += shg.Score; break;
                    case ScoreSource.WechatFriend: dto.share += shg.Score; break;
                    case ScoreSource.Like: dto.like += shg.Score; break;
                    case ScoreSource.Liked: dto.like += shg.Score; break;
                    case ScoreSource.LiveOrder: dto.liveOrder += shg.Score; break;
                }
            });

            dto.total = dto.share + dto.liveOrder + dto.like;

            //to-do 扣除已经使用的积分
            dto.remaining = dto.total;

            return dto;
        }
    }
}
