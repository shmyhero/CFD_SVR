using CFD_COMMON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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

            //StringBuilder sb = new StringBuilder();
            //sb.Append("heads====================");
            //sb.Append(System.Environment.NewLine);
            //actionContext.Request.Headers.ToList().ForEach(header => {
            //    sb.Append(header.Key);
            //    sb.Append(":");
            //    sb.Append(string.Join(",", header.Value.ToArray()));
            //    sb.Append(System.Environment.NewLine);
            //});
            //sb.Append("properties====================");
            //sb.Append(System.Environment.NewLine);
            //actionContext.Request.Properties.ToList().ForEach(pro => {
            //    sb.Append(pro.Key);
            //    sb.Append(":");
            //    sb.Append(string.Join(",", pro.Value.ToString()));
            //    sb.Append(System.Environment.NewLine);
            //});
            //sb.Append("uri====================");
            //sb.Append(System.Environment.NewLine);
            //sb.Append(actionContext.Request.RequestUri);

            var agent = actionContext.Request.Headers.FirstOrDefault(h => h.Key == "User-Agent");
            if (agent.Value != null && agent.Value.Count() > 0)
            {
                if(isBrowserRequest(agent.Value.ToList()))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(System.Environment.NewLine);
                    actionContext.Request.Headers.ToList().ForEach(header =>
                    {
                        sb.Append(header.Key);
                        sb.Append(":");
                        sb.Append(string.Join(",", header.Value.ToArray()));
                        sb.Append(System.Environment.NewLine);
                    });

                    CFDGlobal.LogInformation("SMS attack detected and intercepted.");
                    CFDGlobal.LogInformation(sb.ToString());
                    
                   

                    actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.OK, "{\"success\":true,\"message\":\"OK\"}");
                    
                }
            }
           
        }

        private bool isBrowserRequest(List<string> agents)
        {
            List<string> browsers = new List<string>();
            browsers.AddRange(new string[] { "Opera", "Presto", "Mozilla", "Intel Mac", "Chrome", "Firefox" });

            foreach(string agent in agents)
            {
                foreach(string browser in browsers)
                {
                    if (agent.IndexOf(browser) > -1) //如果agent字段包含有以上浏览器信息，返回true
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}