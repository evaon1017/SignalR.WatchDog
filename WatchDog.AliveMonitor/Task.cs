using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatchDog.AliveMonitor
{
    class Task : WatchDogTaskLib.TaskBase
    {
        protected override string TaskName
        {
            get
            {
                return "AliveMonitor";
            }
        }

        protected override void AddHandler()
        {
            base.AddHandler<string>("onAliveReported", this.OnAliveReported);
            base.ReceiveSignal = false;
        }

        protected override void DoTask()
        {
            base.SendSignal("inIAmAlive", this.machineName);
            base.AppendLog(1, "alive reported");
        }

        protected override TimeSpan NextCheckPoint()
        {
            return TimeSpan.FromSeconds(5);
        }

        protected override bool TaskCheck()
        {
            return true;
        }

        private void OnAliveReported(string machineName)
        {
            if (base.ReceiveSignal == true)
            {
                base.AppendLog(1, $"{machineName} alive reported");
            }
        }
    }
}
