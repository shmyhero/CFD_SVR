using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using CFD_COMMON.Utils;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/trend")]
    public class TrendController : CFDController
    {
        public TrendController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [HttpPost]
        [Route("")]
        [BasicAuth]
        public ResultDTO PostTrend(TrendDTO dto)
        {
            if(!string.IsNullOrEmpty(dto.message))
            {
                db.Trends.Add(new Trend() {
                    Message = dto.message,
                    UserID = this.UserId,
                     CreatedAt = DateTime.UtcNow,
                      ExpiredAt = SqlDateTime.MaxValue.Value,
                       Likes = 0,
                        RewardCount = 0,
                         TotalRewardedScore = 0
                });

                db.SaveChanges();
            }

            return new ResultDTO() { success = true, message="OK" };

        }

        [HttpGet]
        [Route("{id}")]
        [AdminAuth]
        public TrendDTO GetTrend(int id)
        {
            var trend = db.Trends.FirstOrDefault(t => t.ID == id);
            var dto = new TrendDTO()
            {
                createdAt = DateTimes.UtcToChinaTime(trend.CreatedAt),
                id = trend.ID,
                likes = trend.Likes,
                message = trend.Message,
                rewardCount = trend.RewardCount
            };

            return dto;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userID">动态的创建人ID</param>
        /// <param name="id">动态ID</param>
        /// <returns></returns>
        [HttpGet]
        [Route("{userId}/next/{id}")]
        [BasicAuth]
        public List<TrendDTOV2> NextTrend(int userID, int id)
        {
            List<TrendDTOV2> result = new List<TrendDTOV2>();
            int pageSize = 10;
            var trends = db.Trends.Where(t => t.UserID == userID && t.ID > id && t.ExpiredAt == SqlDateTime.MaxValue.Value).OrderByDescending(t => t.CreatedAt).Take(pageSize);
            var trendDTOs = trends.Select(t => new TrendDTO() {
                 createdAt = t.CreatedAt,
                  id = t.ID,
                   likes = t.Likes,
                     message = t.Message,
                      rewardCount = t.RewardCount
            }).ToList();
            trendDTOs.ForEach(t => {
                t.Liked = db.TrendLikeHistorys.Any(tl => tl.UserID == this.UserId && tl.TrendID == t.id);
            });

            trendDTOs.ForEach(t =>
            {
                result.Add(new TrendDTOV2()
                {
                    createdAt = DateTimes.UtcToChinaTime(t.createdAt.Value).ToString("yyyy.MM.dd HH:mm"),
                    id = t.id,
                    Liked = t.Liked,
                    likes = t.likes,
                    message = t.message,
                    rewardCount = t.rewardCount
                });
            });

            return result;
        }

        [HttpGet]
        [Route("like/{id}")]
        [BasicAuth]
        public ResultDTO LikeTrend(int id)
        {
            if(db.TrendLikeHistorys.Any(t=>t.UserID == this.UserId && t.TrendID == id))
            {
                return new ResultDTO() { success = false, message="不能重复点赞" };
            }

            var trend = db.Trends.FirstOrDefault(t => t.ID == id && t.ExpiredAt == SqlDateTime.MaxValue.Value);
            if (trend == null)
                return new ResultDTO() { success = false, message = "动态不存在" };
            trend.Likes++;

            db.TrendLikeHistorys.Add(new TrendLikeHistory() { TrendID = id, UserID = this.UserId, CreatedAt = DateTime.UtcNow, ExpiredAt= SqlDateTime.MaxValue.Value  });

            db.SaveChanges();

            return new ResultDTO() { success = true };
        }

        [HttpGet]
        [Route("reward/{id}")]
        [BasicAuth]
        public ResultDTO RewardTrend(int id)
        {
            int amount = 10;
            var trend = db.Trends.FirstOrDefault(t => t.ID == id && t.ExpiredAt == SqlDateTime.MaxValue.Value);
            if (trend == null)
                return new ResultDTO() { success = false, message = "动态不存在" };

            if(trend.UserID == this.UserId)
                return new ResultDTO() { success = false, message = "不能打赏自己" };

            trend.RewardCount++;
            trend.TotalRewardedScore += amount;

            db.TrendRewardHistorys.Add(new TrendRewardHistory() { TrendID = id, RewardUserID = this.UserId, Amount =  amount, CreatedAt = DateTime.UtcNow, ExpiredAt = SqlDateTime.MaxValue.Value});
            db.ScoreHistorys.Add(new ScoreHistory() { Source = ScoreSource.Reward, Score = amount, UserID = trend.UserID });
            db.SaveChanges();

            return new ResultDTO() { success = true };
        }
    }
}