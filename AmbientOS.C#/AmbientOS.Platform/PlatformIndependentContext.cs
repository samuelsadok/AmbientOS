using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.UI;
using AmbientOS.Environment;

namespace AmbientOS.Platform
{
    static class PlatformIndependentContext
    {
        /// <summary>
        /// Initializes the environment in a platform independent way, using the console as the means of user interaction.
        /// If no console is present, user interaction is not possible. todo: search for a shell/environment in the object store
        /// </summary>
        public static void Initialize(string[] args, IEnvironment environment, string appTitle, string appDescription)
        {
            var controller = new TaskController();
            
            var haveInputStream = Console.OpenStandardInput(1) != System.IO.Stream.Null;

            if (haveInputStream) {
                var ui = new ConsoleUI(SystemConsole.Console);
                Context.Setup(ui.ShellRef, environment, ui.LogContext, controller);
                ui.Start();
            } else {
                var log = new LogContext((c, m, t) => { System.Diagnostics.Debug.WriteLine(c + ": " + m); }, null, appTitle);
                Context.Setup(null, environment, log, controller); // bad idea: use dummy UI
            }
        }
    }
}
