using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.Utils;
using AmbientOS.Environment;
using AmbientOS.UI;
using AmbientOS.Platform;

namespace AmbientOS.Platform
{
    public static class WindowsServicePlatform
    {
        public static void Initialize(string[] args, IEnvironment environment, string appTitle, string appDescription)
        {
            // Note that the actual service is governed by a different lifecycle than the context in which it is initialized.
            var controller = new TaskController(TaskState.Inactive);
            var log = new LogContext((c, m, t) => { System.Diagnostics.Debug.WriteLine(c + ": " + m); }, null, appTitle);

            Context.Setup(null, environment, log, controller);

            var service = new WindowsService(controller);
            System.ServiceProcess.ServiceBase.Run(service);
        }


        public static void Install(string[] args, string appTitle, string appDescription)
        {
            // restart as admin if neccessary
            if (!PlatformUtilities.RunningAsAdmin()) {
                PlatformUtilities.RestartWithAdminPrivileges(args, "Installing a system service requires admin priviledges.");
                return;
            }

            if (PlatformUtilities.IsOnNetworkDrive(PlatformUtilities.Assembly)) {
                Context.CurrentContext.Shell.Notify(
                    new Text() {
                        Summary = "The service executable must not be on a network drive",
                        Details = "Due to unexplicable limitations of Windows, services can't be installed when they're on a network drive. Copy the application to a local drive and try again."
                    },
                    Severity.Warning
                );
                return;
            }

            var answer = Context.CurrentContext.Shell.PresentDialog(
                new Text() {
                    Summary = appTitle + " will be installed as a service in the system",
                    Details = "When installed as a service, the application will start in the background every time the computer starts, even when no user is logged in."
                }, new Option[] { new Option() {
                    Text = new Text() {
                        Summary = "OK",
                        Details = "Install the service"
                    },
                    Level = Level.Recommended
                }, new Option() {
                    Text = new Text() {
                        Summary = "Cancel",
                        Details = "Don't install the service"
                    },
                    Level = Level.Easy
                } });

            if (answer == 0) {
                System.Configuration.Install.ManagedInstallerClass.InstallHelper(new string[] { System.Reflection.Assembly.GetEntryAssembly().Location });
                Context.CurrentContext.Shell.Notify(
                    new Text() {
                        Summary = "Service installed successfully.",
                        Details = string.Format("The service was installed successfully under the name \"{0}\".", appTitle)
                    }, Severity.Success
                );
            }
        }

        public static void Uninstall(string[] args, string appTitle, string appDescription)
        {
            // restart as admin if neccessary
            if (!PlatformUtilities.RunningAsAdmin()) {
                PlatformUtilities.RestartWithAdminPrivileges(args, "Uninstalling a system service requires admin priviledges.");
                return;
            }

            var answer = Context.CurrentContext.Shell.PresentDialog(
                new Text() {
                    Summary = appTitle + " will be installed as a service in the system",
                    Details = "When installed as a service, the application will start in the background every time the computer starts, even when no user is logged in."
                }, new Option[] { new Option() {
                    Text = new Text() {
                        Summary = "OK",
                        Details = "Uninstall the service"
                    },
                    Level = Level.Recommended
                }, new Option() {
                    Text = new Text() {
                        Summary = "Cancel",
                        Details = "Leave the service installed for now"
                    },
                    Level = Level.Easy
                } });

            if (answer == 0) {
                System.Configuration.Install.ManagedInstallerClass.InstallHelper(new string[] { "/u", System.Reflection.Assembly.GetEntryAssembly().Location });
                Context.CurrentContext.Shell.Notify(
                    new Text() {
                        Summary = "Service uninstalled successfully.",
                        Details = string.Format("The service \"{0}\" was uninstalled successfully.", appTitle)
                    }, Severity.Success
                );
            }
        }
    }
}
