using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.Framework
{
    /// <summary>
    /// Helps building a standard command line tool that takes the first argument to select an action
    /// </summary>
    public class ArgsParser
    {
        /// <summary>
        /// Help text describing the usage.
        /// Arguments: {0} = command list
        /// </summary>
        public string UsageText { get; set; }
        /// <summary>
        /// Message shown for unknown commands.
        /// Arguments: {0} = command name
        /// </summary>
        public string UnknownCommandText { get; set; }
        /// <summary>
        /// Message shown if not enough arguments are provided.
        /// Arguments: {0} = command name, {1} = expected number of args
        /// </summary>
        public string NotEnoughArgumentsText { get; set; }
        /// <summary>
        /// Message shown if a command failed.
        /// Arguments: {0} = command name, {1} = exception message
        /// </summary>
        public string CommandFailedText { get; set; }
        /// <summary>
        /// The action that is executed prior to any command but only if the command line was parsed successfully
        /// </summary>
        public Action Preparation { get; set; }

        private class Command
        {
            public string HelpText;
            public string[] ParamNames;
            public Action<Dictionary<string, string>> Action;
        }

        private Dictionary<string, Command> commands = new Dictionary<string,Command>();


        public void AddCommand(string command, string helpText, Action<Dictionary<string, string>> action, params string[] paramNames)
        {
            commands[command.ToLower()] = new Command() {
                HelpText = helpText,
                ParamNames = paramNames,
                Action = action
            };
        }


        public bool Execute(IConsole console, string[] args)
        {
            Command cmd;

            if (args.Count() == 0) {
                PrintUsage(console);
                return false;
            }

            args[0] = args[0].TrimStart('-', '/').ToLower();

            if (!commands.TryGetValue(args[0], out cmd)) {
                console.WriteLine(string.Format(UnknownCommandText, args[0]), ConsoleColor.Yellow);
                PrintUsage(console);
                return false;
            }

            if (args.Count() <= cmd.ParamNames.Count()) {
                console.WriteLine(string.Format(NotEnoughArgumentsText, args[0], cmd.ParamNames.Count()), ConsoleColor.Yellow);
                PrintUsage(console);
                return false;
            }

            try {
                Preparation();
                Dictionary<string, string> cmdArgs = new Dictionary<string, string>();
                for (int i = 0; i < cmd.ParamNames.Count(); i++)
                    cmdArgs[cmd.ParamNames[i]] = args[i + 1];
                cmd.Action(cmdArgs);
            } catch (Exception ex) {
                console.WriteLine(string.Format(CommandFailedText, args[0], ex.ToString()), ConsoleColor.Red);
                return false;
            }

            return true;
        }

        private void PrintUsage(IConsole console)
        {
            console.WriteLine(string.Format(UsageText, string.Join("\n",
                Utilities.MergeColumns(from c in commands select new Tuple<string, string>(
                    " -" + c.Key + string.Join("", from p in c.Value.ParamNames select " [" + p + "]"),
                    c.Value.HelpText), ": ")
                    )));
        }
    }
}
