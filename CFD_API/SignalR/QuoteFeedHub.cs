using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using CFD_API.DTO.SignalRDTO;
using Microsoft.AspNet.SignalR.Hubs;
using ServiceStack.Common;

namespace CFD_API.SignalR
{
    [HubName("Q")]
    //[QueryStringAuthorize]
    public class QuoteFeedHub : Microsoft.AspNet.SignalR.Hub
    {
        private readonly QuoteFeedTicker _quoteFeedTicker;

        public QuoteFeedHub() : this(QuoteFeedTicker.Instance)
        {
        }

        public QuoteFeedHub(QuoteFeedTicker quoteFeedTicker)
        {
            _quoteFeedTicker = quoteFeedTicker;

            //var id = Context.ConnectionId;

            //var s = HttpContext.Current.User.Identity.Name;
            //var user = Context.User;
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            //var auth = Context.QueryString["auth"];
            //var userId = auth.Substring(0, auth.IndexOf('_'));

            //leave group
            Groups.Remove(Context.ConnectionId, Context.ConnectionId);

            //clear quote subscription
            _quoteFeedTicker.RemoveSubscription(Context.ConnectionId);

            return base.OnDisconnected(stopCalled);
        }

        //private int? userId = null;

        [HubMethodName("S")]
        public void Subscribe(string strSecurityIds)
        {
            //var auth = Context.QueryString["auth"];
            //var userId = auth.Substring(0, auth.IndexOf('_'));

            //join group
            Groups.Add(Context.ConnectionId, Context.ConnectionId);// single-user group

            //add quote subscription
            var secIds = strSecurityIds.Split(',').Select(o => Convert.ToInt32(o)).Distinct().ToList();

            _quoteFeedTicker.AddSubscription(Context.ConnectionId, secIds);
        }

        //[HubMethodName("L")]
        //public void Login(string auth)
        //{
        //    //int userId = 0;
        //    string token = null;

        //    try
        //    {
        //        var split = auth.Split('_');
        //        userId = Convert.ToInt32(split[0]);
        //        token = split[1];
        //    }
        //    catch (Exception ex)
        //    {
        //        //this.Context.User.Identity.IsAuthenticated = false;
        //    }


        //    var s = HttpContext.Current.User.Identity.Name;
        //    var name = Context.User.Identity.Name;

        //    //HttpContext.Current.User = new GenericPrincipal(new GenericIdentity(userId.ToString()), null);

        //    // Get the request lifetime scope so you can resolve services.
        //    //var requestScope = HttpContext.Current.Request.GetDependencyScope();

        //    // Resolve the service you want to use.
        //    //var db = requestScope.GetService(typeof(CFDEntities)) as CFDEntities;

        //    //var isUserExist = db.Users.Any(o => o.Id == userId && o.Token == token);

        //    //if (!isUserExist)
        //    //    actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);
        //}

        //public void Broadcast()
        //{

        //}

        //public IEnumerable<QuoteFeed> GetAllStocks()
        //{
        //    return new List<QuoteFeed>(){new QuoteFeed(){Id=1,last=1.22354m}};
        //}
    }
}