using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatchDogTaskLib;

namespace WatchDog.CpuRamMonitor
{
    class Task : TaskBase
    {
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;

        private bool init = false;

        protected override string TaskName
        {
            get
            {
                return "CpuRamMonitor";
            }
        }

        protected override bool TaskCheck()
        {
            return true;
        }

        protected override TimeSpan NextCheckPoint()
        {
            return TimeSpan.FromSeconds(10);
        }

        protected override void DoTask()
        {
            if (this.init == false)
            {
                this.cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                this.ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                this.init = true;
            }

            base.SendSignal("inCpuRamReport", Environment.MachineName, cpuCounter.NextValue(), ramCounter.NextValue());
        }

        protected override void AddHandler()
        {
            base.AddHandler<string, long, long>(
                "onCpuRamReport",
                (name, cpu, ram) =>
                {
                    if (base.ReceiveSignal == true)
                    {
                        base.AppendLog(1, $"[{name}] CPU: {cpu}%, RAM: {ram}MB");
                    }
                });
        }
    }
}
