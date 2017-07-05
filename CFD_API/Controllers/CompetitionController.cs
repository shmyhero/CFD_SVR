using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;
using ServiceStack.Redis;

namespace CFD_API.Controllers
{
    [RoutePrefix("api/competition")]
    public class CompetitionController : CFDController
    {
        public CompetitionController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }

        [HttpPost]
        [Route("signUp")]
        public ResultDTO SignUp(CompetitionSignUpDTO form)
        {
            var competition = db.Competitions.FirstOrDefault(o => o.Id == form.competitionId);

            if (competition == null)
                return new ResultDTO() {success = false, message = "competition not existed"};

            var user = db.Users.FirstOrDefault(o => o.Id == form.userId);

            if (user == null)
                return new ResultDTO() {success = false, message = "user not existed"};

            var competitionUser = new CompetitionUser() {CompetitionId = form.competitionId, UserId = form.userId};

            if (user.Phone != null)
                competitionUser.Phone = user.Phone;
            else
            {
                if (!Phone.IsValidPhoneNumber(form.phone))
                    return new ResultDTO() {success = false, message = "invalid phone"};

                competitionUser.Phone = form.phone;
            }

            var firstOrDefault =
                db.CompetitionUsers.FirstOrDefault(o => o.CompetitionId == form.competitionId && o.UserId == form.userId);

            if (firstOrDefault != null)
                return new ResultDTO() {success = false, message = "already signed up"};

            db.CompetitionUsers.Add(competitionUser);
            db.SaveChanges();

            return new ResultDTO() {success = true};
        }

        [HttpGet]
        [Route("{id}/leaderboard")]
        public List<CompetitionResultDTO> GetLeaderboard(int id)
        {
            //var date = DateTimes.GetLastFinishedChinaWorkday();
            var date = db.CompetitionResults.OrderByDescending(o => o.Date).Select(o => o.Date).FirstOrDefault();

            //Top3尽量安排给实盘用户
            var liveUserResult = db.CompetitionResults.Include(o => o.User).Where(o => o.CompetitionId == id && o.Date == date && o.User.AyLiveAccountId.HasValue)
                    .OrderBy(o => o.Rank)
                    .Take(3)
                    .ToList()
                    .Select(o =>
                    {
                        var dto = Mapper.Map<CompetitionResultDTO>(o);
                        dto.nickname = o.User.Nickname;
                        dto.picUrl = o.User.PicUrl;
                        return dto;
                    })
                    .ToList();

            //其他参与竞赛用户的数量(排除了top3实盘用户之后，可能是实盘用户和模拟盘用户的混合)
            int restUserCount = 10 - liveUserResult.Count;
            //取其他竞赛用户的时候，要排除这些Top Live User
            var liveUserIDs = liveUserResult.Select(u => u.userId.Value).ToList();

            var restUserResult =
                db.CompetitionResults.Include(o => o.User).Where(o => o.CompetitionId == id && o.Date == date && !liveUserIDs.Contains(o.User.Id))
                    .OrderBy(o => o.Rank)
                    .Take(restUserCount)
                    .ToList()
                    .Select(o =>
                    {
                        var dto = Mapper.Map<CompetitionResultDTO>(o);
                        dto.nickname = o.User.Nickname;
                        dto.picUrl = o.User.PicUrl;
                        return dto;
                    })
                    .ToList();

            liveUserResult.AddRange(restUserResult);
            return liveUserResult;
        }

        //todo: for support/test
        [HttpGet]
        [Route("{id}/leaderboard/all")]
        [IPAuth]
        public dynamic GetLeaderboardAll(int id)
        {
            //var date = DateTimes.GetLastFinishedChinaWorkday();
            var date = db.CompetitionResults.OrderByDescending(o => o.Date).Select(o => o.Date).FirstOrDefault();

            var competitionResults =
                db.CompetitionResults.Where(o => o.CompetitionId == id && o.Date == date)
                    .OrderBy(o => o.Rank)
                    .ToList()
                    .Select(o => new {o.Date, o.UserId, o.Nickname, o.Phone, o.Rank, o.PositionCount, o.Invest, o.PL})
                    .ToList();

            return competitionResults;
        }

