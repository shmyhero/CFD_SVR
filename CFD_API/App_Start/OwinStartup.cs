using CFD_API.SignalR;
using Microsoft.AspNet.SignalR;
//using Microsoft.Owin.Cors;
//using Microsoft.Owin.Security.OAuth;
using Owin;

namespace CFD_API
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Any connection or hub wire up and configuration should go here
            //app.

            app.MapSignalR();

            //app.Map("/signalr", map =>
            //{
            //    map.UseCors(CorsOptions.AllowAll);

            //    map.UseOAuthBearerAuthentication(new OAuthBearerAuthenticationOptions()
            //    {
            //        Provider = new QueryStringAuthorizeAttribute()
            //    });

            //    var hubConfiguration = new HubConfiguration
            //    {
            //        Resolver = GlobalHost.DependencyResolver,
            //    };
            //    map.RunSignalR(hubConfiguration);
            //});
        }
    }
}