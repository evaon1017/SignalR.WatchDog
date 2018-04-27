using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace DashboardWeb
{
    [HubName("hitCounter")]
    public class HitCounterHub : Hub
    {
        static int _cnt = 0;
        public void RecordHit()
        {
            _cnt++;
            Clients.All.onRecordHit(_cnt);
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            _cnt--;
            Clients.All.onRecordHit(_cnt);
            return base.OnDisconnected(stopCalled);
        }
    }
}