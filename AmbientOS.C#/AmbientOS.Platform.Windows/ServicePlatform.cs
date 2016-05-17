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
    public class ServicePlatform : IPlatform
    {
        public Text Text { get; } = new Text() { Summary = "Windows System Service", Details = "The application is started when the computer starts and runs in the background, even when no user is logged in." };

        public static IPlatform CreateInstance(LaunchMode launchMode)
        {
            // if an interactive environment is available, we're not being ran as a service
            if (launchMode == LaunchMode.Launch && System.Environment.UserInteractive)
                throw new InvalidOperationException("To use system service mode, you must run this program as a service.");

            return new ServicePlatform();
        }

        public Context Init(string[] args, DynamicProperty<string> name, DynamicProperty<string> description, Context preContext)
        {
            Context context = new Context() {
                Name = name.GetValue(),
                Controller = new TaskController(TaskState.Inactive),
                Environment = new ForeignEnvironment().EnvironmentRef.Retain(),
                Log = new LogContext((c, m, t) => { System.Diagnostics.Debug.WriteLine(c + ": " + m); }, null, name.GetValue()),
                Shell = null
            };

            var service = new MainService() {
                Controller = context.Controller
            };
            System.ServiceProcess.ServiceBase.Run(service);
            return context;
        }

        public void Install(string[] args, DynamicProperty<string> name, DynamicProperty<string> description, Context context)
        {
            // restart as admin if neccessary
            if (!PlatformUtilities.RunningAsAdmin()) {
                PlatformUtilities.RestartWithAdminPrivileges(args, "Installing a system service requires admin priviledges.", context);
                return;
            }

            if (PlatformUtilities.IsOnNetworkDrive(PlatformUtilities.Assembly)) {
                context.Shell.Notify(
                    new Text() {
                        Summary = "The service executable must not be on a network drive",
                        Details = "Due to unexplicable limitations of Windows, services can't be installed when they're on a network drive. Copy the application to a local drive and try again."
                    },
                    Severity.Warning
                );
                return;
            }

            var answer = context.Shell.PresentDialog(
                new Text() {
                    Summary = name + " will be installed as a service in the system",
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
                context.Shell.Notify(
                    new Text() {
                        Summary = "Service installed successfully.",
                        Details = string.Format("The service was installed successfully under the name \"{0}\".", name)
                    }, Severity.Success
                );
            }
        }

        public void Uninstall(string[] args, DynamicProperty<string> name, DynamicProperty<string> description, Context context)
        {
            // restart as admin if neccessary
            if (!PlatformUtilities.RunningAsAdmin()) {
                PlatformUtilities.RestartWithAdminPrivileges(args, "Uninstalling a system service requires admin priviledges.", context);
                return;
            }

            var answer = context.Shell.PresentDialog(
                new Text() {
                    Summary = name + " will be installed as a service in the system",
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
                context.Shell.Notify(
                    new Text() {
                        Summary = "Service uninstalled successfully.",
                        Details = string.Format("The service \"{0}\" was uninstalled successfully.", name)
                    }, Severity.Success
                );
            }
        }
    }
}
