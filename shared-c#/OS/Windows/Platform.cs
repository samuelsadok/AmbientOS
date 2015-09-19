using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.UI;
using AppInstall.Framework;

namespace AppInstall.OS
{
    public static class Platform
    {
        /// <summary>
        /// Returns the current platform.
        /// </summary>
        public static PlatformType Type { get { return PlatformType.Windows; } }

        /// <summary>
        /// Returns a temp folder on the system
        /// </summary>
        public static string TempFolder { get { return Path.GetTempPath(); } }
        /// <summary>
        /// Returns the platform specific suffix for executable binaries (.exe for Windows)
        /// </summary>
        public static string ExecutableSuffix { get { return ".exe"; } }


        public static LogContext DefaultLog { get { return Environment.UserInteractive ? consoleLog : debugLog; } }
        private static LogContext consoleLog = LogContext.FromConsole(SystemConsole.Console, "root");
        private static LogContext debugLog = new LogContext((c, m, t) => { System.Diagnostics.Debug.WriteLine(c + ": " + m); }, Application.ApplicationName);

        private static DispatcherThread mainThread = DispatcherThread.Create(true);

        /// <summary>
        /// Executes a routine in the context of the main thread (in GUI apps this is the GUI thread). This does also work when already in the main thread.
        /// </summary>
        public static void InvokeMainThread(Action action)
        {
            mainThread.Invoke(action);
        }

        /// <summary>
        /// Executes a routine in the context of the main thread (in GUI apps this is the GUI thread). This does also work when already in the main thread.
        /// </summary>
        public static T EvaluateOnMainThread<T>(Func<T> action)
        {
            return mainThread.Evaluate(action);
        }
    }
}
