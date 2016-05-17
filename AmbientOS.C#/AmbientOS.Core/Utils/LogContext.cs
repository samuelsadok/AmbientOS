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


    public delegate void LogDelegate(string context, string message, LogType type);

    /// <summary>
    /// todo: make this an interface
    /// </summary>
    public class LogContext
    {
        Dictionary<string, LogContext> children = new Dictionary<string, LogContext>();
        LogDelegate logDelegate;
        Action breakDelegate;
        string name;

        /// <summary>
        /// Creates a new log context from a log delegate
        /// </summary>
        /// <param name="logDelegate">The delegate that accepts a log message and writes it to the output.</param>
        /// <param name="breakDelegate">An action that inserts a break (e.g. new line) on the log context. If null, a break equals an empty log entry.</param>
        /// <param name="name">The root name of the context (e.g. the application name).</param>
        public LogContext(LogDelegate logDelegate, Action breakDelegate, string name)
        {
            this.name = name;
            this.logDelegate = logDelegate;
            this.breakDelegate = breakDelegate == null ? () => logDelegate("", "", LogType.Info) : breakDelegate;
        }

        public static LogContext FromConsole(Action<string, UI.ConsoleColor, UI.ConsoleColor> writeDelegate, string name)
        {
            return new LogContext((c, m, t) => {
                var color = UI.ConsoleColor.DefaultForeground;
                switch (t) {
                    case LogType.Debug: color = UI.ConsoleColor.DarkGray; break;
                    case LogType.Info: color = UI.ConsoleColor.Green; break;
                    case LogType.Success: color = UI.ConsoleColor.Green; break;
                    case LogType.Warning: color = UI.ConsoleColor.Yellow; break;
                    case LogType.Error: color = UI.ConsoleColor.Red; break;
                }
                writeDelegate(c + ": " + m + "\n", color, UI.ConsoleColor.DefaultBackground);
            }, () => {
                writeDelegate("\n", UI.ConsoleColor.DefaultForeground, UI.ConsoleColor.DefaultBackground);
            }, name);
        }

        /// <summary>
        /// Writes a log message to the context.
        /// </summary>
        public void LogEx(string message, LogType type)
        {
            logDelegate(name, message, type);
        }

        /// <summary>
        /// Writes a log message to the current context.
        /// </summary>
        public static void Log(string message, LogType type = LogType.Info)
        {
            Context.CurrentContext.LogContext.LogEx(message, type);
        }

        /// <summary>
        /// Writes a log message to the context using the debug type.
        /// </summary>
        public void Debug(string message, params object[] args)
        {
            logDelegate(name, string.Format(message, args), LogType.Debug);
        }

        /// <summary>
        /// Writes a log message to the current context using the debug type.
        /// </summary>
        public static void DebugLog(string message, params object[] args)
        {
            Context.CurrentContext.LogContext.LogEx(string.Format(message, args), LogType.Debug);
        }

        /// <summary>
        /// Inserts a break in the log.
        /// On consoles this is an empty line.
        /// On other log contexts, this may have no effect.
        /// </summary>
        public void Break()
        {
            breakDelegate();
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
                result = (children[name] = new LogContext((c, m, t) => { logDelegate(this.name + "->" + c, m, t); }, breakDelegate, name));
            return result;
        }
    }
}