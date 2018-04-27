using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet;
using System.Reflection;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Hosting;

namespace WatchDogSignalRServer
{
    public class ServerHub : Hub
    {
        /// <summary>
        /// 用來記錄從Hello到Goodbye中間的暫存資訊，儲存哪一個ConnectionId對應到甚麼MachineName和TaskName
        /// </summary>
        private struct ClientInfo
        {
            public string MachineName { get; set; }
            public string TaskName { get; set; }
        }

        private IDisposable SignalR { get; set; }

        /// <summary>
        /// 紀錄ConnectionId對應的MachinName和TaskName
        /// </summary>
        private static Dictionary<string, ClientInfo> onlineIds = new Dictionary<string, ClientInfo>();

        /// <summary>
        /// 開始Server的監聽行為
        /// </summary>
        internal void Start()
        {
            Console.Write("Starting server...");
            Task.Run(() =>
            {
                var serverURI = Server.Default.ServerURL;
                try
                {
                    this.SignalR = WebApp.Start(serverURI);
                    Console.WriteLine("Success.");
                }
                catch (TargetInvocationException ex)
                {
                    Console.WriteLine("failed.");
                    Console.WriteLine("Server failed to start. " + ex.Message);

                    return;
                }
                Console.WriteLine("Server started at " + serverURI);
            });

            while (true)
            {
                Console.ReadLine();
            }
        }

        /// <summary>
        /// 由WatchDog.CpuRamMonitor回報使用
        /// </summary>
        public void inCpuRamReport(string machineName, long cpuRawValue, long ramRawValue)
        {
            Clients.All.onCpuRamReport(machineName, cpuRawValue, ramRawValue);
        }

        /// <summary>
        /// 由WatchDog.ProcessMonitor回報使用
        /// </summary>
        public void inProcessReport(string machineName, WatchDogTaskLib.ProcessMonitor.ReportItem item)
        {
            Clients.All.onProcessReport(machineName, item);
        }

        /// <summary>
        /// 任何任務單元上線後的第一個動作就是向其他單元打招呼，用以回報自己的Id對應MachineName和TaskName
        /// </summary>
        public void inHelloToOther(string machineName, string taskName)
        {
            var id = Context.ConnectionId;
            var client = new ClientInfo() { MachineName = machineName, TaskName = taskName };
            if (onlineIds.ContainsKey(id) == false)
            {
                onlineIds.Add(id, client);
            }
            else
            {
                onlineIds[id] = client;
            }

            Clients.Others.onHello(id, machineName, taskName);
            var disp = $"{machineName}-{taskName}";
            Console.WriteLine(disp + " say hello");
        }

        /// <summary>
        /// 由WatchDog.Dashboard單元呼叫，用來讓剛啟動的Dashboard知道目前有哪些可監測的任務單元
        /// </summary>
        public void inWhoIsThere()
        {
            var id = Context.ConnectionId;

            onlineIds.ToList().ForEach(pair =>
            {
                Clients.Caller.onHello(pair.Key, pair.Value.MachineName, pair.Value.TaskName);
            });
        }

        public void inIAmAlive(string machineName)
        {
            Clients.All.OnAliveReported(machineName);
        }

        public void inDBTestReport(string machineName, string folderName, string fileName, string result, double executionMs)
        {
            Clients.All.onDBTestReport(machineName, folderName, fileName, result, executionMs);
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            var id = Context.ConnectionId;
            if (onlineIds.ContainsKey(id))
            {
                var item = onlineIds[id];
                onlineIds.Remove(id);
                Clients.Others.onGoodbye(id, item.MachineName, item.TaskName);
                var disp = $"{item.MachineName}-{item.TaskName}";
                Console.WriteLine(disp + " say goodbye");
            }

            return base.OnDisconnected(stopCalled);
        }
    }
}
