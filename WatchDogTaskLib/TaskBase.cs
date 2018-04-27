using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using System.IO;
using System.Net.Http;

namespace WatchDogTaskLib
{
    public abstract class TaskBase: IDisposable
    {
        protected delegate void WriteLog(int intend, string msg);
        protected bool ReceiveSignal { get; set; } = true;
        protected bool EchoDebugInfo { get; set; } = false;
        protected string machineName = Environment.MachineName;
        protected TimeSpan Interval { get; set; } = TimeSpan.Zero;
        protected event EventHandler ReloadConfig;
        protected abstract string TaskName { get; }
        private string url;
        private IHubProxy hubProxy;
        private HubConnection connection;

        private System.Threading.Timer tmr;

        string logFileName;

        public TaskBase()
        {
            var logFileFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            if (Directory.Exists(logFileFolder) == false)
            {
                Directory.CreateDirectory(logFileFolder);
            }

            // delete older log, only keep 10 days
            Directory.GetFiles(logFileFolder, this.GetType().Name + "_????????.txt")
                .Select(file =>
                {
                    DateTime dt;
                    var safeFileName = Path.GetFileNameWithoutExtension(file);
                    var dateText = safeFileName.Split("_".ToCharArray()).Last().Trim();

                    if (DateTime.TryParseExact(dateText, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out dt) == false)
                    {
                        dt = DateTime.MinValue;
                    }
                    return new
                    {
                        FullName = file,
                        Day = dt,
                    };
                })
                .OrderByDescending(item => item.Day)
                .Skip(10)
                .ToList()
                .ForEach(item => File.Delete(item.FullName));

            System.Threading.TimerCallback setLogFileName = null;
            setLogFileName = (o) =>
            {
                logFileName = Path.Combine(logFileFolder, this.GetType().Name + "_" + DateTime.Now.ToString("yyyyMMdd") + ".txt");

                var tomorrowMillisionSecond = DateTime.Today.AddDays(1).Subtract(DateTime.Now).TotalMilliseconds;
                new System.Threading.Timer(setLogFileName, null, Convert.ToInt32(tomorrowMillisionSecond), -1);
            };

            new System.Threading.Timer(setLogFileName, null, 0, -1);

            while (string.IsNullOrWhiteSpace(this.logFileName) == true)
            {
                System.Threading.Thread.Sleep(500);
            }
            this.AppendLog(0, this.GetType().Name + " init");
        }

        public void Start(string url, string hubName)
        {
            this.url = url;
            this.connection = new HubConnection(this.url);
            this.connection.StateChanged += (a) =>
            {
                if (a.NewState == ConnectionState.Connected)
                {
                    this.hubProxy.Invoke("inHelloToOther", Environment.MachineName, this.TaskName);
                }
            };
            this.hubProxy = connection.CreateHubProxy(hubName);
            this.hubProxy.On<string>("onHello", (mn) => this.AppendLog(1, $"machine online: {mn}"));
            this.hubProxy.On<string>("onGoodbye", (mn) => this.AppendLog(1, $"machine offline: {mn}"));
            this.AddHandler();

            // 第一次就固定1秒後啟動
            tmr = new System.Threading.Timer(this.Timer_Tick, null, 1000, -1);

            while (true)
            {
                var command = Console.ReadLine();
                switch (command.ToLower())
                {
                    case "debug+":
                        this.EchoDebugInfo = true;
                        break;
                    case "debug-":
                        this.EchoDebugInfo = false;
                        break;
                    case "cls":
                        Console.Clear();
                        break;
                    case "quit":
                    case "exit":
                    case "stop":
                    case "end":
                    case "kill":
                        AppendLog(0, $"shutdown watch dog by console command");
                        return;
                    case "receive+":
                        if (this.ReceiveSignal == false)
                        {
                            this.ReceiveSignal = true;
                            Console.WriteLine();
                            AppendLog(0, $"start receiving signal");
                            Console.WriteLine();
                        }
                        break;
                    case "receive-":
                        if (this.ReceiveSignal == true)
                        {
                            this.ReceiveSignal = false;
                            Console.WriteLine();
                            AppendLog(0, $"stop receiving signal");
                            Console.WriteLine();
                        }
                        break;
                    case "reload":
                        this.ReloadConfig?.Invoke(this, EventArgs.Empty);
                        break;
                    case "?":
                        Console.WriteLine();
                        Console.WriteLine($"debug+                      => echo debug info");
                        Console.WriteLine($"debug-                      => hide debug info");
                        Console.WriteLine($"cls                         => clean console buffer");
                        Console.WriteLine($"quit, exit, stop, end, kill => shutdown watch dog");
                        Console.WriteLine($"quit, exit, stop, end, kill => shutdown watch dog");
                        Console.WriteLine($"receive+                    => start to handle others signal with same kind");
                        Console.WriteLine($"receive-                    => stop to handle others signal with same kind");
                        Console.WriteLine($"reload                      => ask program to reload app.config");
                        Console.WriteLine($"interval 1234               => set interval fix to 1234 second");
                        Console.WriteLine($"interval clear              => set interval to dynamic");
                        Console.WriteLine($"?                           => print this help");
                        this.PrintAdditionalCommandInfo();
                        Console.WriteLine();
                        break;
                        
                    default:

                        //下面是特別判斷，不是完全等於的文字，所以沒辦法放在case裡面
                        if (command.StartsWith("interval "))
                        {
                            var text = command.Substring(9);

                            if (text == "clear")
                            {
                                this.Interval = TimeSpan.Zero;
                                this.AppendLog(1, $"interval set to dynamic (decide by derrived class)");
                                this.tmr.Change(Convert.ToInt32(this.NextCheckPoint().TotalMilliseconds), -1);
                                break;
                            }
                            double value;
                            try
                            {
                                value = Convert.ToDouble(text);
                            }
                            catch (Exception)
                            {
                                this.AppendLog(1, $"can't parse {text} to numeric");
                                return;
                            }
                            this.Interval = TimeSpan.FromSeconds(value);
                            this.AppendLog(1, $"interval is fixed to {value} seconds");
                            this.tmr.Change(Convert.ToInt32(this.Interval.TotalMilliseconds), -1);
                            break;
                        }

                        // 基本指令都不認得，就交給繼承端判，如果有認得會return true
                        if (this.AdditionalCommandHandle(command) == true)
                        {
                            continue;
                        }

                        Console.WriteLine();
                        Console.WriteLine($"Unknow command: {command}");
                        Console.WriteLine();
                        break;
                }
            }
        }

