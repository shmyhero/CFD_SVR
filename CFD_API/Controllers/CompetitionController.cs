using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
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
        public CompetitionController(CFDEntities db, IMapper mapper, IRedisClient redisClient)
            : base(db, mapper, redisClient)
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
            var chinaNow = DateTimes.GetChinaDateTimeNow();
            var chinaYesterday = chinaNow.AddDays(-1).Date;

            if (chinaYesterday.DayOfWeek == DayOfWeek.Sunday)
                chinaYesterday = chinaYesterday.AddDays(-2);

            if (chinaYesterday.DayOfWeek == DayOfWeek.Saturday)
                chinaYesterday = chinaYesterday.AddDays(-1);

            var competitionResults =
                db.CompetitionResults.Where(o => o.CompetitionId == id && o.Date == chinaYesterday)
                    .OrderBy(o => o.Rank)
                    .Take(10)
                    .ToList()
                    .Select(o => Mapper.Map<CompetitionResultDTO>(o))
                    .ToList();

            return competitionResults;
        }

        [HttpGet]
        [Route("{id}/user/{userId}/rank")]
        public CompetitionResultDTO GetUserRank(int id, int userId)
        {
            var chinaNow = DateTimes.GetChinaDateTimeNow();
            var chinaYesterday = chinaNow.AddDays(-1).Date;

            if (chinaYesterday.DayOfWeek == DayOfWeek.Sunday)
                chinaYesterday = chinaYesterday.AddDays(-2);

            if (chinaYesterday.DayOfWeek == DayOfWeek.Saturday)
                chinaYesterday = chinaYesterday.AddDays(-1);

            var competitionResult =
                db.CompetitionResults.FirstOrDefault(
                    o => o.CompetitionId == id && o.Date == chinaYesterday && o.UserId == userId);

            if (competitionResult == null)
                return new CompetitionResultDTO() {};
            else
                return Mapper.Map<CompetitionResultDTO>(competitionResult);
        }

        [HttpGet]
        [Route("{id}/user/{userId}/position")]
        public List<CompetitionUserPositionDTO> GetUserPositions(int id, int userId)
        {
            var chinaNow = DateTimes.GetChinaDateTimeNow();
            var chinaYesterday = chinaNow.AddDays(-1).Date;

            if (chinaYesterday.DayOfWeek == DayOfWeek.Sunday)
                chinaYesterday = chinaYesterday.AddDays(-2);

            if (chinaYesterday.DayOfWeek == DayOfWeek.Saturday)
                chinaYesterday = chinaYesterday.AddDays(-1);

            var positions =
                db.CompetitionUserPositions.Where(o => o.CompetitionId == id && o.Date == chinaYesterday && o.UserId == userId).ToList();

            return positions.Select(o => Mapper.Map<CompetitionUserPositionDTO>(o)).ToList();
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