using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace CFD_API.Controllers.Attributes
{
    public class RestrictByIp:ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            base.OnActionExecuting(actionContext);

            string ip = null;
            if (actionContext.Request.Properties.ContainsKey("MS_HttpContext"))
            {
                var requestBase = ((HttpContextWrapper)actionContext.Request.Properties["MS_HttpContext"]).Request;
                ip = requestBase.UserHostAddress;
            }
        }
    }
}