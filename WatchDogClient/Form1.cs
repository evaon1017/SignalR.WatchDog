
using Microsoft.AspNet.SignalR.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.Security.Policy;

namespace WatchDogClient
{
    public partial class Form1 : Form
    {
        private IHubProxy HubProxy { get; set; }
        private HubConnection Connection { get; set; }

        public class SignalRInputEventArg : EventArgs
        {
            public object[] parameters { get; set; }
        }

        public event EventHandler<SignalRInputEventArg> SignalIn;

        public Form1()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.ConnectAsync();
            this.LoadAssembly();
        }

        private void ConnectAsync()
        {
            while (true)
            {
                if (this.IsDisposed)
                {
                    return;
                }

                var serverURL = Server.Default.ServerURL;
                Connection = new HubConnection(serverURL);
                HubProxy = Connection.CreateHubProxy("ServerHub");
                //Handle incoming event from server: use Invoke to write to console from SignalR's thread
                this.AddHandler();
                try
                {
                    this.AppendLogC("remote hub connecting...");
                    Connection.Start();
                    this.AppendLog("success");
                    break;
                }
                catch (HttpRequestException)
                {
                    this.AppendLog("failed");
                    this.AppendLog("Unable to connect to server: Start server before connecting clients.");

                    this.AppendLog("retry in 5 seconds");
                    System.Threading.Thread.Sleep(5000);
                }
            }
        }

        private void LoadAssembly()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tasks");
            if (Directory.Exists(path) == false)
            {
                this.AppendLog($"folder not found: {path}");
                return;
            }

            var allDlls = Directory.GetFiles(path, "WatchDog*.dll");
            if (allDlls.Any() == false)
            {
                this.AppendLog($"no assembly can be load");
                return;
            }

            allDlls.ToList().ForEach(file =>
            {
                var adevidence = AppDomain.CurrentDomain.Evidence;
                var folder = Path.GetDirectoryName(file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                var fileName2 = Path.GetFileName(file);

                var appSetup = new AppDomainSetup();
                appSetup.ApplicationBase = folder;
                var domain = AppDomain.CreateDomain(fileName, null, appSetup);

                //domain.AssemblyResolve += new ResolveEventHandler(MyAssemblyResolver);
                //domain.TypeResolve += new ResolveEventHandler(MyTypeResolver);
                List<string> loaddedAssemblyNames = new List<string>();
                try
                {
                    Action<AssemblyName> loadAssembly = null;
                    loadAssembly = (an) =>
                    {
                        if (loaddedAssemblyNames.Contains(an.FullName))
                        {
                            return;
                        }
                        try
                        {
                            this.AppendLogC($"try loading {an.FullName} ...");
                            domain.Load(an);
                            loaddedAssemblyNames.Add(an.FullName);
                            this.AppendLog($"loadded");
                        }
                        catch (Exception ex)
                        {
                            this.AppendLog($"failed with error: {ex.Message}");
                            var ar = Assembly.ReflectionOnlyLoad(an.FullName);
                            ar.GetReferencedAssemblies().ToList().ForEach(ar2 =>
                            {
                                loadAssembly(ar2);
                            });
                            this.AppendLogC($"try loading {an.FullName} ...");
                            domain.Load(an);
                            loaddedAssemblyNames.Add(an.FullName);
                            this.AppendLog($"loadded");
                        }
                    };
                    var rel = Assembly.ReflectionOnlyLoadFrom(file);
                    var rootAssemblyName = rel.GetName();
                    loadAssembly(rootAssemblyName);

                    var lst = domain.GetAssemblies().SelectMany(asb => asb.GetTypes().Where(tp => tp.GetInterfaces().Any(itf => itf == typeof(WatchDogTaskLib.TaskBase)))).ToList();

                    lst.ForEach(tp =>
                    {
                        this.AppendLog($"initializing type: {tp.FullName}");
                        var page = new TabPage();
                        page.Text = domain.FriendlyName + " " + tp.Name;
                        var task = (WatchDogTaskLib.TaskBase)domain.CreateInstanceAndUnwrap(tp.AssemblyQualifiedName, tp.Name);
                        page.Tag = task;
                        task.Log = this.AppendLog;
                        task.LogC = this.AppendLogC;

                        task.Start(this.HubProxy);
                    });
                }
                catch (Exception ex)
                {
                    this.AppendLog($"failed");
                }
            });
        }

        private Assembly MyTypeResolver(object sender, ResolveEventArgs args)
        {
            var aName = new AssemblyName(args.Name);
            var fullName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tasks", aName.Name + ".dll");
            if (File.Exists(fullName) == true)
            {
                byte[] bs = System.IO.File.ReadAllBytes(fullName);
                return ((AppDomain)sender).Load(bs);
            }
            else
            {
                return null;
            }
        }

        static Assembly MyAssemblyResolver(object sender, ResolveEventArgs args)
        {
            var aName = new AssemblyName(args.Name);
            var fullName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tasks", aName.Name + ".dll");
            if (File.Exists(fullName) == true)
            {
                byte[] bs = System.IO.File.ReadAllBytes(fullName);
                return ((AppDomain)sender).Load(bs);
            }
            else
            {
                return null;
            }
        }

        private void AddHandler()
        {
            this.HubProxy.On<string, long, long>(
                "onSystemResourceReport", 
                (name, cpu, ram) => this.AppendLog($"[{name}] CPU: {cpu}%, RAM: {ram}MB"));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            this.Connection.Dispose();
            base.OnFormClosing(e);
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void SignalExecute(string mathodName, params object[] parameters)
        {
            if (mathodName == "onSystemResourceReport")
            {
                var name = parameters[0];
                var cpu = parameters[1];
                var ram = parameters[2];
                this.AppendLog($"[{name}] CPU: {cpu}%, RAM: {ram}MB");
            }

            this.SignalIn(this, new SignalRInputEventArg() { parameters = parameters });
        }
    }
}
