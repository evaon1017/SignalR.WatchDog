using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatchDog.AliveMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            new Task().Start(System.Configuration.ConfigurationManager.AppSettings["url"], "ServerHub");
        }
    }
}
