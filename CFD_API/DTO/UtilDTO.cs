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
    public class BannerDTO
    {
        public int id { get; set; }
        public string imgUrl { get; set; }
        public string url { get; set; }
    }
    public class FeedbackFormDTO
    {
        public string phone { get; set; }
        public string text { get; set; }
    }
}