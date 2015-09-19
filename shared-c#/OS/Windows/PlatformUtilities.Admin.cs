
// this file requires a reference to System.ServiceProcess

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.OS
{
    /// <summary>
    /// Provides advanced system functions for the Windows platform.
    /// These functions cannot be used in sandboxed apps
    /// </summary>
    static partial class PlatformUtilities
    {
        /// <summary>
        /// Restarts the application with administrator privileges if neccessary. Returns true if the application should quit.
        /// </summary>
        public static bool RestartWithAdminPrivileges(string[] args, IConsole console)
        {
            try {
                if (new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                    return false;
            } catch (Exception) {
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (args != null)
                if (args.Count() > 0)
                    startInfo.Arguments = string.Join(" ", (from arg in args select arg.Contains(' ') ? "\"" + arg + "\"" : arg));
            startInfo.Verb = "runas";
            try {
                Process.Start(startInfo);
            } catch (System.ComponentModel.Win32Exception) {
                console.WriteLine("You must grant administrator rights to this application", Framework.ConsoleColor.Yellow);
                console.WaitForInput();
            }

            return true;
        }


        /// <summary>
        /// Launches a process.
        /// </summary>
        /// <param name="name">the path of the executable</param>
        public static void StartProcess(string name)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo() {
                FileName = name,
                UseShellExecute = false
            };

            using (Process p = new Process() { StartInfo = startInfo }) { // todo: verify intended effect
                p.Start();
            }
        }

        /// <summary>
        /// Launches a system service.
        /// </summary>
        /// <param name="name">the service name</param>
        public static void StartService(string name)
        {
            using (ServiceController c = new ServiceController(name))
                c.Start();
        }

        /// <summary>
        /// Kills all instances of a program and blocks until they are terminated.
        /// </summary>
        /// <param name="name">the path of the executable</param>
        public static void TerminateProcess(string name, LogContext log)
        {
            log.Log("terminating process " + name);

            do {
                //if (waitFirst)
                //    if (WaitForAllToExit(AllInstances(name), timeout * 1000))
                //        break;
                foreach (Process p in AllInstances(name))
                    p.Kill();
            } while (AllInstances(name).Any());

            log.Log("all processes terminated");
        }
        

        /// <summary>
        /// Stops a system service and blocks until it is terminated.
        /// </summary>
        /// <param name="name">the path of the executable or the service name</param>
        /// <param name="timeout">timeout in seconds to wait for the program to terminate</param>
        public static void TerminateService(string name, int timeout, LogContext log)
        {
            log.Log("terminating service " + name);

            using (ServiceController c = new ServiceController(name)) {
                if (c.Status == ServiceControllerStatus.StopPending) {
                    try {
                        c.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(timeout));
                    } catch (System.ServiceProcess.TimeoutException) {
                        log.Log("service did not shut down properly, forcing shutdown...", LogType.Warning);
                    }
                }
                if (c.Status != ServiceControllerStatus.Stopped) {
                    c.Stop();
                    c.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(timeout));
                }
            }

            log.Log("service terminated");
        }

        public static IEnumerable<Process> AllInstances(string executable)
        {
            foreach (Process p in Process.GetProcesses()) {
                string path;
                try {
                    path = p.MainModule.FileName;
                } catch (Exception) { // we cannot access all processes
                    continue;
                }
                if (Utilities.PathsEqual(path, executable))
                    yield return p;
            }
            yield break;
        }

        public static bool WaitForAllToExit(IEnumerable<Process> processes, int milliseconds)
        {
            var tasks = (from process in processes select new Task<bool>(() => process.WaitForExit(milliseconds))).ToArray();
            Task.WaitAll(tasks);
            return tasks.All((t) => t.Result);
        }
    }
}
