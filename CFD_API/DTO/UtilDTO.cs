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
        public string header { get; set; }
        public string digest { get; set; }
    }

    public class BannerDTO
    {
        public int id { get; set; }
        public string imgUrl { get; set; }
        public string url { get; set; }
        public string header { get; set; }

        public string body { get; set; }

        public string digest { get; set; }

        public int? isTop { get; set; }

        public DateTime? topAt { get; set; }

        public string createdBy { get; set; }

        public DateTime? createdAt { get; set; }

        public string expiredBy { get; set; }

        public DateTime? expiration { get; set; }
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
        /// <summary>
        /// for request, it is base64 of image
        /// for response, it is url 
        /// </summary>
        public string image { get; set; }
        public int color { get; set; }
        public DateTime? createdAt { get; set; }
    }

    public class HeadlineGroupDTO
    {
        public string createdDay { get; set; }
        public List<HeadlineDTO> headlines { get; set; }
    }

    public class FeedbackFormDTO
    {
        public string phone { get; set; }
        public string text { get; set; }
    }

    public class FeedBackFormDTO_Pic
    {
        public int id { get; set; }
        public string phone { get; set; }
        public string text { get; set; }

        public List<string> photos { get; set; }
    }

    public class BannerDetailDTO
    {
        public string header { get; set; }
        public string body { get; set; }
        public string startdate { get; set; }
        public string enddate { get; set; }
    }
}