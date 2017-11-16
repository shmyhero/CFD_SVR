using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class ActivityChannelDTO
    {
        public int activityID { get; set; }
        public string activityName { get; set; }
        public int channelID { get; set; }
        public string channelName { get; set; }
        public int personCount { get; set; }
    }
}