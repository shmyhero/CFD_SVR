using AutoMapper;
using CFD_API.Caching;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Service;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/quiz")]
    public class QuizController : CFDController
    {
        public QuizController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }

        #region 管理后台使用的接口
        [HttpPost]
        [Route("admin")]
        [AdminAuth]
        public ResultDTO CreateQuiz(QuizDTO form)
        {
            var result = VerifyQuiz(form);
            if(!result.success)
            {
                return result;
            }

            form.OpenAt = form.OpenAt ?? DateTime.Now.Date.AddDays(1);
            form.ClosedAt = form.ClosedAt ?? DateTime.Now.Date.AddDays(2).AddSeconds(-1);

            DateTime openTime = form.OpenAt.Value;
            DateTime closeTime = form.ClosedAt.Value;

            //判断当天是否已经有竞猜
            bool hasQuiz = db.Quizzes.Any(q => q.OpenAt.HasValue && q.OpenAt.Value > openTime && q.OpenAt.Value < closeTime && q.ExpiredAt == SqlDateTime.MaxValue.Value);
            if(hasQuiz)
            {
                return new ResultDTO(false) { message = "该天已经有竞猜活动" };
            }

            var cache = WebCache.GetInstance(true);
            var prod = cache.ProdDefs.FirstOrDefault(p => p.Id == form.ProdID);
            if(prod == null)
            {
                return new ResultDTO(false) { message = "产品编号错误" };
            }

            

            var quiz = new Quiz() { ProdID = form.ProdID, ProdName = prod.Name, OpenAt = form.OpenAt, ClosedAt = form.ClosedAt, CreatedAt = DateTime.Now, ExpiredAt = SqlDateTime.MaxValue.Value };
            db.Quizzes.Add(quiz);
            db.SaveChanges();
            return new ResultDTO(true);
        }

        [HttpPut]
        [Route("admin")]
        [AdminAuth]
        public ResultDTO UpdateQuiz(QuizDTO form)
        {
            if (form.ProdID == 0)
            {
                return new ResultDTO(false) { message = "缺少竞猜活动ID" };
            }

            form.OpenAt = form.OpenAt ?? DateTime.Now.Date.AddDays(1);
            form.ClosedAt = form.ClosedAt ?? DateTime.Now.Date.AddDays(2).AddSeconds(-1);

            var result = VerifyQuiz(form);
            if (!result.success)
            {
                return result;
            }

            var quiz = db.Quizzes.FirstOrDefault(q => q.ID == form.ID && q.ExpiredAt == SqlDateTime.MaxValue.Value);
            if(quiz == null)
            {
                return new ResultDTO(false) { message = "未找到对应的竞猜活动" };
            }

            if(quiz.OpenAt < DateTime.Now)
            {
                return new ResultDTO(false) { message = "活动已经开始，不能修改" };
            }

            var cache = WebCache.GetInstance(true);
            var prod = cache.ProdDefs.FirstOrDefault(p => p.Id == form.ProdID);
            if (prod == null)
            {
                return new ResultDTO(false) { message = "产品编号错误" };
            }

            quiz.ProdID = form.ProdID;
            quiz.ProdName = prod.Name;
            quiz.OpenAt = form.OpenAt;
            quiz.ClosedAt = form.ClosedAt;

            db.SaveChanges();
            return new ResultDTO(true);

        }

        private ResultDTO VerifyQuiz(QuizDTO form)
        {
            if (form.ProdID == 0)
            {
                return new ResultDTO(false) { message = "缺少产品ID" };
            }

            if (form.OpenAt < DateTime.Now)
            {
                return new ResultDTO(false) { message = "竞猜开始时间不能小于当前时间" };
            }

            if (form.ClosedAt <= form.OpenAt.Value.Date)
            {
                return new ResultDTO(false) { message = "竞猜结束时间必须大于开始时间" };
            }

            if (form.ClosedAt.Value.Date != form.OpenAt.Value.Date)
            {
                return new ResultDTO(false) { message = "竞猜开始时间和结束时间必须在同一天内" };
            }

            return new ResultDTO(true);
        }

        [HttpDelete]
        [Route("admin/{qid}")]
        [AdminAuth]
        public ResultDTO DeleteQuiz(int qid)
        {
            var quiz = db.Quizzes.FirstOrDefault(q => q.ID == qid);
            if(quiz != null)
            {
                quiz.ExpiredAt = DateTime.Now;
            }
            db.SaveChanges();

            return new ResultDTO(true);
        }

        [HttpGet]
        [Route("admin/{qid}")]
        [AdminAuth]
        public QuizDTO GetQuizByID(int qid)
        {
            var quiz = db.Quizzes.FirstOrDefault(q => q.ID == qid && q.ExpiredAt == SqlDateTime.MaxValue.Value);
            QuizDTO dto = new QuizDTO();
            if(quiz != null)
            {
               var quizBets = db.QuizBets.Where(q => q.ID == qid).ToList();
                dto.ID = quiz.ID;
                dto.ProdName = quiz.ProdName;
                dto.OpenAt = quiz.OpenAt;
                dto.ClosedAt = quiz.ClosedAt;

                var longBets = quizBets.Where(q => q.BetDirection == "long");
                if(longBets != null)
                {
                    dto.LongAmount = longBets.Sum(q => q.BetAmount).Value;
                    dto.LongPersons = longBets.Count();
                }
                var shortBets = quizBets.Where(q => q.BetDirection == "short");
                if (longBets != null)
                {
                    dto.ShortAmount = shortBets.Sum(q => q.BetAmount).Value;
                    dto.ShortPersons = shortBets.Count();
                }
            }

            return dto;
        }

        /// <summary>
        /// 返回前N条
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("admin/all")]
        [AdminAuth]
        public List<QuizDTO> GetTopN()
        {
            int max = 30;
            List<QuizDTO> result = null;
            //get top N quizzes
            var quizzes = db.Quizzes.Where(item => item.ExpiredAt.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.ID).Take(max).ToList();
            if(quizzes != null)
            {
                result = quizzes.Select(q => {
                    return new QuizDTO() {
                         ID = q.ID,
                         ProdID =q.ProdID,
                         ProdName = q.ProdName,
                          ClosedAt = q.ClosedAt,
                          OpenAt = q.OpenAt,
                    };
                }).ToList();
            }

            return result;
        }

        [HttpGet]
        [Route("admin/next/{qid}")]
        [AdminAuth]
        public List<QuizDTO> NextN(int qid)
        {
            int max = 30;
            List<QuizDTO> result = null;
            //get top N quizzes
            var quizzes = db.Quizzes.Where(item => item.ID > qid && item.ExpiredAt.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.ID).Take(max).ToList();
            if (quizzes != null)
            {
                result = quizzes.Select(q => {
                    return new QuizDTO()
                    {
                        ID = q.ID,
                        ProdID = q.ProdID,
                        ProdName = q.ProdName,
                        ClosedAt = q.ClosedAt,
                        OpenAt = q.OpenAt,
                    };
                }).ToList();
            }

            return result;
        }

        #endregion

        #region 前台页面使用的接口
        /// <summary>
        /// 获取用户当天的竞猜活动信息，包括可用交易金、投注金额、方向
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{userID}/today")]
        public QuizBetDTO GetTodayBet(int userID)
        {
            QuizBetDTO dto = null;
            var todayQuiz = db.Quizzes.FirstOrDefault(q => q.OpenAt < DateTime.Now && q.ClosedAt > DateTime.Now && q.ExpiredAt == SqlDateTime.MaxValue.Value);

            if(todayQuiz == null)
            {
                return dto;
            }

            var quizBets = db.QuizBets.Where(q => q.ID == todayQuiz.ID).ToList();
            dto.ProdName = todayQuiz.ProdName;
            dto.OpenAt = todayQuiz.OpenAt;
            dto.ClosedAt = todayQuiz.ClosedAt;

            var longBets = quizBets.Where(q => q.BetDirection == "long");
            if (longBets != null)
            {
                dto.LongAmount = longBets.Sum(q => q.BetAmount).Value;
                dto.LongPersons = longBets.Count();
            }
            var shortBets = quizBets.Where(q => q.BetDirection == "short");
            if (longBets != null)
            {
                dto.ShortAmount = shortBets.Sum(q => q.BetAmount).Value;
                dto.ShortPersons = shortBets.Count();
            }

            RewardService service = new RewardService(db);
            var totalReward = service.GetTotalReward(userID);
            var paid = db.RewardTransfers.Where(o => o.UserID == userID).Select(o => o.Amount).DefaultIfEmpty(0).Sum();
            dto.AvailableBonus = totalReward.GetTotal() - paid;

            var myBet = db.QuizBets.FirstOrDefault(q => q.ID == todayQuiz.ID && q.UserID == userID);
            if(myBet!=null)
            {
                dto.BetAmount = myBet.BetAmount?? 0;
                dto.BetDirection = myBet.BetDirection;
            }
            

            return dto;
        }

        #endregion
    }
}
