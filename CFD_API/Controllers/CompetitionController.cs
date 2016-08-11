using System.Linq;
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
    }
}