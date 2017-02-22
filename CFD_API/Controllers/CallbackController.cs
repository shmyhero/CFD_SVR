using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON;
using CFD_COMMON.Models.Context;
using Newtonsoft.Json.Linq;

namespace CFD_API.Controllers
{
    [RoutePrefix("api")]
    public class CallbackController : CFDController
    {
        public CallbackController(CFDEntities db, IMapper mapper) : base(db, mapper)
        {
        }

        [HttpPut]
        [Route("Live/live-account/{accountGuid}/status")]
        public LifecycleCallbackDTO LiveAccountStatusCallback(string accountGuid, LifecycleCallbackFormDTO form)
        {
            var authorization = Request.Headers.Authorization;

            //if (authorization != null)
            //    CFDGlobal.LogWarning("Lifecycle Callback header: " + authorization.Scheme + " " + authorization.Parameter);

            if (authorization == null || authorization.Parameter == null || authorization.Parameter != CFDGlobal.AMS_CALLBACK_AUTH_TOKEN)
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "invalid auth token"));

            if (form != null)
            {
                CFDGlobal.LogWarning("AMS Callback live-account status: " + (accountGuid ?? "") + " " + (form.status ?? ""));

                if (!string.IsNullOrWhiteSpace(accountGuid) && !string.IsNullOrWhiteSpace(form.status))
                {
                    var user = db.Users.FirstOrDefault(o => o.AyLiveAccountGuid == accountGuid);
                    if (user != null)
                    {
                        user.AyLiveAccountStatus = form.status;
                        db.SaveChanges();
                    }
                }
            }

            return new LifecycleCallbackDTO();
        }
    }
}
