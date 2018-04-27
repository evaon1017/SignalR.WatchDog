using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatchDog.CpuRamMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var task = new Task())
            {
                task.Start(System.Configuration.ConfigurationManager.AppSettings["url"], "ServerHub");
            }
        }
    }
}
