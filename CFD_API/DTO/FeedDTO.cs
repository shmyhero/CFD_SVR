using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class FeedDTO
    {
        public int? id { get; set; }
        public DateTime time { get; set; }
        public string type { get; set; }

        public UserBaseDTO user { get; set; }
        public bool isRankedUser { get; set; }

        public string status { get; set; }

        public SecurityBaseDTO security { get; set; }
        public PositionBaseDTO position { get; set; }
    }
}