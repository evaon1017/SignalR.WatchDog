using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatchDog.DBResponseMonitor
{
    class Task : WatchDogTaskLib.TaskBase
    {
        protected override string TaskName
        {
            get
            {
                return "DB Response Monitor";
            }
        }

        protected override void AddHandler()
        {
            base.AddHandler<string, string, string, string, double>("onDBTestReport", this.OnDBResponseReport);
        }

        protected override void DoTask()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sql");
            if (Directory.Exists(path) == false)
            {
                base.ConsoleWriteLine();
                base.ConsoleWriteLine("No sql folder exists. create one and put some .sql file in it to monitor");
                base.ConsoleWriteLine();
                return;
            }

            var cns = ConfigurationManager
                .ConnectionStrings
                .Cast<ConnectionStringSettings>()
                .ToDictionary(
                    item => item.Name, 
                    item => new System.Data.SqlClient.SqlConnection(item.ConnectionString));

            try
            {
                Directory.GetDirectories(path).ToList().ForEach(dir =>
                {
                    var folderName = new DirectoryInfo(dir).Name;
                    if (cns.ContainsKey(folderName) == false)
                    {
                        base.ConsoleWriteLine();
                        this.AppendLog(1, $"can't monitor folder: {folderName} due to no such connection setting in app.config file");
                        base.ConsoleWriteLine();
                        return;
                    }

                    Directory.GetFiles(dir, "*.sql").ToList().ForEach(file =>
                    {
                        var cn = cns[folderName];
                        if (cn.State != System.Data.ConnectionState.Open)
                        {
                            try
                            {
                                cn.Open();
                            }
                            catch (Exception ex)
                            {
                                this.AppendLog(1, $"can't open connection: {folderName} ({ex.Message})");
                            }
                        }

                        var fileName = Path.GetFileNameWithoutExtension(file);
                        using (var cm = cn.CreateCommand())
                        {
                            cm.CommandText = File.ReadAllText(file);
                            var tick = Environment.TickCount;
                            try
                            {
                                var result = cm.ExecuteScalar();
                                var diff = Environment.TickCount - tick;
                                this.SendSignal("inDBTestReport", this.machineName, folderName, fileName, result.ToString(), diff);
                            }
                            catch (Exception ex)
                            {
                                var diff = Environment.TickCount - tick;
                                this.SendSignal("inDBTestReport", this.machineName, folderName, fileName, ex.Message, diff);
                            }
                        }
                    });
                });
            }
            finally
            {
                cns.ToList()
                    .Select(pair => pair.Value)
                    .Where(cn => cn.State != System.Data.ConnectionState.Closed)
                    .ToList()
                    .ForEach(cn => cn.Close());
                cns.ToList()
                    .ForEach(pair => pair.Value.Dispose());
                cns.Clear();
            }
        }

        protected override TimeSpan NextCheckPoint()
        {
            return TimeSpan.FromSeconds(10);
        }

        protected override bool TaskCheck()
        {
            return true;
        }

        private void OnDBResponseReport(string machineName, string folderName, string fileName, string result, double executionMs)
        {
            base.ConsoleWriteLine();
            base.ConsoleWriteLine($"[{machineName}] - {folderName}\\{fileName}.sql ({executionMs}ms)\r\n{result}");
            base.ConsoleWriteLine();
        }
    }
}
