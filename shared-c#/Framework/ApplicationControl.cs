using System;
using System.IO;
using System.Threading;

namespace AppInstall.Framework
{
    static class ApplicationControl
    {

        /// <summary>
        /// Returns the path including name of the executing assembly
        /// </summary>
        [Obsolete("use PlatformUtilities.Assembly")]
        public static string ApplicationBinaryPath { get { return System.Reflection.Assembly.GetExecutingAssembly().Location; } }
        /// <summary>
        /// Returns the directory where the executing assembly is located
        /// </summary>
        [Obsolete("use PlatformUtilities.Assembly.GetParent")]
        public static string ApplicationBinaryDirectory { get { return Path.GetDirectoryName(ApplicationBinaryPath); } }
        /// <summary>
        /// Returns the parent directory of where the executing assembly is located
        /// </summary>
        [Obsolete("use PlatformUtilities.Assembly")]
        public static string ApplicationPath { get { return Path.GetFullPath(ApplicationBinaryDirectory + "\\.."); } }
        /// <summary>
        /// Indicates if this application is running as a system service
        /// </summary>
        public static bool IsSystemService { get { return !Environment.UserInteractive; } }
        /// <summary>
        /// Returns path of the user independent configuration file
        /// </summary>
        [Obsolete()]
        public static string CommonConfigPath { get { return IsSystemService ? ApplicationBinaryDirectory + "\\config.xml" : Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\APPLICATION\\config.xml"; } }
        /// <summary>
        /// Returns the path of a user and application specific folder where the application has write access.
        /// </summary>
        public static string AppDataPath { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
        /// <summary>
        /// Returns the path to an application specific folder that can be used for caching.
        /// The contents of this folder can dissappear.
        /// </summary>
        public static string CachePath { get { return Environment.GetFolderPath(Environment.SpecialFolder.InternetCache); } }
        /// <summary>
        /// Returns path of the user specific configuration file
        /// </summary>
        [Obsolete()]
        public static string UserConfigPath { get { return IsSystemService ? CommonConfigPath : ApplicationPath + "\\APPLICATION\\config.xml"; } }

        /// <summary>
        /// When the application is started as a service, this property will by set to the service name at startup.
        /// </summary>
        public static string ServiceName { get; set; }

        /// <summary>
        /// The application must listen to this event and terminate all threads when it is triggered.
        /// </summary>
        //public static CancellationToken ShutdownToken { get; private set; }
        //private static CancellationTokenSource shutdownTokenSource;
        //
        ///// <summary>
        ///// Shuts down the application in a controlled manner.
        ///// </summary>
        //public static void Shutdown()
        //{
        //    shutdownTokenSource.Cancel();
        //}
    }
}