using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using CFD_COMMON.Models.Context;

namespace CFD_API.Controllers.Attributes
{
    public class BasicAuth : AuthorizationFilterAttribute
    {
        // DO NOT DO THIS!!!
        // the attribute filters persist across request
        // it means that the dbcontext can become stale and will not return
        // the expected result (which might be a problem for authentication...)
        //private tradeheroEntities db = tradeheroEntities.Create();

        public override void OnAuthorization(HttpActionContext actionContext)
        {
            var authorization = actionContext.Request.Headers.Authorization;
            if (authorization == null)
                actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);
            else
            {
                int userId = 0;
                string token = null;

                try
                {
                    var split = authorization.Parameter.Split('_');
                    userId = Convert.ToInt32(split[0]);
                    token = split[1];
                }
                catch (Exception)
                {
                    actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);
                }

                // Get the request lifetime scope so you can resolve services.
                var requestScope = actionContext.Request.GetDependencyScope();

                // Resolve the service you want to use.
                var db = requestScope.GetService(typeof (CFDEntities)) as CFDEntities;

                var isUserExist = db.Users.Any(o => o.Id == userId && o.Token == token);

                if (!isUserExist)
                    actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);

                ////var info = new BasicAuthenticationInfo(actionContext.Request);
                //if (info.IsValid())
                //{
                //    ThIdentity thUser = new ThIdentity(info.UserEmail);
                //    HttpContext.Current.User = new GenericPrincipal(thUser, null);
                //}
                //else
                //{
                //    actionContext.Response = actionContext.Request.CreateErrorResponse(HttpStatusCode.Unauthorized, info.ErrorMessage ?? Global.LocalizedString(ApiStringKeys.ERR_api_AUTH_FAIL));
                //}

                HttpContext.Current.User = new GenericPrincipal(new GenericIdentity(userId.ToString()), null);
            }

            base.OnAuthorization(actionContext);
        }
    }
}