        [HttpGet]
        [Route("{id}/user/{userId}/rank")]
        public CompetitionResultDTO GetUserRank(int id, int userId)
        {
            //var date = DateTimes.GetLastFinishedChinaWorkday();
            var date = db.CompetitionResults.OrderByDescending(o => o.Date).Select(o => o.Date).FirstOrDefault();

            var competitionResult =
                db.CompetitionResults.Include(o=>o.User).FirstOrDefault(
                    o => o.CompetitionId == id && o.Date == date && o.UserId == userId);

            if (competitionResult == null)
            {
                var competitionUser = db.CompetitionUsers.Include(o=>o.User).FirstOrDefault(o => o.UserId == userId);

                if (competitionUser == null)
                    return new CompetitionResultDTO();
                else
                {
                    var dto = new CompetitionResultDTO();
                    dto.nickname = competitionUser.User.Nickname;
                    dto.picUrl = competitionUser.User.PicUrl;
                    return dto;
                }
            }
            else
            {
                var dto = Mapper.Map<CompetitionResultDTO>(competitionResult);
                dto.nickname = competitionResult.User.Nickname;
                dto.picUrl = competitionResult.User.PicUrl;
                return dto;
            }
        }

        [HttpGet]
        [Route("{id}/user/{userId}/position")]
        public List<CompetitionUserPositionDTO> GetUserPositions(int id, int userId)
        {
            //var date = DateTimes.GetLastFinishedChinaWorkday();
            var date = db.CompetitionResults.OrderByDescending(o => o.Date).Select(o => o.Date).FirstOrDefault();

            var positions =
                db.CompetitionUserPositions.Where(o => o.CompetitionId == id && o.Date == date && o.UserId == userId).ToList();

            return positions.Select(o => Mapper.Map<CompetitionUserPositionDTO>(o)).ToList();
        }

        //todo: for support/test
        [HttpGet]
        [Route("{id}/position")]
        [IPAuth]
        public dynamic GetPositionAll(int id)
        {
            //var date = DateTimes.GetLastFinishedChinaWorkday();
            var date = db.CompetitionResults.OrderByDescending(o => o.Date).Select(o => o.Date).FirstOrDefault();

            var cPositions =
                db.CompetitionUserPositions.Where(o => o.CompetitionId == id && o.Date == date)
                    .ToList();

            var posIds = cPositions.Select(o => o.PositionId).ToList();
            var positions = db.NewPositionHistories.Where(o => posIds.Contains(o.Id)).ToList();

            var result = cPositions.Select(o =>
            {
                var pos = positions.First(p => p.Id == o.PositionId);
                return new
                {
                    o.Date,
                    o.PositionId,
                    o.UserId,
                    o.SecurityId,
                    o.SecurityName,
                    o.Invest,
                    o.PL,
                    CreateTime = pos.CreateTime.Value.AddHours(8),
                    pos.Leverage,
                    Side = pos.LongQty.HasValue?1:0,
                };
            })
                .ToList();

            return result;
        }

        [HttpGet]
        [Route("{id}/user/{userId}")]
        public CompetitionUserDTO GetUser(int id, int userId)
        {
            var competitionUser = db.CompetitionUsers.FirstOrDefault(o => o.CompetitionId == id && o.UserId == userId);

            if (competitionUser == null)
            {
                var user = db.Users.FirstOrDefault(o => o.Id == userId);

                if (user == null)
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "no such user"));

                return new CompetitionUserDTO()
                {
                    isSignedUp = false,
                    userId = user.Id,
                    userType = user.Phone == null ? "wechat" : "phone",
                };
            }
            else
            {
                return new CompetitionUserDTO()
                {
                    isSignedUp = true,
                    userId = competitionUser.UserId,
                };
            }
        }
    }
}