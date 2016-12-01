using Owin;
using System.Web.Http;
using System.Web.Http.Cors;

namespace CardService
{
    class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();

            config.Routes.MapHttpRoute(
                name: "cardapi",
                routeTemplate: "card/{action}",
                defaults: new { controller = "card", action = "PostCard" }
            );

            appBuilder.UseWebApi(config);
        }
    }
}
