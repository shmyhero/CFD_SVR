using CFD_COMMON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace CFD_API.Controllers.Attributes
{
    public class RecordHeaders : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            base.OnActionExecuting(actionContext);

            StringBuilder sb = new StringBuilder();
            sb.Append("heads====================");
            sb.Append(System.Environment.NewLine);
            actionContext.Request.Headers.ToList().ForEach(header => {
                sb.Append(header.Key);
                sb.Append(":");
                sb.Append(string.Join(",", header.Value.ToArray()));
                sb.Append(System.Environment.NewLine);
            });
            sb.Append("properties====================");
            sb.Append(System.Environment.NewLine);
            actionContext.Request.Properties.ToList().ForEach(pro => {
                sb.Append(pro.Key);
                sb.Append(":");
                sb.Append(string.Join(",", pro.Value.ToString()));
                sb.Append(System.Environment.NewLine);
            });
            sb.Append("uri====================");
            sb.Append(System.Environment.NewLine);
            sb.Append(actionContext.Request.RequestUri);
            CFDGlobal.LogInformation(sb.ToString());
        }
    }
}