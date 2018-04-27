using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatchDogTaskLib;

namespace WatchDog.ProcessMonitor
{
    class Task : WatchDogTaskLib.TaskBase
    {
        private string[] processNames;

        protected override string TaskName
        {
            get
            {
                return "ProcessMonitor";
            }
        }

        public Task()
        {
            try
            {
                this.processNames = ((string)Config.Default["target"]).Split(',').Select(i => i.Trim()).ToArray();
            }
            catch
            {
            }

            this.ReloadConfig += (a, b) =>
            {
                try
                {
                    this.processNames = ((string)Config.Default["target"]).Split(',').Select(i => i.Trim()).ToArray();
                    base.ConsoleWriteLine($"config reloaded");
                }
                catch
                {
                }
            };
        }

        protected override bool TaskCheck()
        {
            return true;
        }

        protected override TimeSpan NextCheckPoint()
        {
            return TimeSpan.FromSeconds(10);
        }

        private Dictionary<int, PerformanceCounter> listOfCounter = new Dictionary<int, PerformanceCounter>();

        protected override void DoTask()
        {
            if (processNames == null || processNames.Length == 0)
            {
                base.ConsoleWriteLine();
                base.ConsoleWriteLine("\tno monitor target set");
                base.ConsoleWriteLine("\tplease use 'add target xxxxx' to add target");
                base.ConsoleWriteLine("\tor press ? and <enter> to see help");
                base.ConsoleWriteLine();
                return;
            }

            foreach (var processName in processNames)
            {
                var ps = Process.GetProcesses().Where(p => string.Compare(p.ProcessName, processName, true) == 0).ToList();

                var result = new WatchDogTaskLib.ProcessMonitor.ReportItem()
                {
                    ProcessName = processName,
                    Items = ps.AsParallel().Select(p =>
                    {
                        float cpuUsage = 0;
                        if (listOfCounter.ContainsKey(p.Id) == true && listOfCounter[p.Id].RawValue != p.Id)
                        {
                            listOfCounter[p.Id].Dispose();
                            listOfCounter.Remove(p.Id);
                        }

                        if (listOfCounter.ContainsKey(p.Id) == false)
                        {
                            var pcProcess = new PerformanceCounter("Process", "% Processor Time", p.ProcessName);
                            pcProcess.NextValue();
                            listOfCounter.Add(p.Id, pcProcess);
                            System.Threading.Thread.Sleep(1000);
                        }

                        cpuUsage = listOfCounter[p.Id].NextValue();

                        return new WatchDogTaskLib.ProcessMonitor.ReportItem.ReportItemDetail()
                        {
                            PID = p.Id,
                            MemoryUsage = p.PrivateMemorySize64,
                            CPUUsage = Convert.ToInt64(cpuUsage),
                        };
                    })
                    .AsSequential()
                    .ToArray()
                };

                base.SendSignal("inProcessReport", Environment.MachineName, result);
            }
        }

        protected override void AddHandler()
        {
            base.AddHandler<string, WatchDogTaskLib.ProcessMonitor.ReportItem>(
                "onProcessReport",
                (name, result) =>
                {
                    if (ReceiveSignal == true)
                    {
                        if (result.Items.Length == 0)
                        {
                            base.AppendLog(1, result.ProcessName + ": no instance");
                        }
                        else
                        {
                            base.AppendLog(1, name + ":\r\n" + string.Join("\r\n", result.Items.OrderBy(p => p.PID).Select((item, idx) => $"{(idx == 0 ? $"[{result.ProcessName}]" : new string(' ', result.ProcessName.Length + 2))} {item.PID.ToString().PadLeft(6)} CPU: {item.CPUUsage}% RAM: {Convert.ToInt32(item.MemoryUsage / (1024 * 1024))}MB")));
                        }
                    }
                });
        }

        protected override bool AdditionalCommandHandle(string command)
        {
            if (command == "list")
            {
                base.ConsoleWriteLine(string.Join("\r\n", Process.GetProcesses().Select(p => p.ProcessName).Distinct().OrderBy(name => name).ToArray()));
                return true;
            }
            else if (command.StartsWith("add target"))
            {
                var target = command.Substring(11);
                var lst = processNames.ToList();
                if (lst.Contains(target) == false)
                {
                    lst.Add(target);
                    processNames = lst.ToArray();
                    Config.Default["target"] = string.Join(",", lst.ToArray());
                    Config.Default.Save();
                    base.AppendLog(1, $"add {target} to monitor list.\r\n" + string.Join(", ", lst.ToArray()));
                }
                else
                {
                    base.ConsoleWriteLine($"{target} already exists in target list");
                }
                return true;
            }
            else if (command.StartsWith("remove target"))
            {
                var target = command.Substring(14);
                var lst = processNames.ToList();
                if (lst.Contains(target) == true)
                {
                    lst.Remove(target);
                    processNames = lst.ToArray();
                    Config.Default["target"] = string.Join(",", lst.ToArray());
                    Config.Default.Save();
                    base.AppendLog(1, $"remove {target} to monitor list.\r\n" + string.Join(", ", lst.ToArray()));
                }
                else
                {
                    base.ConsoleWriteLine($"{target} not exists in target list");
                }
                return true;
            }
            return false;
        }

        protected override void PrintAdditionalCommandInfo()
        {
            base.ConsoleWriteLine("list                => list all sorted process name");
            base.ConsoleWriteLine("add target xxxxx    => add xxxxx to the monitor list");
            base.ConsoleWriteLine("remove target xxxxx => add xxxxx to the monitor list");
        }
    }
}
