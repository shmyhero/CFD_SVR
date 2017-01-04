using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Utils;
using System.Data.SqlTypes;

namespace CFD_API.Controllers.Attributes
{
    public class BasicAuth2 : AuthorizationFilterAttribute
    {
        public override void OnAuthorization(HttpActionContext actionContext)
        {
            
            var authorization = actionContext.Request.Headers.Authorization;
            if (authorization == null)
                actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);
            else
            {
                int userId = 0;
                string token = null;

                try
                {
                    var split = authorization.Parameter.Split('_');
                    userId = Convert.ToInt32(split[0]);
                    token = split[1];
                }
                catch (Exception)
                {
                    actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);
                }

                // Get the request lifetime scope so you can resolve services.
                var requestScope = actionContext.Request.GetDependencyScope();

                // Resolve the service you want to use.
                var db = requestScope.GetService(typeof(CFDEntities)) as CFDEntities;

                var user = db.Users.FirstOrDefault(o => o.Id == userId && o.Token == token);

                if (user == null) //unauthorize
                    actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);
                else//record last hit time
                {
                    user.LastHitAt = DateTime.UtcNow;
                    db.SaveChanges();
                }
                HttpContext.Current.User = new GenericPrincipal(new GenericIdentity(userId.ToString()), null);

                #region 验证时间戳
                //Live环境下才验证时间戳
                if (actionContext.Request.RequestUri.AbsolutePath.Contains("/live/") || actionContext.Request.RequestUri.AbsolutePath.EndsWith("/live"))
                {
                    if(!actionContext.Request.Headers.Contains("signature"))
                    {
                        actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized,"缺少signature");
                    }
                    else
                    {
                        var des = new DESUtil();
                        try
                        {
                            string decrypted = des.Decrypt(actionContext.Request.Headers.GetValues("signature").First(), token.Substring(0, 8));
                            if(decrypted.Length < 11)//10位TimeStamp+Nonce
                            {
                                actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized, "签名不正确");
                            }
                            else
                            {
                                long timeStamp = 0;
                                int nonce = 0;
                                long.TryParse(decrypted.Substring(0, 10), out timeStamp);
                                int.TryParse(decrypted.Substring(10), out nonce);
                                if(timeStamp == 0 || nonce == 0)
                                {
                                    actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized, "时间戳/随机数不正确");
                                }
                                else
                                {
                                    var record = db.TimeStampNonces.FirstOrDefault(o => o.Nonce == nonce && o.TimeStamp == timeStamp && o.Expiration == SqlDateTime.MaxValue.Value);
                                    if(record == null)
                                    {
                                        actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized, "时间戳不存在");
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
                            actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized,"signature解密失败");
                        }
                    }
                }
                #endregion

            }
        
            base.OnAuthorization(actionContext);
        }
    }
}