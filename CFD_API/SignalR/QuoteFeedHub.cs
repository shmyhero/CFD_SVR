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
    //[BasicAuth]
    //[QueryStringAuthorizeAttribute]
    //[QueryStringAuthorize]
    //[Authorize()]
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
        }

        public override Task OnConnected()
        {
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            return base.OnDisconnected(stopCalled);
        }

        //private int? userId = null;

        [HubMethodName("S")]
        public void Subscribe(string strSecurityIds)
        {
            var secIds = strSecurityIds.Split(',').Select(o => Convert.ToInt32(o));

            //var identity = 1; // Convert.ToInt32(HttpContext.Current.User.Identity.Name);

            var s = HttpContext.Current.User.Identity.Name;
            var name = Context.User.Identity.Name;

            ////Clients.user
            //if (userId.HasValue)
            //    _quoteFeedTicker.SetSubscription(userId.Value, secIds);

            //_quoteFeedTicker.SetSubscription(s, secIds);

            //Clients.All.p(new List<QuoteFeed>() {new QuoteFeed() {Id = 111, last = 2.222m}});
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