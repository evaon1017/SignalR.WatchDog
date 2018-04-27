using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatchDogTaskLib.ProcessMonitor
{
    public class ReportItem
    {
        public class ReportItemDetail
        {
            public int PID { get; set; }
            public long MemoryUsage { get; set; }
            public long CPUUsage { get; set; }
        }

        public string ProcessName { get; set; }
        public ReportItemDetail[] Items { get; set; }
    }
}
