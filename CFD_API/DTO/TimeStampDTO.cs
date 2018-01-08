using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class TimeStampDTO
    {
        public long timeStamp;
        public int nonce;
        public string captchaImg { get; set; }
    }
}