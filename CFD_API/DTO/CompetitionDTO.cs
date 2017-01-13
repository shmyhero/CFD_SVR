using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class CompetitionSignUpDTO
    {
        public int competitionId { get; set; }
        public int userId { get; set; }
        public string phone { get; set; }
    }

    public class CompetitionResultDTO
    {
        public int? userId { get; set; }
        public string nickname { get; set; }
        public string picUrl { get; set; }
        public int? positionCount { get; set; }
        public decimal? invest { get; set; }
        public decimal? pl { get; set; }
        public int? rank { get; set; }
    }

    public class CompetitionUserDTO
    {
        public int userId { get; set; }
        public string userType { get; set; }
        public bool isSignedUp { get; set; }
    }

    public class CompetitionUserPositionDTO
    {
        public string SecurityName { get; set; }
        public decimal invest { get; set; }
        public decimal pl { get; set; }
        public DateTime date { get; set; }
    }
}