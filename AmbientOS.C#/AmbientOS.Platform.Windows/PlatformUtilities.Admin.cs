﻿
// this file requires a reference to System.ServiceProcess

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading.Tasks;
using AmbientOS.UI;
using static AmbientOS.LogContext;

namespace AmbientOS
{
    /// <summary>
    /// Provides advanced system functions for the Windows platform.
    /// These functions cannot be used in sandboxed (Windows Store) apps.
    /// </summary>
    static partial class PlatformUtilities
    {
        /// <summary>
        /// Returns true if the current process has admin privileges.
        /// </summary>
        public static bool RunningAsAdmin()
        {
            try {
                return new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            } catch (Exception) {
                return false;
            }
        }

        /// <summary>
        /// Restarts the application with administrator privileges.
        /// On failure, an exception is thrown.
        /// </summary>
        public static void RestartWithAdminPrivileges(string[] args)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if ((args?.Count() ?? 0) > 0)
                startInfo.Arguments = string.Join(" ", (from arg in args select "\"" + arg + "\""));
            startInfo.Verb = "runas";

            Process.Start(startInfo);
        }

        /// <summary>
        /// Restarts the application with administrator privileges.
        /// On failure, a message is displayed with the specified explanation.
        /// </summary>
        public static void RestartWithAdminPrivileges(string[] args, string explanation)
        {
            try {
                RestartWithAdminPrivileges(args);
                return;
            } catch (Exception ex) {
                Context.CurrentContext.Shell.Notify(
                    new Text() {
                        Summary = "You must grant administrator rights to this application",
                        Details = explanation,
                        Debug = ex.ToString()
                    },
                    Severity.Warning);
            }
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
            Log("terminating process " + name);

            do {
                //if (waitFirst)
                //    if (WaitForAllToExit(AllInstances(name), timeout * 1000))
                //        break;
                foreach (Process p in AllInstances(name))
                    p.Kill();
            } while (AllInstances(name).Any());

            Log("all processes terminated");
        }
        

        /// <summary>
        /// Stops a system service and blocks until it is terminated.
        /// </summary>
        /// <param name="name">the path of the executable or the service name</param>
        /// <param name="timeout">timeout in seconds to wait for the program to terminate</param>
        public static void TerminateService(string name, int timeout, LogContext log)
        {
            Log("terminating service " + name);

            using (ServiceController c = new ServiceController(name)) {
                if (c.Status == ServiceControllerStatus.StopPending) {
                    try {
                        c.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(timeout));
                    } catch (System.ServiceProcess.TimeoutException) {
                        Log("service did not shut down properly, forcing shutdown...", LogType.Warning);
                    }
                }
                if (c.Status != ServiceControllerStatus.Stopped) {
                    c.Stop();
                    c.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(timeout));
                }
            }

            Log("service terminated");
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
                if (AmbientOS.Utils.Utilities.PathsEqual(path, executable))
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
