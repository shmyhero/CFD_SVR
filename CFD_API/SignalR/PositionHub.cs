using System;
using System.Linq;
using System.Threading.Tasks;
using CFD_COMMON.Models.Context;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace CFD_API.SignalR
{
    [HubName("P")]
    public class PositionHub : Hub
    {
        private readonly PositionReportTicker _posRptTicker;

        public PositionHub() : this(PositionReportTicker.Instance)
        {
        }

        public PositionHub(PositionReportTicker ticker)
        {
            _posRptTicker = ticker;

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

            ////clear quote subscription
            //_posRptTicker.RemoveSubscription(Context.ConnectionId);

            return base.OnDisconnected(stopCalled);
        }

        //private int? userId = null;

        [HubMethodName("S")]
        public void Subscribe(string auth)
        {
            int userId = -1;
            string token = null;
            try
            {
                var split = auth.Split('_');
                userId = Convert.ToInt32(split[0]);
                token = split[1];
            }
            catch (Exception)
            {
                return;
            }

            if (userId == -1 || token == null) return;

            var db = CFDEntities.Create();
            var user = db.Users.FirstOrDefault(o => o.Id == userId && o.Token == token);

            if (user == null) return;
            
            //join group
            Groups.Add(Context.ConnectionId, Context.ConnectionId); // single-user group

            //add subscription
            _posRptTicker.AddSubscription(user.AyondoUsername, Context.ConnectionId);
        }
    }
}