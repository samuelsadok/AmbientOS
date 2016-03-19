using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.UI;
using AmbientOS.Environment;

namespace AmbientOS.Platform
{
    class PlatformIndependentContext : IPlatform
    {
        public Text Text { get; } = new Text() { Summary = "Console mode", Details = "The application runs in a console window" };

        public static IPlatform CreateInstance(LaunchMode launchMode)
        {
            if (launchMode == LaunchMode.Install || launchMode == LaunchMode.Uninstall)
                throw new NotSupportedException("cannot (un)install this app in console mode");

            return new PlatformIndependentContext();
        }

        public Context Init(string[] args, DynamicProperty<string> name, DynamicProperty<string> description, Context preContext)
        {
            if (preContext != null)
                return preContext;

            Context context = new Context() {
                Name = name.GetValue(),
                Controller = new TaskController(),
                Environment = new ForeignEnvironment().EnvironmentRef.Retain()
            };

            bool haveInputStream = Console.OpenStandardInput(1) != System.IO.Stream.Null;

            if (haveInputStream) {
                var ui = new ConsoleUI(SystemConsole.Console);
                ui.Start(context);
                context.Log = ui.LogContext;
                context.UI = ui.UIRef.Retain();
            } else {
                context.Log = new LogContext((c, m, t, controller) => { System.Diagnostics.Debug.WriteLine(c + ": " + m); }, null, name.GetValue());
                context.UI = null; // bad idea: use dummy UI
            }

            return context;
        }

        public void Install(string[] args, DynamicProperty<string> name, DynamicProperty<string> description, Context context)
        {
        }

        public void Uninstall(string[] args, DynamicProperty<string> name, DynamicProperty<string> description, Context context)
        {
        }
    }
}
