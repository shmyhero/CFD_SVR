using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CFD_API
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();


            //web api routes
            config.Routes.MapHttpRoute(
                name: "MiscStuff",
                routeTemplate: "api/misc/{action}",
                defaults: new { controller = "misc" }
                );

            //default route
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

        }
        public static void ConfigureJSONFormatter(HttpConfiguration config)
        {
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            json.SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.None;
            json.SerializerSettings.FloatParseHandling = FloatParseHandling.Decimal;
            var converters = json.SerializerSettings.Converters;
            converters.Add(new IsoDateTimeConverter() { DateTimeFormat = "yyyy-MM-ddTHH:mm:ss" });
            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }
    }
}
