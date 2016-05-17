using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using AmbientOS.UI;
using AmbientOS.Environment;
using static AmbientOS.LogContext;

namespace AmbientOS.Platform
{
    public static class Platform
    {
        /// <summary>
        /// Instantiates and returns the main application of the entry assembly (i.e. the assembly that was started initially).
        /// </summary>
        public static IApplication GetMainApp()
        {
            var mainApp = Assembly.GetEntryAssembly().ExportedTypes
                .Where(t => typeof(IApplicationImpl).IsAssignableFrom(t) && t.GetCustomAttribute<AOSMainApplicationAttribute>() != null);

            if (mainApp.Count() != 1)
                return null;

            var app = (IApplicationImpl)Activator.CreateInstance(mainApp.Single());
            return app.ApplicationRef.Retain();
        }

        public static PlatformType GetPlatform()
        {
            switch (System.Environment.OSVersion.Platform) {
                case PlatformID.Win32NT: case PlatformID.Win32S: case PlatformID.Win32Windows: case PlatformID.WinCE: return PlatformType.Windows;
                case PlatformID.MacOSX: return PlatformType.OSX; // todo: consider iOS
                case PlatformID.Unix: return PlatformType.Linux; // todo: consider Android
                default: return PlatformType.Unspecified; // todo: consider native AmbientOS
            }
        }

        public static PlatformType Type { get; private set; } = GetPlatform();

        /// <summary>
        /// This is the entry point that should be directly called by the Main function of each executable assembly.
        /// It initializes the AmbientOS environment on all supported platforms.
        /// </summary>
        public static void Initialize(string[] args)
        {
            // retrieve details about the application that is launching
            var appTitle = Assembly.GetEntryAssembly().GetTitle("Unnamed AmbientOS Service");
            var appDescription = Assembly.GetEntryAssembly().GetDescription("(no description available)");

            // handle special purpose launches
            var install = args.Select(arg => arg.Trim()).Contains("--install");
            var uninstall = args.Select(arg => arg.Trim()).Contains("--uninstall");
            var verb = install ? "install" : uninstall ? "uninstall" : "launch";
            var launchMode = install ? LaunchMode.Install : uninstall ? LaunchMode.Uninstall : LaunchMode.Launch;

            // precedence: CLI > GUI > Service
            var forceCLI = args.Select(arg => arg.Trim()).Contains("--mode=cli");
            var forceGUI = args.Select(arg => arg.Trim()).Contains("--mode=gui");
            var forceService = args.Select(arg => arg.Trim()).Contains("--mode=service");
            
            var interactive = System.Environment.UserInteractive;
            var haveInputStream = Console.OpenStandardInput(1) != Stream.Null;

            forceCLI |= haveInputStream && interactive;
            forceGUI |= interactive;

            // the foreign environment is used by all platforms other than native AmbientOS
            var environment = new ForeignEnvironment().EnvironmentRef;

            switch (Type) {
                case PlatformType.Windows:

                    if (forceCLI)
                        goto default;
                    else if (forceGUI)
                        goto default; // todo: init Windows GUI Platform
                    else
                        WindowsServicePlatform.Initialize(args, environment, appTitle, appDescription);

                    break;

                case PlatformType.OSX:
                    Type = PlatformType.OSX;

                    if (forceCLI)
                        goto default;
                    else if (forceGUI)
                        goto default; // todo: init OSX/iOS GUI Platform
                    else
                        goto default; // todo: init OSX/iOS service Platform

                //break;

                case PlatformType.Linux:
                    Type = PlatformType.Linux;

                    if (forceCLI)
                        goto default;
                    else if (forceGUI)
                        goto default; // todo: init Linux GUI Platform
                    else
                        goto default; // todo: init Linux service Platform

                //break;

                default:
                    // create a platform independent context (uses console)
                    PlatformIndependentContext.Initialize(args, environment, appTitle, appDescription);
                    break;
            }


            if (install) {
                if (Type != PlatformType.Windows)
                    throw new Exception("Services are currently only supported on Windows.");
                WindowsServicePlatform.Install(args, appTitle, appDescription);
                System.Environment.Exit(0);
            } else if (uninstall) {
                if (Type != PlatformType.Windows)
                    throw new Exception("Services are currently only supported on Windows.");
                WindowsServicePlatform.Uninstall(args, appTitle, appDescription);
                System.Environment.Exit(0);

            }
        }

        /// <summary>
        /// Terminates the context in which the method is called.
        /// If called in the application's Main function, the application shuts down and the process terminates.
        /// </summary>
        public static void Exit()
        {
            Context.CurrentContext.Controller.Cancel();
        }
    }
}
