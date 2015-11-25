using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.Utils;
using AmbientOS.Environment;
using AmbientOS.UI;

namespace AmbientOS
{
    public class Application
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public TaskController Controller { get; }
        private Action<Context> Task { get; set; }

        private Application()
        {

        }

        /// <summary>
        /// Initializes the specified action as the application action.
        /// This function should be invoked in the Main() function of the application after installing all of the services.
        /// </summary>
        public static void Init(string[] args, string name, string description, Action<Context> task)
        {
            var app = new Application() {
                Name = name,
                Description = description,
                Task = task
            };

            Context context = new Context() {
                Name = name,
                Controller = new TaskController(),
                Environment = new ForeignEnvironment().EnvironmentRef.Retain()
            };

            // if no interactive environment is available, run as a service
            if (!System.Environment.UserInteractive) {
                context.Log = new LogContext((c, m, t, controller) => { System.Diagnostics.Debug.WriteLine(c + ": " + m); }, null, name);
                context.UI = null;

                var service = new MainService();
                service.Application = app;
                service.Context = context;
                System.ServiceProcess.ServiceBase.Run(service);
                return;
            }

            // todo: support desktop apps
            var ui = new ConsoleUI(SystemConsole.Console);
            ui.Start(context.Controller);
            context.Log = ui.LogContext;
            context.UI = ui.UIRef.Retain();

            string needsAdminExplanation = null;
            var install = false;
            var uninstall = false;

            // check for special arguments
            if (args.Count() == 2) {
                if ((install = args[1].Trim() == "--install"))
                    needsAdminExplanation = "Installing a system service requires admin priviledges.";
                if ((uninstall = args[1].Trim() == "--uninstall"))
                    needsAdminExplanation = "Uninstalling a system service requires admin priviledges.";
            }

            // restart as admin if neccessary
            if (needsAdminExplanation != null && !PlatformUtilities.RunningAsAdmin()) {
                if (PlatformUtilities.RestartWithAdminPrivileges(args)) {
                    return;
                }
                context.UI.Notify(
                    new Text() {
                        Summary = "You must grant administrator rights to this application",
                        Details = needsAdminExplanation
                    },
                    Severity.Warning);
                return;
            }

            // handle (un)installation as a service
            if (install) {
                if (PlatformUtilities.IsOnNetworkDrive(PlatformUtilities.Assembly)) {
                    context.UI.Notify(
                        new Text() {
                            Summary = "The service executable must not be on a network drive",
                            Details = "Due to unexplicable limitations of Windows, services can't be installed when they're on a network drive. Copy the application to a local drive and try again."
                        },
                        Severity.Warning
                    );
                } else {
                    var answer = context.UI.PresentDialog(
                        new Text() {
                            Summary = name + " will be installed as a service in the system",
                            Details = "When installed as a service, the application will start in the background every time the computer starts, even when no user is logged in."
                        }, new Option() {
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
                        });
                    if (answer == 0) {
                        System.Configuration.Install.ManagedInstallerClass.InstallHelper(new string[] { System.Reflection.Assembly.GetExecutingAssembly().Location });
                        context.UI.Notify(
                            new Text() {
                                Summary = "Service installed successfully.",
                                Details = string.Format("The service was installed successfully under the name \"{0}\".", name)
                            }, Severity.Success
                        );
                    }
                }
                return;
            } else if (uninstall) {
                var answer = context.UI.PresentDialog(
                    new Text() {
                        Summary = name + " will be installed as a service in the system",
                        Details = "When installed as a service, the application will start in the background every time the computer starts, even when no user is logged in."
                    }, new Option() {
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
                    });
                if (answer == 0) {
                    System.Configuration.Install.ManagedInstallerClass.InstallHelper(new string[] { "/u", System.Reflection.Assembly.GetExecutingAssembly().Location });
                    context.UI.Notify(
                        new Text() {
                            Summary = "Service uninstalled successfully.",
                            Details = string.Format("The service \"{0}\" was uninstalled successfully.", name)
                        }, Severity.Success
                    );
                }
                return;
            }
            
            app.Run(context);
            context.Controller.CancellationHandle.WaitOne();

            //try {
            //   ApplicationControl.Start(args);
            //   new Task(() => {
            //       SystemConsole.Console.Pause();
            //       ApplicationControl.Shutdown();
            //   }).Start();
            //   ApplicationControl.ShutdownToken.WaitHandle.WaitOne();
            //} catch (Exception ex) {
            //    SystemConsole.Console.WriteLine(ex.ToString(), AmbientOS.Environment.ConsoleColor.Red);
            //}
        }

        internal void Run(Context context)
        {
            Task(context);
            context.Controller.Cancel();
        }
    }
}
