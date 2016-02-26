using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON.Models.Entities;

namespace CFD_API
{
    public class Global : HttpApplication
    {
        void Application_Start(object sender, EventArgs e)
        {
            // Code that runs on application startup
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            WebApiConfig.ConfigureJSONFormatter(GlobalConfiguration.Configuration);
            WebApiConfig.ConfigureDependencyResolver(GlobalConfiguration.Configuration);

            GlobalConfiguration.Configuration.Filters.Add(new ElmahHandledErrorLoggerFilter());

            //var config = new MapperConfiguration(cfg => cfg.CreateMap<User, UserDTO>());
        }
    }
}