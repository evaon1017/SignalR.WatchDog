using Microsoft.AspNet.SignalR.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using WatchDogTaskLib.ProcessMonitor;

namespace WatchDog.Windows.Dashboard
{
    public partial class Form1 : Form
    {
        private DataSet mainDatas;
        private DataTable onlineTasks;
        private DataTable processMonitorData;
        private DataTable cpuRamMonitorData;
        private string url;
        private IHubProxy hubProxy;
        private HubConnection connection;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.ReadyDataSet();
            this.ReadyChart();
            this.dataGridView1.DataSource = this.onlineTasks;
            this.url = Config.Default.url;

            this.connection = new HubConnection(this.url);
            this.connection.StateChanged += (a) =>
            {
                if (a.NewState == Microsoft.AspNet.SignalR.Client.ConnectionState.Connected)
                {
                    this.Invoke((Action)(() =>
                    {
                        this.UI_lblStatus.Text = "SignalR hub Connected";
                        this.statusStrip1.Refresh();
                        this.hubProxy.Invoke("inHelloToOther", Environment.MachineName, "DashBoard");
                    }));
                }
            };

            this.hubProxy = connection.CreateHubProxy("ServerHub");

            this.hubProxy.On<string, string, string>("onHello", this.onHello);
            this.hubProxy.On<string, string, string>("onGoodbye", this.onGoodbye);

            this.hubProxy.On<string, ReportItem>("onProcessReport", this.onProcessReport);
            this.hubProxy.On<string, long, long>("onCpuRamReport", this.onCpuRamReport);

            this.EnsureSignalRConnection();
            this.hubProxy.Invoke("inWhoIsThere");
        }

        private void ReadyDataSet()
        {
            this.mainDatas = new DataSet();
            this.onlineTasks = this.mainDatas.Tables.Add("OnlineTasks");
            this.onlineTasks.Columns.Add("MachineName");
            this.onlineTasks.Columns.Add("TaskName");
            this.onlineTasks.Columns.Add("Id");
            this.onlineTasks.DefaultView.Sort = "MachineName asc, TaskName desc";
            this.onlineTasks.DefaultView.ApplyDefaultSort = true;
            this.onlineTasks.PrimaryKey = new[] { this.onlineTasks.Columns["Id"] };

            this.processMonitorData = this.mainDatas.Tables.Add("ProcessMonitor");
            this.processMonitorData.Columns.Add("Time", typeof(DateTime));
            this.processMonitorData.Columns.Add("MachineName");
            this.processMonitorData.Columns.Add("ProcessName");
            this.processMonitorData.Columns.Add("Memory");
            this.processMonitorData.DefaultView.Sort = "MachineName Asc, ProcessName Asc, Time Desc";
            this.processMonitorData.DefaultView.ApplyDefaultSort = true;
            this.processMonitorData.PrimaryKey = new[] { this.processMonitorData.Columns["MachineName"], this.processMonitorData.Columns["ProcessName"], this.processMonitorData.Columns["Time"]};

            this.cpuRamMonitorData = this.mainDatas.Tables.Add("CpuRamMonitor");
            this.cpuRamMonitorData.Columns.Add("Time", typeof(DateTime));
            this.cpuRamMonitorData.Columns.Add("MachineName");
            this.cpuRamMonitorData.Columns.Add("CPU");
            this.cpuRamMonitorData.Columns.Add("Memory");
            this.cpuRamMonitorData.DefaultView.Sort = "MachineName Asc, Time Desc";
            this.cpuRamMonitorData.DefaultView.ApplyDefaultSort = true;
            this.cpuRamMonitorData.PrimaryKey = new[] { this.cpuRamMonitorData.Columns["MachineName"], this.cpuRamMonitorData.Columns["Time"] };
        }

        private void ReadyChart()
        {
        }

        private void onHello(string connectionId, string machineName, string taskName)
        {
            var row = onlineTasks.Select($"Id = '{connectionId}'").FirstOrDefault();
            if (row == null)
            {
                this.CallByForm(() =>
                {
                    this.Invoke((Action)(() => onlineTasks.Rows.Add(machineName, taskName, connectionId)));
                });
            }
        }

        private void onGoodbye(string connectionId, string machineName, string taskName)
        {
            var row = onlineTasks.Select($"Id = '{connectionId}'").FirstOrDefault();
            if (row != null)
            {
                this.CallByForm(() =>
                {
                    this.Invoke((Action)(() => onlineTasks.Rows.Remove(row)));
                });
            }
        }

        private void onProcessReport(string machineName, ReportItem result)
        {
            this.CallByForm(() =>
            {
                this.processMonitorData.Rows.Add(DateTime.Now, machineName, result.ProcessName, result.Items.Sum(i => i.MemoryUsage));
            });
        }

        private Dictionary<string, Series> cpuRamSeriesData = new Dictionary<string, Series>();
        private void onCpuRamReport(string machineName, long cpuUsage, long ramRemain)
        {
            this.cpuRamMonitorData.Rows.Add(DateTime.Now, machineName, cpuUsage, ramRemain);
            var source = this.cpuRamMonitorData.Select($"MachineName = '{machineName}'").Reverse().Take(100).Reverse();
            this.CallByForm(() =>
            {
                new[] { "Memory", "CPU" }.ToList().ForEach(type =>
                {
                    var disp = $"{machineName}-{type}";
                    Series se;
                    if (cpuRamSeriesData.ContainsKey(disp) == false)
                    {
                        if (this.UI_chtTestingCPU.ChartAreas.Any(ca => ca.Name == type) == false)
                        {
                            this.UI_chtTestingCPU.ChartAreas.Add(type);
                            //this.UI_chtTestingCPU.ChartAreas[type].AxisX.Interval = 30d;
                        }
                        se = this.UI_chtTestingCPU.Series.Add(disp);
                        se.ChartArea = type;
                        se.ChartType = SeriesChartType.Line;
                        se.XValueMember = "Time";
                        se.XAxisType = AxisType.Primary;
                        se.XValueType = ChartValueType.Time;
                        se.YValueMembers = type;
                        cpuRamSeriesData.Add(disp, se);
                    }
                    else
                    {
                        se = cpuRamSeriesData[disp];
                    }

                    se.Points.DataBind(source, "Time", type, null);
                });
            });
        }

        private void EnsureSignalRConnection()
        {
            while (true)
            {
                switch (connection.State)
                {
                    case Microsoft.AspNet.SignalR.Client.ConnectionState.Connected:
                        return;

                    case Microsoft.AspNet.SignalR.Client.ConnectionState.Connecting:
                    case Microsoft.AspNet.SignalR.Client.ConnectionState.Reconnecting:
                        System.Threading.Thread.Sleep(1000);
                        break;

                    case Microsoft.AspNet.SignalR.Client.ConnectionState.Disconnected:
                        try
                        {
                            this.UI_lblStatus.Text =  "SignalR Hub connecting...";
                            this.statusStrip1.Refresh();
                            Task.Run(() => connection.Start().Wait());
                            System.Threading.Thread.Sleep(1000);
                            break;
                        }
                        catch (HttpRequestException)
                        {
                            this.UI_lblStatus.Text = "failed...retry in 5 seconds";
                            System.Threading.Thread.Sleep(5000);
                        }
                        break;
                }
            }
        }

        private void CallByForm(Action act)
        {
            this.Invoke(act);
        }
    }
}
