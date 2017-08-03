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

        /// <summary>
        /// 抽奖
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("draw")]
        [BasicAuth]
        public int Draw()
        {
            //奖品编号，顺时针从左上角开始1-8. 无人机是5，概率是1/1000，其他奖品概率相同
            //除无人机以外的奖品编号
            List<int> prizes = new List<int>();
            prizes.AddRange(new int[] { 1,2,3,4,6,7,8 });
            //一次兑奖消费100
            int score = 100;

            Random ran = new Random();
            //无人机是5，概率是1/1000
            if (ran.Next(1, 1000) == 5)
            {
                db.ScoreConsumptionHistorys.Add(new ScoreConsumptionHistory() {
                    UserID = UserId,
                    PrizeID = 5,
                    PrizeName = "无人机",
                    Score = score,
                    CreatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
                return 5;
            }

            //未抽中无人机，就从其他7个奖品中抽取
            int prizeIndex = ran.Next(1, 7);
            int prizeID = 0;
            string prizeName = string.Empty;
            switch(prizeIndex)
            {
                case 1: prizeID = 1; prizeName = "派克钢笔"; break;
                case 2: prizeID = 2; prizeName = "30元话费"; break;
                case 3: prizeID = 3; prizeName = "高级笔记本"; break;
                case 4: prizeID = 4; prizeName = "30元话费"; break;
                case 5: prizeID = 6; prizeName = "派克钢笔"; break;
                case 6: prizeID = 7; prizeName = "30元话费"; break;
                case 7: prizeID = 8; prizeName = "无线蓝牙耳机"; break;
            }

            db.ScoreConsumptionHistorys.Add(new ScoreConsumptionHistory()
            {
                UserID = UserId,
                PrizeID = prizeID,
                PrizeName = prizeName,
                Score = score,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            return prizeID;
        }

        [HttpGet]
        [Route("draw/count")]
        [BasicAuth]
        /// <summary>
        /// 获取剩余抽奖次数
        /// </summary>
        /// <returns></returns>
        public int GetDrawCount()
        {
            int totalScores = db.ScoreHistorys.Sum(s => s.Score);

            if(totalScores < 1000)
            {
                return 0;
            }

            //积分在1000到2000间有一次抽奖机会，且只有一次
            if(totalScores>= 1000 && totalScores<2000 && !db.ScoreConsumptionHistorys.Any(sch=>sch.UserID==UserId))
            {
                return 1;
            }

            if(totalScores >= 2000) //20002以上最多两次机会
            {
                int count = db.ScoreConsumptionHistorys.Count(sch => sch.UserID == UserId);

                return 2 - count > 0 ? (2 - count) : 0;
            }

            return 0;
        }

        [HttpGet]
        [Route("delivery")]
        [BasicAuth]
        public ResultDTO UpdateDelivery(string phone = "", string address = "")
        {
            var user = db.Users.FirstOrDefault(u => u.Id == UserId);
            if (user != null)
            {
                user.DeliveryAddress = address;
                user.DeliveryPhone = phone;
                db.SaveChanges();
            }

            return new ResultDTO() { success = true };
        }
    }
}
