using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatchDogSignalRServer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            new ServerHub().Start();
        }
    }
}
