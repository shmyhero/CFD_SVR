using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;

namespace CFD_API.DTO
{
    public class ResultDTO
    {
        public ResultDTO(bool success)
        {
            this.success = success;
        }

        public ResultDTO()
        {
        }

        public bool success { get; set; }
        public string message { get; set; }
        public JToken error { get; set; }
    }

    public class VersionDTO
    {
        public int iOSLatestInt { get; set; }
        
        public string iOSLatestStr { get; set; }

        public int androidLatestInt { get; set; }

        public string androidLatestStr { get; set; }

        public int iOSMinInt { get; set; }
        public int androidMinInt { get; set; }
        public int iOSPkgSize { get; set; }
        public int androidPkgSize { get; set; }
        public string iOSAppUrl { get; set; }
    }

    public class VersionIOSDTO
    {
        public int iOSLatestInt { get; set; }
        public string iOSLatestStr { get; set; }
        public int iOSMinInt { get; set; }
        public int iOSPkgSize { get; set; }
        public string iOSAppUrl { get; set; }
    }

    public class VersionAndroidDTO
    {
        public int androidLatestInt { get; set; }
        public string androidLatestStr { get; set; }
        public int androidMinInt { get; set; }
        public int androidPkgSize { get; set; }
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

    public class SMSDTO
    {
        public string message { get; set; }
        public string mobile { get; set; }

    }



    //ayondo callback API

    public class LifecycleCallbackFormDTO
    {
        public string Guid { get; set; }
        public string Status { get; set; }
        
        public string status { get; set; }
        public long accountNumber { get; set; }
    }

    public class LifecycleCallbackDTO
    {
        //public string Message { get; set; }
        //public string DeveloperMessage { get; set; }
        //public string ErrorCode { get; set; }
    }

    public class BankDTO
    {
        public string cname { get; set; }
        public string logo { get; set; }
    }

    public class AreaDTO
    {
        public int Id { get; set; }

        public int ParentId { get; set; }

        public string Name { get; set; }

        public string ShortName { get; set; }
    }

    public class ReferDTO
    {
        public string picUrl { get; set; }
        public string nickName { get; set; }
        public decimal amount { get; set; }
    }
}