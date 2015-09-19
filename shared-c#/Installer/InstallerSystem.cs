using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.Installer
{
    public class InstallerSystem
    {
        private const string CONFIG_CHANNEL = "update/channel";
        private const string CONFIG_TARGET_VERSION = "update/targetVersion";
        private const string CONFIG_INSTALLER = "update/installer";
        private const string CONFIG_STATE = "update/state";
        private const string CONFIG_STATE_DEFAULT = "up to date";
        private const string CONFIG_STATE_PREPARING = "preparing";
        private const string CONFIG_STATE_READY = "ready";
        private const string CONFIG_STATE_STARTED = "started";


        /// <summary>
        /// Indicates if an update was downloaded and is now ready for installation.
        /// </summary>
        public bool IsUpdatePending {
            get { return isUpdatePending; }
            private set
            {
                bool oldPending = isUpdatePending;
                 isUpdatePending = value;
                 if (!oldPending && isUpdatePending)
                     try {
                         UpdatePrepared.SafeInvoke();
                     } catch (Exception) {
                         logContext.Log("failed to signal the availability of an update", LogType.Warning);
                         isUpdatePending = false; // try again
                     }
            }
        }
        private bool isUpdatePending;

        /// <summary>
        /// Triggerd once when a new update was prepared
        /// </summary>
        public event Action UpdatePrepared;

        private CancellationTokenSource periodicCheckCancellation;
        private SlowAction updateCheck;
        private LogContext logContext;

        /// <summary>
        /// Determines the current software package number that uniquely identifies application, version and platform.
        /// </summary>
        public static Guid GetPackageID()
        {
            Guid version;
            using (StreamReader s = new StreamReader(ApplicationControl.ApplicationBinaryDirectory + "\\version"))
                if (!Guid.TryParse(s.ReadLine(), out version))
                    throw new Exception("the current version could not be determined");
            return version;
        }

        /// <summary>
        /// Checks online for an update and downloads it if one is found.
        /// </summary>
        private async Task CheckForUpdateEx(CancellationToken cancellationToken)
        {
            if (IsUpdatePending) { // stop the periodic checking once we've found an update
                if (periodicCheckCancellation != null)
                    periodicCheckCancellation.Cancel();
                return;
            }

            try {

                logContext.Log("update check...");
                var client = new SoftwareDistributionClient(logContext.SubContext("web client"));
                string channel = Config.CommonConfig[CONFIG_CHANNEL];
                var update = await client.GetUpdateScript(GetPackageID(), channel == null ? "default" : channel, cancellationToken);
                logContext.Log("check done");
                if (update == null)
                    return; // we have the latest version


                string installerFolder = Platform.TempFolder + "\\" + Guid.NewGuid();
                // set the context for the installer
                update.Context = new InstallerContext() {
                    ApplicationName = (ApplicationControl.IsSystemService ? ApplicationControl.ServiceName : Application.ApplicationName),
                    ApplicationPath = ApplicationControl.ApplicationPath,
                    ApplicationBinaryPath = ApplicationControl.ApplicationBinaryPath,
                    IsSystemService = ApplicationControl.IsSystemService,
                    InstallerFolder = installerFolder,
                    TerminateApplication = true,
                    RelaunchApplication = true,
                    SoftwareServerClient = client,
                    LogContext = logContext
                };

                logContext.Log("found update, install folder: " + installerFolder);

                Utilities.CreateDirectory(installerFolder);

                try {

                    // prepare download
                    string installerPath = update.Context.InstallerFolder + "\\installer" + Platform.ExecutableSuffix;
                    Config.CommonConfig[CONFIG_TARGET_VERSION] = update.PackageID.ToString();
                    Config.CommonConfig[CONFIG_STATE] = CONFIG_STATE_PREPARING;
                    Config.CommonConfig[CONFIG_INSTALLER] = installerPath;
                    Config.CommonConfig.Save();

                    // download files
                    using (FileStream file = File.Create(installerPath))
                        await file.Write(await client.DownloadFile(update.UpdaterGuid, cancellationToken), cancellationToken);
                    logContext.Log("preparing update...");
                    update.Prepare(cancellationToken);
                    logContext.Log("preparation complete");

                    // write script to disk
                    using (FileStream file = File.Create(update.Context.InstallerFolder + "\\script.xml"))
                        Utilities.XMLSerialize(update, file);

                    Config.CommonConfig[CONFIG_STATE] = CONFIG_STATE_READY;
                    Config.CommonConfig.Save();
                    IsUpdatePending = true;
                } catch {
                    logContext.Log("removing update folder...");
                    Directory.Delete(installerFolder, true);
                }

            } catch (Exception ex) {
                logContext.Log("checking for updates failed:" + ex.Message, LogType.Warning);
            }
        }
        

        /// <summary>
        /// Erases traces of a previous update, sets the IsUpdatePending flag if an update is pending
        /// and starts the periodic update check if desired. This function returns immediately.
        /// </summary>
        /// <param name="updateCheckInterval">the periodic update interval in seconds (-1 disables this feature)</param>
        /// <param name="cancellationToken">cancels the periodic update check (can be null if not checking periodically)</param>
        /// <param name="autoShutdown">If true, the application is shut down and restarted automatically when an update becomes ready</param>
        public void Init(int updateCheckInterval, bool autoShutdown, LogContext logContext, CancellationToken cancellationToken)
        {
            Task.Run(() => {
                try {
                    this.logContext = logContext;

                    if (autoShutdown)
                        UpdatePrepared += InitiateUpdate;

                    if (Config.CommonConfig[CONFIG_STATE] == CONFIG_STATE_READY) {
                        logContext.Log("update ready for installation");
                        IsUpdatePending = true;
                    } else {
                        // log result
                        if (Config.CommonConfig[CONFIG_STATE] == CONFIG_STATE_STARTED)
                            if (Config.CommonConfig[CONFIG_TARGET_VERSION] == GetPackageID().ToString())
                                logContext.Log("update completed successfully", LogType.Warning);
                            else // todo: implement update lock (e.g. lock for 10min) so that there isn't an infinite update loop
                                logContext.Log("update installation failed: should have updated to " + Config.CommonConfig[CONFIG_TARGET_VERSION] + " but have " + GetPackageID().ToString(), LogType.Warning);
                        if (Config.CommonConfig[CONFIG_STATE] == CONFIG_STATE_PREPARING)
                            logContext.Log("previous download was not completed", LogType.Warning);

                        // clear update state
                        Config.CommonConfig[CONFIG_STATE] = CONFIG_STATE_DEFAULT;
                        Config.CommonConfig.Save();
                        try {
                            if (Config.CommonConfig[CONFIG_INSTALLER] != null)
                                Directory.Delete(Directory.GetParent(Config.CommonConfig[CONFIG_INSTALLER]).FullName, true);
                        } catch (Exception ex) {
                            logContext.Log("could not clean up update: " + ex, LogType.Warning);
                        }
                    };

                    updateCheck = new SlowAction((c) => CheckForUpdateEx(c).Wait());
                    if (updateCheckInterval > -1) {
                        periodicCheckCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        updateCheck.TriggerPeriodically(updateCheckInterval * 1000, periodicCheckCancellation.Token);
                    }
                } catch (Exception ex) {
                    logContext.Log("could not init update client: " + ex.ToString(), LogType.Warning);
                    throw;
                }
            });
        }

        /// <summary>
        /// Checks online for an update and downloads it if one is found.
        /// Returns true if an update was found.
        /// </summary>
        public bool CheckForUpdate(CancellationToken cancellationToken)
        {
            updateCheck.TriggerAndWait(cancellationToken).Wait();
            return IsUpdatePending;
        }

        /// <summary>
        /// Starts installation of the pending update and initiates shutdown of the application.
        /// </summary>
        public void InitiateUpdate()
        {
            Config.CommonConfig[CONFIG_STATE] = CONFIG_STATE_STARTED;
            Config.CommonConfig.Save();
            PlatformUtilities.StartProcess(Config.CommonConfig[CONFIG_INSTALLER]);
            //ApplicationControl.Shutdown(); // we should let the installer terminate the application (in case something goes wrong with launching the installer)
            // todo: shutdown only after installer has started (use pipes)
            logContext.Log("shutdown for update pending");
        }

        /// <summary>
        /// Downloads and installs the latest version of the specified application
        /// </summary>
        /// <param name="channel">can be null</param>
        public static async Task InstallApplication(string targetFolder, string application, string channel, string platform, LogContext logContext, CancellationToken cancellationToken)
        {
            string installerFolder = Platform.TempFolder + "\\" + Guid.NewGuid();

            try {
                Utilities.CreateDirectory(installerFolder);
                logContext.Log("install folder: " + installerFolder);

                var client = new SoftwareDistributionClient(logContext.SubContext("web client"));
                logContext.Log("fetching install script...");
                InstallerScript script = await client.GetInstallScript(application, channel == null ? "default" : channel, platform, cancellationToken);

                // set the context for the installer
                script.Context = new InstallerContext() {
                    ApplicationName = application,
                    ApplicationPath = targetFolder,
                    ApplicationBinaryPath = targetFolder + "\\bin\\app.exe", // todo: don't assume this
                    IsSystemService = false, // todo: properly install system services
                    InstallerFolder = installerFolder,
                    TerminateApplication = false,
                    RelaunchApplication = false,
                    SoftwareServerClient = client,
                    LogContext = logContext
                };

                // prepare installation
                logContext.Log("preparing installation...");
                logContext.Log("actions: " + script.Actions.Count());
                script.Prepare(cancellationToken);

                // execute installation
                logContext.Log("executing installation...");
                script.Execute();
                using (FileStream file = File.Open(Directory.GetParent(script.Context.ApplicationBinaryPath) + "\\version", FileMode.OpenOrCreate, FileAccess.Write))
                    await file.Write(script.PackageID.ToString(), cancellationToken);

            } finally {
                // clean up
                logContext.Log("cleaning up...");
                Directory.Delete(installerFolder, true);
            }
        }
    }
}
