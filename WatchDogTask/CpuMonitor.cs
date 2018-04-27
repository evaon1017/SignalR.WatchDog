using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using System.Threading;
using System.Diagnostics;

namespace WatchDogTask
{
    [Serializable]
    public class CpuMonitor : MarshalByRefObject, WatchDogTaskLib.TaskBase
    {
        public Action<string> Log { get; set; }
        public Action<string> LogC { get; set; }
        
        private Timer tmr;
        private IHubProxy proxy;
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;

        public void Start(IHubProxy proxy)
        {
            this.proxy = proxy;

            this.proxy.On<string, long, long>(
                "onSystemResourceReport",
                (name, cpu, ram) => this.Log($"[{name}] CPU: {cpu}%, RAM: {ram}MB"));

            this.cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            this.ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            TimerCallback action = (state) =>
            {
                this.proxy.Invoke("systemResourceReport", Environment.MachineName, cpuCounter.NextValue(), ramCounter.NextValue());
                tmr.Change(1000, -1);
            };
            tmr = new Timer(action, null, 0, -1);
        }

        public void Stop()
        {
            this.tmr.Dispose();
        }
    }
}
