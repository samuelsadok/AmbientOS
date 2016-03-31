using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using AmbientOS.UI;
using AmbientOS.Environment;

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
            // note that on native AmbientOS, this assembly is not loaded, so this function is irrelevant

            switch (System.Environment.OSVersion.Platform) {
                case PlatformID.Win32NT: case PlatformID.Win32S: case PlatformID.Win32Windows: case PlatformID.WinCE: return PlatformType.Windows;
                case PlatformID.MacOSX: return PlatformType.OSX; // todo: consider iOS
                case PlatformID.Unix: return PlatformType.Linux; // todo: consider Android
                default: return PlatformType.Unspecified;
            }
        }


        /// <summary>
        /// This is the entry point that should be directly called by the Main function of each executable assembly.
        /// It will search for other assemblies named with "AmbientOS.Platform.*.dll" and try to load and init them.
        /// </summary>
        public static void Init(string[] args)
        {
            // retrieve details about the main app that is launching
            var mainApp = GetMainApp();
            var name = mainApp.Name;
            var description = mainApp.Description;


            // create an initial platform independent context
            var initialPlatform = new PlatformIndependentContext();
            var context = initialPlatform.Init(args, name, description, null);


            // load all platform specific assemblies that are in the same folder as the executing assembly
            var location = Assembly.GetExecutingAssembly().Location;
            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(location), "AmbientOS.Platform.*.dll", SearchOption.AllDirectories)) {
                try {
                    var assembly = Assembly.LoadFrom(file);
                    context.Log.Log(string.Format("loaded assembly {0}", assembly), LogType.Debug);
                } catch (Exception ex) {
                    context.Log.Log(string.Format("could not load assembly {0}: {1}", file, ex.Message), LogType.Warning);
                }
            }


            // handle special purpose launches
            //context.Log.Log(string.Join(", ", args.Select(a => string.Format("[{0}]", a))), LogType.Info);
            var install = args.Select(arg => arg.Trim()).Contains("--install");
            var uninstall = args.Select(arg => arg.Trim()).Contains("--uninstall");
            var verb = install ? "install" : uninstall ? "uninstall" : "launch";
            var launchMode = install ? LaunchMode.Install : uninstall ? LaunchMode.Uninstall : LaunchMode.Launch;


            // for each valid platform type, try to invoke the constructor
            var availablePlatforms = new List<IPlatform>() { initialPlatform };
            var unavailablePlatforms = new List<Tuple<Type, Exception>>();

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.ExportedTypes)
                .Where(t => typeof(IPlatform).IsAssignableFrom(t))
                .ToArray();

            foreach (var t in types) {
                try {
                    var constructor = t.GetMethod("CreateInstance");
                    if (constructor == null)
                        continue;

                    var safeConstructor = (Func<LaunchMode, IPlatform>)Delegate.CreateDelegate(typeof(Func<LaunchMode, IPlatform>), constructor);
                    var p = safeConstructor(launchMode);

                    if (p == null)
                        throw new Exception("The platform constructor returned null");

                    availablePlatforms.Add(p);
                } catch (Exception ex) {
                    unavailablePlatforms.Add(new Tuple<Type, Exception>(t, ex));
                }
            }

            context.Log.Log(string.Format("available platforms: {0}", availablePlatforms.Any() ? string.Join(", ", availablePlatforms.Select(p => p == null ? "(null)" : string.Format("{0} ({1})", p.GetType(), p.Text.Summary))) : "none"), LogType.Debug);
            context.Log.Log(string.Format("unavailable platforms: {0}", unavailablePlatforms.Any() ? string.Join(", ", unavailablePlatforms.Select(p => p == null ? "(null)" : string.Format("{0} ({1})", p.Item1, p.Item2.Message))) : "none"), LogType.Debug);


            IPlatform platform;

            if (availablePlatforms.Count() > 1) {
                var answer = context.UI.PresentDialog(new Text() {
                    Summary = string.Format("You are about to {0} {1}. In which mode do you want {0} {1}?", verb, name),
                    Details = string.Format("There are multiple environments available and it's not clear which one you prefer."),
                },
                availablePlatforms.Select(p => new Option() {
                    Text = p.Text,
                    Level = p.GetType() == initialPlatform.GetType() ? Level.Recommended : Level.Easy
                }).Concat(new Option[] {
                    new Option() {
                        Text = new Text() {
                            Summary = "Abort",
                            Details = string.Format("Don't {0} {1}", verb, name)
                        },
                        Level = Level.Escape
                    }
                }).ToArray()
                );

                if (answer >= availablePlatforms.Count()) {
                    context.Controller.Cancel();
                    return;
                }

                platform = availablePlatforms[answer];

            } else {
                platform = availablePlatforms.First();
            }

            if (install) {
                platform.Install(args, name, description, context);
            } else if (uninstall) {
                platform.Uninstall(args, name, description, context);
            } else {
                context = platform.Init(args, name, description, context);

                var serviceTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly => assembly.ExportedTypes)
                    .Where(t => t.GetCustomAttribute<AOSServiceAttribute>() != null)
                    .ToArray();

                foreach (var t in serviceTypes) {
                    var restrictions = t.GetCustomAttributes<ForPlatformAttribute>().ToArray();
                    if (restrictions.Any())
                        if (!restrictions.Any(attr => attr.Type == GetPlatform()))
                            continue;

                    try {
                        ApplicationRegistry.InstallService(t);
                        context.Log.Log(string.Format("installed service {0}", t), LogType.Debug);
                    } catch (Exception ex) {
                        context.Log.Log(string.Format("failed to install service {0}: {1} ", t, ex.Message), LogType.Error);
                    }
                }


                mainApp.Run(context);
            }
        }
    }
}