        private void Timer_Tick(object context)
        {
            int ms = 1000;
            try
            {
                try
                {
                    this.EnsureSignalRConnection();
                    if (this.EchoDebugInfo)
                    {
                        AppendLog(0, "checking task runable");
                    }
                    if (this.TaskCheck() == true)
                    {
                        if (this.EchoDebugInfo)
                        {
                            AppendLog(0, "executing task");
                        }
                        this.DoTask();
                        if (this.EchoDebugInfo)
                        {
                            AppendLog(0, "finish");
                        }
                    }

                    if (this.Interval != TimeSpan.Zero)
                    {
                        ms = Convert.ToInt32(this.Interval.TotalMilliseconds);
                    }
                    else
                    {
                        var nextCheckPoint = this.NextCheckPoint();
                        ms = Convert.ToInt32(nextCheckPoint.TotalMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    AppendLog(0, "error: " + ex.Message);
                }
            }
            finally
            {
                this.tmr.Change(ms, -1);
            }
        }

        protected abstract bool TaskCheck();

        protected abstract TimeSpan NextCheckPoint();

        protected abstract void DoTask();

        protected abstract void AddHandler();

        protected virtual bool AdditionalCommandHandle(string command)
        {
            return false;
        }

        protected virtual void PrintAdditionalCommandInfo()
        {
        }

        protected void AddHandler(string signalName, Action action)
        {
            this.hubProxy.On(signalName, action);
        }
        protected void AddHandler<T>(string signalName, Action<T> action)
        {
            this.hubProxy.On<T>(signalName, action);
        }
        protected void AddHandler<T1, T2>(string signalName, Action<T1, T2> action)
        {
            this.hubProxy.On<T1, T2>(signalName, action);
        }
        protected void AddHandler<T1, T2, T3>(string signalName, Action<T1, T2, T3> action)
        {
            this.hubProxy.On<T1, T2, T3>(signalName, action);
        }
        protected void AddHandler<T1, T2, T3, T4>(string signalName, Action<T1, T2, T3, T4> action)
        {
            this.hubProxy.On<T1, T2, T3, T4>(signalName, action);
        }
        protected void AddHandler<T1, T2, T3, T4, T5>(string signalName, Action<T1, T2, T3, T4, T5> action)
        {
            this.hubProxy.On<T1, T2, T3, T4, T5>(signalName, action);
        }

        protected void SendSignal(string signalName, params object[] args)
        {
            this.EnsureSignalRConnection();
            this.hubProxy.Invoke(signalName, args);
        }

        private static readonly object _syncRoot = new object();

        protected void AppendLog(int intend, string msg)
        {
            var nowText = DateTime.Now.ToString("HH:mm:ss.fff");
            var firstLine = string.Empty;
            var intendSpaces = new string(' ', intend * 2);
            msg = string.Join("\r\n",
                msg
                    .Replace("\r", "\n")
                    .Replace("\n\n", "\n")
                    .Split('\n')
                    .Select(line => $"{(firstLine == string.Empty ? firstLine = nowText : new string(' ', 12))} {intendSpaces} {line}")
                    .ToArray()
                );

            lock (_syncRoot)
            {
                File.AppendAllText(this.logFileName, msg + "\r\n");
            }

            this.ConsoleWriteLine(msg);
        }

        protected void ConsoleWrite(string msg)
        {
            Console.Write(msg);
        }
        protected void ConsoleWriteLine()
        {
            Console.WriteLine();
        }
        protected void ConsoleWriteLine(string msg)
        {
            Console.WriteLine(msg);
        }

        private void EnsureSignalRConnection()
        {
            while (true)
            {
                switch (connection.State)
                {
                    case ConnectionState.Connected:
                        return;

                    case ConnectionState.Connecting:
                    case ConnectionState.Reconnecting:
                        System.Threading.Thread.Sleep(1000);
                        break;

                    case ConnectionState.Disconnected:
                        try
                        {
                            AppendLog(0, "SignalR Hub connecting...");
                            Task.Run(() => connection.Start().Wait());
                            System.Threading.Thread.Sleep(1000);
                            break;
                        }
                        catch (HttpRequestException)
                        {
                            AppendLog(0, "failed");
                            AppendLog(0, "Unable to connect to server: Start server before connecting clients.");
                            AppendLog(0, "retry in 5 seconds");
                            System.Threading.Thread.Sleep(5000);
                        }
                        break;
                }
            }
        }

        public void Dispose()
        {
            if (this.hubProxy != null)
            {
                this.hubProxy = null;
            }
            if (this.connection != null)
            {
                if (this.connection.State != ConnectionState.Disconnected)
                {
                    this.connection.Stop();
                }

                this.connection.Dispose();
                this.connection = null;
            }
        }
    }
}
