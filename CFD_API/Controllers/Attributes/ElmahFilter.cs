using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http.Filters;
using CFD_COMMON.Localization;
using Elmah;

namespace CFD_API.Controllers.Attributes
{
    public class ElmahHandledErrorLoggerFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            // Ignore OperationCanceledException
            if (context.Exception is OperationCanceledException)
                return;

            base.OnException(context);
            //Elmah.ErrorLog.GetDefault(HttpContext.Current).Log(new Elmah.Error(actionExecutedContext.Exception));
            ErrorSignal.FromCurrentContext().Raise(context.Exception);

            //unify error response
            context.Response = context.Request.CreateErrorResponse(HttpStatusCode.InternalServerError, Translator.Translate(TransKey.EXCEPTION));
        }
    }
}