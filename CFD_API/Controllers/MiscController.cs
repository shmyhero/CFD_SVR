using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using CFD_API.Controllers.Attributes;

namespace CFD_API.Controllers
{
    public class MiscController : ApiController
    {
        [HttpGet]
        [ActionName("version")]
        public HttpResponseMessage GetVersion()
        {
            //ApiGlobal.LogLine("");
            //string dbName = db.Database.Connection.Database;
            
            return Request.CreateResponse(
                HttpStatusCode.OK,

#if DEBUG
                "TH API STATUS: OK [build=DEBUG]" +
#else
                "TH API STATUS: OK [build=RELEASE]" +
#endif
                    " -- v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()
                    //+" -- DB=[" + dbName + "]" 
                    //+" -- top-table cabling: brought to you by The A-Team."
                );
        }

        [HttpGet]
        [ActionName("err")]
        public HttpResponseMessage TestErr()
        {
            //ApiGlobal.LogLine("about to throw test exception...");
            string s = null;
            string s2 = s.ToString();
            return Request.CreateResponse(HttpStatusCode.OK, s2);
        }

        [HttpGet]
        [ActionName("https")]
        [RequireHttps]
        public HttpResponseMessage TestHttps()
        {
            return Request.CreateResponse(HttpStatusCode.OK, "url scheme: " + Request.RequestUri.Scheme);
        }
    }
}