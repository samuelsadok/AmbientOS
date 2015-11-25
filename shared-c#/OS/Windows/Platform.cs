using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.UI;
using AppInstall.Framework;
using AmbientOS.Environment;

namespace AppInstall.OS
{
    public static class Platform
    {
        /// <summary>
        /// Returns the current platform.
        /// </summary>
        public static PlatformType Type { get { return PlatformType.Windows; } }

        /// <summary>
        /// Returns the platform specific suffix for executable binaries (.exe for Windows)
        /// </summary>
        public static string ExecutableSuffix { get { return ".exe"; } }

        private static DispatcherThread mainThread = DispatcherThread.Create(true, new AmbientOS.Utils.TaskController());

        /// <summary>
        /// Executes a routine in the context of the main thread (in GUI apps this is the GUI thread). This does also work when already in the main thread.
        /// </summary>
        [Obsolete()]
        public static void InvokeMainThread(Action action)
        {
            mainThread.Invoke(action, new AmbientOS.Utils.TaskController());
        }

        /// <summary>
        /// Executes a routine in the context of the main thread (in GUI apps this is the GUI thread). This does also work when already in the main thread.
        /// </summary>
        [Obsolete()]
        public static T EvaluateOnMainThread<T>(Func<T> action)
        {
            return mainThread.Evaluate(action, new AmbientOS.Utils.TaskController());
        }
    }
}
