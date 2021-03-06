﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.EnterpriseServices;
using System.Linq;
using System.Net;
using System.Threading;
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
using CFD_COMMON.Utils;
using Elmah;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace CFD_API
{
    public class Global : HttpApplication
    {
        void Application_Start(object sender, EventArgs e)
        {
            ThreadPool.SetMinThreads(1000, 1000);

            ServicePointManager.DefaultConnectionLimit = 1000;

            ServicePointManager.ServerCertificateValidationCallback = (o, certificate, chain, errors) => true;

            ServicePointManager.Expect100Continue = false;


            // Code that runs on application startup
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            WebApiConfig.ConfigureJSONFormatter(GlobalConfiguration.Configuration);
            WebApiConfig.ConfigureDependencyResolver(GlobalConfiguration.Configuration);

            GlobalConfiguration.Configuration.Filters.Add(new ElmahHandledErrorLoggerFilter());

            GlobalConfiguration.Configuration.Filters.Add(new ApiHitRecordFilter());

            GlobalConfiguration.Configuration.MessageHandlers.Add(new CultureHandler());

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

        void ErrorLog_Filtering(object sender, ExceptionFilterEventArgs e)
        {
            Filter(e);
        }

        private static DateTime _lastElmahMailTime = DateTime.MinValue;
        private static string _lastElmahMessage = string.Empty;

        void ErrorMail_Filtering(object sender, ExceptionFilterEventArgs e)
        {
            Filter(e);

            //no email for localhost
            var host = ((HttpContext) e.Context).Request.Url.Host;
            if (host.ToLower().StartsWith("localhost"))
                e.Dismiss();

            if (!e.Dismissed)
            {
                var ts = DateTime.UtcNow - _lastElmahMailTime;
                var chinaNow = DateTimes.GetChinaNow();

                if (ts < TimeSpan.FromMinutes(10)) //within 10 min
                {
                    e.Dismiss();
                }
                else if (e.Exception.Message.Substring(0, 10) == _lastElmahMessage && ts < TimeSpan.FromHours(1))
                    //same message && within 1 hour
                {
                    e.Dismiss();
                }
                else if (ts < TimeSpan.FromHours(12) &&
                         (_lastElmahMailTime.AddHours(8).Hour >= 23 || _lastElmahMailTime.AddHours(8).Hour < 7) &&
                         (chinaNow.Hour >= 23 || chinaNow.Hour < 7)) //only 1 at night
                {
                    e.Dismiss();
                }
                else
                {
                    _lastElmahMailTime = DateTime.UtcNow;
                    _lastElmahMessage = e.Exception.Message.Substring(0, 10);
                }
            }
        }

        void Filter(ExceptionFilterEventArgs e)
        {
            if (e.Exception is HttpException && e.Exception.Message.Contains("was not found or does not implement IController"))
                e.Dismiss();
        }
    }
}