using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class ResultDTO
    {
        public bool success { get; set; }
        public string message { get; set; }
    }

    public class SimpleBannerDTO
    {
        public int id { get; set; }
        public string imgUrl { get; set; }
        public string url { get; set; }
    }

    public class BannerDTO
    {
        public int id { get; set; }
        public string imgUrl { get; set; }
        public string url { get; set; }
        public string Header { get; set; }

        public string Body { get; set; }

        public int? IsTop { get; set; }

        public DateTime? TopAt { get; set; }

        public string CreatedBy { get; set; }

        public DateTime? CreatedAt { get; set; }

        public string ExpiredBy { get; set; }

        public DateTime? Expiration { get; set; }
    }

    public class OperationUserDTO
    {
        public string name { get; set; }
        public string password { get; set; }
        public string Type { get; set; }
    }

    public class HeadlineDTO
    {
        public int id { get; set; }
        public string header { get; set; }
        public string body { get; set; }
        public DateTime? createdat { get; set; }
    }

    public class HeadlineGroupDTO
    {
        public string createdday { get; set; }
        public List<HeadlineDTO> headlines { get; set; }
    }

    public class FeedbackFormDTO
    {
        public string phone { get; set; }
        public string text { get; set; }
    }
    public class BannerDetailDTO
    {
        public string header { get; set; }
        public string body { get; set; }
        public string startdate { get; set; }
        public string enddate { get; set; }
    }
}