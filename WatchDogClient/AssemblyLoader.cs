using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WatchDogClient
{
    public class AssemblyLoader : MarshalByRefObject
    {
        public AssemblyInfo LoadAssemlyInfo(string assemblyPath)
        {
            Assembly assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);
            return new AssemblyInfo(assembly);
        }
    }

    [Serializable]
    public class AssemblyInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string RuntimeVersion { get; set; }
        public string Platform { get; set; }

        public AssemblyInfo()
        {
        }

        public AssemblyInfo(AssemblyName assemblyName)
        {
            Name = assemblyName.Name;
            Version = assemblyName.Version.ToString();
            RuntimeVersion = "";
            Platform = assemblyName.ProcessorArchitecture.ToString();
        }

        public AssemblyInfo(Assembly assembly)
            : this(assembly.GetName())
        {
            RuntimeVersion = assembly.ImageRuntimeVersion;
        }
    }
}
