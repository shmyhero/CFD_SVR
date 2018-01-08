using System;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;

namespace CFD_API.Controllers.Attributes
{
    public class TimestampCaptchaAuth : AuthorizationFilterAttribute
    {
        public override void OnAuthorization(HttpActionContext actionContext)
        {
            if (!actionContext.Request.Headers.Contains("signature"))
            {
                actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized,
                    "signature required");
            }
            else
            {
                try
                {
                    string auth = actionContext.Request.Headers.GetValues("signature").First();
                    var arr = auth.Split('_');

                    if (arr.Length < 3)
                    {
                        actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized,
                            "invalid signature string");
                    }
                    else
                    {
                        long timeStamp = 0;
                        int nonce = 0;
                        string code = arr[2];
                        long.TryParse(arr[0], out timeStamp);
                        int.TryParse(arr[1], out nonce);
                        if (timeStamp == 0 || nonce == 0 || string.IsNullOrWhiteSpace(code))
                        {
                            actionContext.Response =
                                actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized,
                                    "invalid signature");
                        }
                        else
                        {
                            // Get the request lifetime scope so you can resolve services.
                            var requestScope = actionContext.Request.GetDependencyScope();

                            // Resolve the service you want to use.
                            var db = requestScope.GetService(typeof (CFDEntities)) as CFDEntities;

                            var record =
                                db.TimeStampNonces.FirstOrDefault(o => o.Nonce == nonce && o.TimeStamp == timeStamp);
                            if (record == null || record.Expiration != SqlDateTime.MaxValue.Value)
                            {
                                actionContext.Response =
                                    actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized,
                                        "signature unauthorized");
                            }
                            else if (code.ToLower() != record.CaptchaCode.ToLower())
                            {
                                record.CaptchaAttemps = (record.CaptchaAttemps ?? 0) + 1;
                                db.SaveChanges();

                                actionContext.Response =
                                    actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized,
                                        "signature unauthorized");
                            }
                            else
                            {
                                record.Expiration = DateTime.Now;
                                db.SaveChanges();
                            }
                        }
                    }
                }
                catch
                {
                    actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized,
                        "signature checking failed");
                }
            }

            base.OnAuthorization(actionContext);
        }
    }
}