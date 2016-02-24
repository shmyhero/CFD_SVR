using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace CFD_API.Controllers.Attributes
{
    public class BasicAuth : AuthorizationFilterAttribute
    {
        // DO NOT DO THIS!!!
        // the attribute filters persist across request
        // it means that the dbcontext can become stale and will not return
        // the expected result (which might be a problem for authentication...)
        //private tradeheroEntities db = tradeheroEntities.Create();

        //public override void OnAuthorization(HttpActionContext actionContext)
        //{
        //    var info = new BasicAuthenticationInfo(actionContext.Request);
        //    if (info.IsValid())
        //    {
        //        ThIdentity thUser = new ThIdentity(info.UserEmail);
        //        HttpContext.Current.User = new GenericPrincipal(thUser, null);
        //    }
        //    else
        //    {
        //        actionContext.Response = actionContext.Request.CreateErrorResponse(HttpStatusCode.Unauthorized, info.ErrorMessage ?? Global.LocalizedString(ApiStringKeys.ERR_api_AUTH_FAIL));
        //    }

        //    base.OnAuthorization(actionContext);
        //}
    }
}