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

            //周五就加两天，确保周五、六、日只有一个竞猜活动
            if(form.OpenAt.Value.DayOfWeek == DayOfWeek.Friday)
            {
                form.ClosedAt = form.ClosedAt.Value.AddDays(2);
            }

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
            //交易日是在竞猜结束后的一天
            quiz.TradeDay = closeTime.Date.AddDays(1); 
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
            //周五就加两天，确保周五、六、日只有一个竞猜活动
            if (form.OpenAt.Value.DayOfWeek == DayOfWeek.Friday)
            {
                form.ClosedAt = form.ClosedAt.Value.AddDays(2);
            }

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

            DateTime openTime = form.OpenAt.Value;
            DateTime closeTime = form.ClosedAt.Value;

            //判断当天是否已经有竞猜
            bool hasQuiz = db.Quizzes.Any(q => q.OpenAt.HasValue && q.OpenAt.Value == openTime && q.ID != form.ID && q.ExpiredAt == SqlDateTime.MaxValue.Value);
            if (hasQuiz)
            {
                return new ResultDTO(false) { message = "该天已经有竞猜活动" };
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
            quiz.TradeDay = closeTime.Date.AddDays(1);

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

            //if (form.ClosedAt.Value.Date != form.OpenAt.Value.Date)
            //{
            //    return new ResultDTO(false) { message = "竞猜开始时间和结束时间必须在同一天内" };
            //}

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
                dto.TradeDay = quiz.TradeDay;
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
            var quizzes = db.Quizzes.Where(item => item.ExpiredAt.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.OpenAt).Take(max).ToList();
            if (quizzes != null)
            {
                result = quizzes.Select(q =>
                {
                    return new QuizDTO()
                    {
                        ID = q.ID,
                        ProdID = q.ProdID,
                        ProdName = q.ProdName,
                        ClosedAt = q.ClosedAt,
                        OpenAt = q.OpenAt,
                        TradeDay = q.TradeDay,
                        Result = q.Result
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
            var quizzes = db.Quizzes.Where(item => item.ID > qid && item.ExpiredAt.Value == SqlDateTime.MaxValue.Value).OrderByDescending(o => o.OpenAt).Take(max).ToList();
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
                        TradeDay = q.TradeDay,
                        Result = q.Result
                    };
                }).ToList();
            }

            return result;
        }

        /// <summary>
        /// 上一个竞猜的情况
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("admin/last")]
        [AdminAuth]
        public QuizDTO Last()
        {
            var dto = new QuizDTO();
            DateTime today = DateTime.Now.Date;
            var lastQuiz = db.Quizzes.OrderByDescending(q => q.OpenAt).FirstOrDefault(q => q.TradeDay < today);
            if(lastQuiz != null)
            {
                dto.ID = lastQuiz.ID;
                dto.ProdID = lastQuiz.ProdID;
                dto.ProdName = lastQuiz.ProdName;
                dto.ClosedAt = lastQuiz.ClosedAt;
                dto.OpenAt = lastQuiz.OpenAt;
                dto.TradeDay = lastQuiz.TradeDay;
                dto.Result = lastQuiz.Result;
            }

            return dto;
        }

        #endregion

        #region 前台页面使用的接口
        /// <summary>
        /// 获取用户当天的竞猜活动信息，包括可用交易金、投注金额、方向
        /// 这里的当天是指最近未发生的一个竞猜活动，比如今天是周一，返回的就是周二的竞猜
        /// 如果今天是周五，返回的就是下周一的竞猜
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{userID}/next")]
        [AdminAuth]
        public QuizBetDTO GetNextQuizBet(int userID)
        {
            QuizBetDTO dto = new QuizBetDTO();
            var nextQuiz = db.Quizzes.OrderBy(q=>q.OpenAt).FirstOrDefault(q => q.OpenAt > DateTime.Now && q.ExpiredAt == SqlDateTime.MaxValue.Value);

            if(nextQuiz == null)
            {
                return dto;
            }

            var quizBets = db.QuizBets.Where(q => q.ID == nextQuiz.ID).ToList();
            dto.ProdName = nextQuiz.ProdName;
            dto.OpenAt = nextQuiz.OpenAt;
            dto.ClosedAt = nextQuiz.ClosedAt;
            dto.TradeDay = nextQuiz.TradeDay;
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

            var myBet = db.QuizBets.FirstOrDefault(q => q.ID == nextQuiz.ID && q.UserID == userID);
            if(myBet!=null)
            {
                dto.BetAmount = myBet.BetAmount?? 0;
                dto.BetDirection = myBet.BetDirection;
            }
            

            return dto;
        }

        /// <summary>
        /// 获取用户上一个竞猜活动的信息，包括可用交易金、投注金额、方向、盈亏结果
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{userID}/last")]
        [AdminAuth]
        public QuizBetDTO GetLastBet(int userID)
        {
            QuizBetDTO dto = new QuizBetDTO();
            //var lastQuiz = db.Quizzes.OrderByDescending(q=>q.ID).FirstOrDefault(q => q.OpenAt < DateTime.Now &&  && q.ExpiredAt == SqlDateTime.MaxValue.Value);
            //找出该用户参与过的最后一个竞猜活动
            var lastQuizBet = (from b in db.QuizBets
                            join q in db.Quizzes on b.QuizID equals q.ID
                            where q.OpenAt < DateTime.Now && q.ExpiredAt == SqlDateTime.MaxValue.Value && b.UserID == userID
                            orderby b.QuizID descending
                            select new QuizBetDTO()
                            {
                                ID = b.ID,
                                ClosedAt = q.ClosedAt,
                                TradeDay = q.TradeDay,
                                Result = q.Result,
                                OpenAt = q.OpenAt,
                                ProdID = q.ProdID,
                                ProdName = q.ProdName,
                                BetAmount = b.BetAmount,
                                BetDirection = b.BetDirection,
                                PL = b.PL,
                                IsViewed = b.IsPLViewed?? false
                            }).FirstOrDefault();


            if (lastQuizBet == null)
            {
                return dto;
            }

            dto.OpenAt = lastQuizBet.OpenAt;
            dto.ProdID = lastQuizBet.ProdID;
            dto.TradeDay = lastQuizBet.TradeDay;
            dto.Result = lastQuizBet.Result;
            dto.BetAmount = lastQuizBet.BetAmount ?? 0;
            dto.BetDirection = lastQuizBet.BetDirection;
            dto.PL = lastQuizBet.PL ?? 0;
            dto.IsViewed = lastQuizBet.IsViewed;

            if(!lastQuizBet.IsViewed.HasValue || lastQuizBet.IsViewed == false)
            {
                var quizBet = db.QuizBets.FirstOrDefault(q => q.ID == lastQuizBet.ID);
                if(quizBet!=null)
                {
                    quizBet.IsPLViewed = true;
                }
            }
             
            db.SaveChanges();

            return dto;
        }

        /// <summary>
        /// 对下一个竞猜活动下注
        /// </summary>
        [HttpPost]
        [Route("{userID}/bet")]
        public ResultDTO BetOnNext(int userID, QuizBetDTO form)
        {
            var nextQuiz = db.Quizzes.OrderBy(q => q.OpenAt).FirstOrDefault(q => q.OpenAt > DateTime.Now && q.ExpiredAt == SqlDateTime.MaxValue.Value);

            if (nextQuiz == null)
            {
                return new ResultDTO(false) { message = "竞猜活动不存在" };
            }

            var quizBet = new QuizBet()
            {
                BetAmount = form.BetAmount,
                BetDirection = form.BetDirection,
                CreatedAt = DateTime.Now,
                QuizID = form.QID?? 0,
                UserID = userID
            };

            db.QuizBets.Add(quizBet);
            db.SaveChanges();

            return new ResultDTO(true);
        }

        [HttpGet]
        [Route("{userID}/all")]
        public List<QuizBetDTO> GetTopN(int userID)
        {
            int max = 20;
            var quizBets = (from b in db.QuizBets
                               join q in db.Quizzes on b.QuizID equals q.ID
                               where q.ExpiredAt == SqlDateTime.MaxValue.Value && b.UserID == userID
                               orderby q.OpenAt descending
                               select new QuizBetDTO()
                               {
                                   ID = b.ID,
                                   QID = q.ID,
                                   ClosedAt = q.ClosedAt,
                                   TradeDay = q.TradeDay,
                                   Result = q.Result,
                                   OpenAt = q.OpenAt,
                                   ProdID = q.ProdID,
                                   ProdName = q.ProdName,
                                   BetAmount = b.BetAmount,
                                   BetDirection = b.BetDirection,
                                   PL = b.PL,
                                   IsViewed = b.IsPLViewed ?? false
                               }).Take(max).ToList();

            return quizBets;
        }

        [HttpGet]
        [Route("{userID}/all/{bid}")]
        public List<QuizBetDTO> GetNextN(int userID, int bid)
        {
            int max = 20;
            var quizBets = (from b in db.QuizBets
                            join q in db.Quizzes on b.QuizID equals q.ID
                            where q.ExpiredAt == SqlDateTime.MaxValue.Value && b.UserID == userID && b.ID < bid
                            orderby q.OpenAt descending
                            select new QuizBetDTO()
                            {
                                ID = b.ID,
                                QID = q.ID,
                                ClosedAt = q.ClosedAt,
                                TradeDay = q.TradeDay,
                                Result = q.Result,
                                OpenAt = q.OpenAt,
                                ProdID = q.ProdID,
                                ProdName = q.ProdName,
                                BetAmount = b.BetAmount,
                                BetDirection = b.BetDirection,
                                PL = b.PL,
                                IsViewed = b.IsPLViewed ?? false
                            }).Take(max).ToList();

            return quizBets;
        }

        #endregion
    }
}
