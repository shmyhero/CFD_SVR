using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using CFD_COMMON.Models.Entities;
using Newtonsoft.Json;

namespace CFD_API.Controllers.Attributes
{
    public class ApiHitRecordFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            base.OnActionExecuting(actionContext);

            //record start time
            ((CFDController) actionContext.ControllerContext.Controller).RequestStartAt = DateTime.UtcNow;
        }

        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            base.OnActionExecuted(actionExecutedContext);

            var controller = (CFDController) actionExecutedContext.ActionContext.ControllerContext.Controller;

            var startAt = controller.RequestStartAt;
            var timeSpent = (DateTime.UtcNow - startAt).TotalMilliseconds;

            var httpMethod = actionExecutedContext.Request.Method.Method;

            int? userId;
            try
            {
                userId = controller.UserId;
            }
            catch (Exception)
            {
                userId = null;
            }

            var isException = actionExecutedContext.Exception !=null;

            var param = JsonConvert.SerializeObject(actionExecutedContext.ActionContext.ActionArguments);

            string ip = null;
            if (actionExecutedContext.Request.Properties.ContainsKey("MS_HttpContext"))
            {
                var requestBase = ((HttpContextWrapper)actionExecutedContext.Request.Properties["MS_HttpContext"]).Request;
                ip = requestBase.UserHostAddress;
            }

            controller.db.ApiHits.Add(new ApiHit()
            {
                HitAt = startAt,
                HttpMethod = httpMethod,
                Ip = ip,
                IsException = isException,
                Param = param,
                TimeSpent = timeSpent,
                Url = actionExecutedContext.Request.RequestUri.AbsoluteUri,
                UserId = userId
            });
            controller.db.SaveChanges();
        }
    }
}