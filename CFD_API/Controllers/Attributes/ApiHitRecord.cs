using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace CFD_API.Controllers.Attributes
{
    public class ApiHitRecordFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            base.OnActionExecuting(actionContext);

            ((CFDController) actionContext.ControllerContext.Controller).RequestStartAt = DateTime.UtcNow;
        }

        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            base.OnActionExecuted(actionExecutedContext);

            var startAt = ((CFDController)actionExecutedContext.ActionContext.ControllerContext.Controller).RequestStartAt;
            var timeSpent = (DateTime.UtcNow - startAt).TotalMilliseconds;

            var httpMethod = actionExecutedContext.Request.Method.Method;

            var isSuccess= actionExecutedContext.Exception == null;

            var httpContent = actionExecutedContext.Request.Content.ReadAsStringAsync().Result;
        }
    }
}