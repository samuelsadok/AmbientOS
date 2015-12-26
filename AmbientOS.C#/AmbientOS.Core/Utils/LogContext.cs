using System;
using System.Collections.Generic;
using AmbientOS.UI;
using AmbientOS.Utils;

namespace AmbientOS
{
    public enum LogType
    {
        Debug,
        Info,
        Success,
        Warning,
        Error
    }


    public delegate void LogDelegate(string context, string message, LogType type, TaskController controller);

    /// <summary>
    /// todo: make this an interface
    /// </summary>
    public class LogContext
    {
        Dictionary<string, LogContext> children = new Dictionary<string, LogContext>();
        LogDelegate logDelegate;
        Action<TaskController> breakDelegate;
        string name;

        /// <summary>
        /// Creates a new log context from a log delegate
        /// </summary>
        /// <param name="logDelegate">The delegate that accepts a log message and writes it to the output.</param>
        /// <param name="breakDelegate">An action that inserts a break (e.g. new line) on the log context. If null, a break equals an empty log entry.</param>
        /// <param name="name">The root name of the context (e.g. the application name).</param>
        public LogContext(LogDelegate logDelegate, Action<TaskController> breakDelegate, string name)
        {
            this.name = name;
            this.logDelegate = logDelegate;
            this.breakDelegate = breakDelegate == null ? (controller) => logDelegate("", "", LogType.Info, controller) : breakDelegate;
        }

        public static LogContext FromConsole(Action<string, UI.ConsoleColor, UI.ConsoleColor, TaskController> writeDelegate, string name)
        {
            return new LogContext((c, m, t, controller) => {
                var color = UI.ConsoleColor.DefaultForeground;
                switch (t) {
                    case LogType.Debug: color = UI.ConsoleColor.DarkGray; break;
                    case LogType.Info: color = UI.ConsoleColor.Green; break;
                    case LogType.Success: color = UI.ConsoleColor.Green; break;
                    case LogType.Warning: color = UI.ConsoleColor.Yellow; break;
                    case LogType.Error: color = UI.ConsoleColor.Red; break;
                }
                writeDelegate(c + ": " + m + "\n", color, UI.ConsoleColor.DefaultBackground, controller);
            }, (controller) => {
                writeDelegate("\n", UI.ConsoleColor.DefaultForeground, UI.ConsoleColor.DefaultBackground, controller);
            }, name);
        }

        /// <summary>
        /// Writes a log message to the context.
        /// </summary>
        public void Log(string message, LogType type = LogType.Info, TaskController controller = null)
        {
            logDelegate(name, message, type, controller ?? new TaskController());
        }

        /// <summary>
        /// Writes a log message to the context using the debug type.
        /// </summary>
        public void Debug(string message, params object[] args)
        {
            logDelegate(name, string.Format(message, args), LogType.Debug, new TaskController());
        }

        /// <summary>
        /// Inserts a break in the log.
        /// On consoles this is an empty line.
        /// On other log contexts, this may have no effect.
        /// </summary>
        public void Break(TaskController controller = null)
        {
            breakDelegate(controller ?? new TaskController());
        }

        /// <summary>
        /// Hooks into this log context by letting a hook function create a new log delegate from the old one.
        /// </summary>
        /// <param name="hook">A function that provides a new log delegate. The new log delegate should call the old one to forward the log message.</param>
        public void Hook(Func<LogDelegate, LogDelegate> hook)
        {
            logDelegate = hook(logDelegate);
        }

        /// <summary>
        /// Creates a subcontext of this context
        /// </summary>
        public LogContext SubContext(string name)
        {
            LogContext result;
            if (!children.TryGetValue(name, out result))
                result = (children[name] = new LogContext((c, m, t, controller) => { logDelegate(this.name + "->" + c, m, t, controller); }, breakDelegate, name));
            return result;
        }
    }
}