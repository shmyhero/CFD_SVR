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
        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            // Ignore OperationCanceledException
            if (actionExecutedContext.Exception is OperationCanceledException)
                return;

            base.OnException(actionExecutedContext);
            //Elmah.ErrorLog.GetDefault(HttpContext.Current).Log(new Elmah.Error(actionExecutedContext.Exception));
            ErrorSignal.FromCurrentContext().Raise(actionExecutedContext.Exception);

            actionExecutedContext.Request.CreateErrorResponse(HttpStatusCode.InternalServerError, Translator.Translate(TransKey.EXCEPTION));
        }
    }
}