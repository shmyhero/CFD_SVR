using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
//using Microsoft.Owin.Security.OAuth;

namespace CFD_API.SignalR
{
    //public class QueryStringAuthorizeAttribute : OAuthBearerAuthenticationProvider
    //{
    //    //public override Task RequestToken(OAuthRequestTokenContext context)
    //    //{
    //    //    var value = context.Request.Query.Get("access_token");

    //    //    if (!string.IsNullOrEmpty(value))
    //    //    {
    //    //        context.Token = value;
    //    //    }

    //    //    return Task.FromResult<object>(null);
    //    //}

    //    public override Task RequestToken(OAuthRequestTokenContext context)
    //    {
    //        return base.RequestToken(context);
    //    }

    //    public override Task ValidateIdentity(OAuthValidateIdentityContext context)
    //    {
    //        //var value = context.Request.Query.Get("access_token");

    //        return base.ValidateIdentity(context);
    //    }
    //}

    public class QueryStringAuthorizeAttribute : AuthorizeAttribute
    {
        public override bool AuthorizeHubConnection(HubDescriptor hubDescriptor, IRequest request)
        {
            var auth = request.QueryString["access_token"];

            int userId = 0;
            string token = null;

            try
            {
                var split = auth.Split('_');
                userId = Convert.ToInt32(split[0]);
                token = split[1];
            }
            catch (Exception ex)
            {
                //this.Context.User.Identity.IsAuthenticated = false;
            }

            request.Environment["server.User"] = new GenericPrincipal(new GenericIdentity(userId.ToString()), null);

            //request.

            //return true;

            return base.AuthorizeHubConnection(hubDescriptor, request);
        }

        public override bool AuthorizeHubMethodInvocation(IHubIncomingInvokerContext hubIncomingInvokerContext, bool appliesToMethod)
        {
            //return true;

            return base.AuthorizeHubMethodInvocation(hubIncomingInvokerContext, appliesToMethod);
        }

        protected override bool UserAuthorized(IPrincipal user)
        {
            var s = HttpContext.Current.User.Identity.Name;
            //var name = Context.User.Identity.Name;

            return true;

            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            var principal = user as ClaimsPrincipal;

            if (principal != null)
            {
                Claim authenticated = principal.FindFirst(ClaimTypes.Authentication);
                if (authenticated != null && authenticated.Value == "true")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            return base.UserAuthorized(user);
        }
    }
}