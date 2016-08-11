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
}