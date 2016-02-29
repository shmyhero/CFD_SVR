using System;
using System.Collections.Generic;
using System.Configuration;
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
using CFD_COMMON;
using CFD_COMMON.Models.Entities;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;

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


            //var isAvailable = RoleEnvironment.IsAvailable;

            //var setting = CloudConfigurationManager.GetSetting("asdf");
            //var appSetting = ConfigurationManager.AppSettings["asdf"];

            //var f = CloudConfigurationManager.GetSetting("CFDEntities");
            //var b = ConfigurationManager.ConnectionStrings["CFDEntities"].ConnectionString;
            //var c = ConfigurationManager.AppSettings["CFDEntities"];

            //var a = RoleEnvironment.GetConfigurationSettingValue("CFDEntities");
            //var aa = RoleEnvironment.GetConfigurationSettingValue("asdf");

            //var configurationSetting = CFDGlobal.GetConfigurationSetting("CFDEntities");
            //var setting = CFDGlobal.GetConfigurationSetting("asdf");
            //var s = CFDGlobal.GetConfigurationSetting("YunPianApiKey");
        }
    }
}