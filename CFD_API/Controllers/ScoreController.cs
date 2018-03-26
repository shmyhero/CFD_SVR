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

using EntityFramework.Extensions;

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
                    case ScoreSource.Reward: dto.reward += shg.Score; break;
                }
            });

            dto.total = dto.share + dto.liveOrder + dto.like + dto.reward;

            int usedScore = 0;
            var sch = db.ScoreConsumptionHistorys.Where(s => s.UserID == UserId).ToList();
            if(sch.Count() > 0)
            {
                usedScore = sch.Sum(s => s.Score).Value;
            }

            var trendRewards = db.TrendRewardHistorys.Where(t => t.RewardUserID == UserId).ToList();
            int trendRewardScore = 0;
            if (trendRewards.Count() > 0)
            {
                trendRewardScore = trendRewards.Sum(s => s.Amount);
            }

            //to-do 扣除已经使用的积分
            dto.remaining = dto.total - usedScore - trendRewardScore;

            return dto;
        }

        /// <summary>
        /// 返回数组形式的积分列表
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("v2")]
        [BasicAuth]
        public ScoreDTOV2 GetScoreV2()
        {           
            int scoreLiveOrder = 0;
            int scoreShare = 0;
            int scoreLike = 0;
            int scoreReward = 0;

            var scoreHistorys = (from sh in db.ScoreHistorys
                                 where sh.UserID == UserId
                                 group sh by sh.Source into shg
                                 select new
                                 {
                                     Source = shg.Key,
                                     Score = shg.Sum(sh => sh.Score)
                                 }).ToList();

            scoreHistorys.ForEach(shg => {
                switch (shg.Source)
                {
                    case ScoreSource.AppShare: scoreShare += shg.Score; break;
                    case ScoreSource.WechatCircle: scoreShare += shg.Score; break;
                    case ScoreSource.WechatFriend: scoreShare += shg.Score; break;
                    case ScoreSource.Like: scoreLike += shg.Score; break;
                    case ScoreSource.Liked: scoreLike += shg.Score; break;
                    case ScoreSource.LiveOrder: scoreLiveOrder += shg.Score; break;
                    case ScoreSource.Reward: scoreReward += shg.Score; break;
                }
            });

            List<NameScorePair> result = new List<NameScorePair>();
            //result.Add(new NameScorePair() { name = "实盘下单积分", score = scoreLiveOrder });
            //result.Add(new NameScorePair() { name = "卡片分享积分", score = scoreShare });
            //result.Add(new NameScorePair() { name = "卡片点赞积分", score = scoreLike });
            //result.Add(new NameScorePair() { name = "获得打赏积分", score = scoreReward });
            result.Add(new NameScorePair() { name = Resources.Resource.ScoreLiveOrder, score = scoreLiveOrder });
            result.Add(new NameScorePair() { name = Resources.Resource.ScoreShare, score = scoreShare });
            result.Add(new NameScorePair() { name = Resources.Resource.ScoreLike, score = scoreLike });
            result.Add(new NameScorePair() { name = Resources.Resource.ScoreReward, score = scoreReward });

            ScoreDTOV2 dto = new ScoreDTOV2();
            dto.total = scoreShare + scoreLike + scoreLiveOrder + scoreReward;
            dto.Items = result;

            //抽奖用掉的积分
            int usedScore = 0;
            var sch = db.ScoreConsumptionHistorys.Where(s => s.UserID == UserId).ToList();
            if (sch.Count() > 0)
            {
                usedScore = sch.Sum(s => s.Score).Value;
            }
            //打赏用掉的积分
            var trendRewards = db.TrendRewardHistorys.Where(t => t.RewardUserID == UserId).ToList();
            int trendRewardScore = 0;
            if (trendRewards.Count() > 0)
            {
                trendRewardScore = trendRewards.Sum(s => s.Amount);
            }
            
            //to-do 扣除已经使用的积分
            dto.remaining = dto.total - usedScore - trendRewardScore;
            
            return dto;
        }

        /// <summary>
        /// 抽奖
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("draw/{userID}")]
        [AdminAuth]
        public int Draw(int userID)
        {
            if (userID == 0)
                return 0;
                 
            //一次兑奖消费100
            int score = 100;

            var prize = db.ScorePrizeLists.OrderBy(sp => sp.ID).FirstOrDefault(sp => !sp.ClaimedAt.HasValue);
            if(prize == null) //全部奖品都被领取了一遍,则重置
            {
                db.ScorePrizeLists.Update(sp => new ScorePrizeList { ClaimedAt = null });
                prize = db.ScorePrizeLists.OrderBy(sp => sp.ID).FirstOrDefault(sp => !sp.ClaimedAt.HasValue);
            }

            prize.ClaimedAt = DateTime.UtcNow;

            db.ScoreConsumptionHistorys.Add(new ScoreConsumptionHistory()
            {
                UserID = userID,
                PrizeID = prize.PrizeID,
                PrizeName = prize.PrizeName,
                Score = score,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            return prize.PrizeID;
        }

        [HttpGet]
        [Route("draw/{userID}/count")]
        [AdminAuth]
        /// <summary>
        /// 获取剩余抽奖次数
        /// 1.3.6将逻辑修改为：
        /// 累计积分达到 1000、3000、5000、7000、9000、11000…依此类推
        /// ⽤户拥有 1 次抽奖机会，抽奖机会可以累加，抽奖 1 次消耗 100 积分
        /// </summary>
        /// <returns></returns>
        public int GetDrawCount(int userID)
        {
            if (userID == 0)
                return 0;

            var scoreHistoryList = db.ScoreHistorys.Where(s => s.UserID == userID).ToList();

            if(scoreHistoryList.Count() == 0)
            {
                return 0;
            }

            int totalScores = scoreHistoryList.Sum(s => s.Score);

            if(totalScores < 1000)
            {
                return 0;
            }

            ////积分在1000到2000间有一次抽奖机会，且只有一次
            //if(totalScores>= 1000 && totalScores<2000 && !db.ScoreConsumptionHistorys.Any(sch=>sch.UserID== userID))
            //{
            //    return 1;
            //}

            //if(totalScores >= 2000) //20002以上最多两次机会
            //{
            //    int count = db.ScoreConsumptionHistorys.Count(sch => sch.UserID == userID);

            //    return 2 - count > 0 ? (2 - count) : 0;
            //}

            //return 0;

            int totalCount = ((totalScores - 1000) / 2000) + 1; 
            int usedCount = db.ScoreConsumptionHistorys.Count(sch => sch.UserID == userID);

            int remainCount = totalCount - usedCount;

            return remainCount > 0 ? remainCount : 0;
        }

        [HttpGet]
        [Route("delivery/{userID}")]
        [AdminAuth]
        public ResultDTO UpdateDelivery(int userID, string phone = "", string address = "")
        {
            if (userID == 0)
                return new ResultDTO() { success = false };

            var user = db.Users.FirstOrDefault(u => u.Id == userID);
            if (user != null)
            {
                user.DeliveryAddress = address;
                user.DeliveryPhone = phone;
                db.SaveChanges();
            }

            return new ResultDTO() { success = true };
        }


        [HttpGet]
        [Route("prize/history")]
        [AdminAuth]
        public List<PrizeDTO> GetPrizeClaimList()
        {
            var historys = (from s in db.ScoreConsumptionHistorys
                          join u in db.Users on s.UserID equals u.Id
                          orderby s.CreatedAt descending
                          select new PrizeDTO()
                          {
                               nickName = u.Nickname,
                                picUrl = u.PicUrl,
                                 prizeName = s.PrizeName
                          }).Take(6).ToList();

            return historys;
        }
    }
}
