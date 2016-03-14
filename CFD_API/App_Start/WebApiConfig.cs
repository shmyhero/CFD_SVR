using System.Reflection;
using System.Web.Http;
using System.Web.Http.Cors;
using Autofac;
using Autofac.Integration.WebApi;
using AutoMapper;
using CFD_COMMON.Models.Context;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CFD_API
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            //enable CORS
            var cors = new EnableCorsAttribute("*", "*", "GET,POST,DELETE");
            config.EnableCors(cors);

            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();

            //web api routes
            //config.Routes.MapHttpRoute(
            //    name: "MiscStuff",
            //    routeTemplate: "api/misc/{action}",
            //    defaults: new { controller = "misc" }
            //    );
            //config.Routes.MapHttpRoute(
            //    name: "Utility",
            //    routeTemplate: "api/{action}",
            //    defaults: new { controller = "util" }
            //    );

            //default route
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{action}/{id}",
                defaults: new {id = RouteParameter.Optional}
                );
        }

        public static void ConfigureJSONFormatter(HttpConfiguration config)
        {
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            json.SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.None;
            json.SerializerSettings.FloatParseHandling = FloatParseHandling.Decimal;
            var converters = json.SerializerSettings.Converters;
            converters.Add(new IsoDateTimeConverter() {DateTimeFormat = "yyyy-MM-ddTHH:mm:ss"});
            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }

        public static void ConfigureDependencyResolver(HttpConfiguration config)
        {
            var builder = new ContainerBuilder();
            var assembly = Assembly.GetExecutingAssembly();

            //register all the controllers
            builder.RegisterApiControllers(assembly).PropertiesAutowired();
            //builder.RegisterControllers(assembly);

            // instantiate a tradeheroEntities on each incoming request
            builder.Register<CFDEntities>(c => CFDEntities.Create()).InstancePerRequest();
            //builder.Register<tradeheroNewsEntities>(c => tradeheroNewsEntities.Create()).InstancePerRequest();
            //builder.Register<StatisticEntities>(c => StatisticEntities.Create()).InstancePerRequest();
            //builder.Register<IRedisClient>(c => redisClientManager.GetClient()).InstancePerRequest();

            //// JSON formatter settings for MVC controllers
            //builder.Register<JsonSerializerSettings>(c => config.Formatters.JsonFormatter.SerializerSettings).InstancePerRequest();

            //builder.RegisterType<DefaultCacheKeyGenerator>().As<ICacheKeyGenerator>().SingleInstance();
            //builder.RegisterType<RedisCacheProvider>().As<ICacheProvider>().InstancePerRequest();
            //builder.RegisterType<CacheInterceptor>().InstancePerRequest();

            builder.Register<IMapper>(c => MapperConfig.GetAutoMapperConfiguration().CreateMapper()).SingleInstance();

            //var assemblies = assembly.GetReferencedAssemblies().Select(Assembly.Load).ToArray();
            //builder.RegisterAssemblyTypes(assemblies)
            //    .Where(t => t.IsSubclassOf(typeof(TradeHeroService)))
            //    .EnableClassInterceptors().InterceptedBy(typeof(CacheInterceptor))
            //    .InstancePerRequest().PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

            // Build the container.
            // Create the dependency resolver 
            var container = builder.Build();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(container);

            //var mvcResolver = new AutofacDependencyResolver(container);
            //DependencyResolver.SetResolver(mvcResolver);
        }
    }
}