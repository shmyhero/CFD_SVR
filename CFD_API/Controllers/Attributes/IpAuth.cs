using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace CFD_API.Controllers.Attributes
{
    public class IPAuth : AuthorizationFilterAttribute
    {
        public override void OnAuthorization(HttpActionContext actionContext)
        {
            string ip = null;
            if (actionContext.Request.Properties.ContainsKey("MS_HttpContext"))
            {
                var requestBase = ((HttpContextWrapper)actionContext.Request.Properties["MS_HttpContext"]).Request;
                ip = requestBase.UserHostAddress;


                string[] allowedIPs = new string[] { "::1", "101.231.88.242", "42.159.235.236", "10.0.0.22" };

                if(!allowedIPs.Contains(ip))
                    actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);
            }

            base.OnAuthorization(actionContext);
        }
    }
}