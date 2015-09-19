using System;
using System.Collections.Generic;

namespace AppInstall.Framework
{
    //public static class LogSystem
    //{
    //    public enum MessageType
    //    {
    //        Debug,
    //        Info,
    //        Warning,
    //        Error
    //    }
    //
    //    public static void Log(string message, MessageType messageType = MessageType.Info)
    //    {
    //        switch (messageType) {
    //            case MessageType.Debug: AppInstall.OS.Console.SetColor(AppInstall.OS.Console.ConsoleColor.DarkGray, AppInstall.OS.Console.ConsoleColor.Black); break;
    //            case MessageType.Info: AppInstall.OS.Console.SetColor(AppInstall.OS.Console.ConsoleColor.Green, AppInstall.OS.Console.ConsoleColor.Black); break;
    //            case MessageType.Warning: AppInstall.OS.Console.SetColor(AppInstall.OS.Console.ConsoleColor.Yellow, AppInstall.OS.Console.ConsoleColor.Black); break;
    //            case MessageType.Error: AppInstall.OS.Console.SetColor(AppInstall.OS.Console.ConsoleColor.Red, AppInstall.OS.Console.ConsoleColor.Black); break;
    //        }
    //        AppInstall.OS.Console.WriteLine(message);
    //    }
    //
    //    //public static void Log(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
    //    //{
    //    //    if (logCaller)
    //    //        Platform.PrintLine(message + " (at " + method + " in " + file + ":" + line + ")");
    //    //    else
    //    //        Platform.PrintLine(message);
    //    //}
    //}


    public enum LogType
    {
        Debug,
        Info,
        Warning,
        Error
    }


    public delegate void LogDelegate(string context, string message, LogType type);

    public class LogContext
    {
        Dictionary<string, LogContext> children = new Dictionary<string, LogContext>();
        LogDelegate logDelegate;
        string name;

        public LogContext(LogDelegate logDelegate, string name)
        {
            this.name = name;
            this.logDelegate = logDelegate;
        }

        public static LogContext FromConsole(IConsole console, string name)
        {
            return new LogContext((c, m, t) => {
                switch (t) {
                    case LogType.Debug: console.SetColor(ConsoleColor.DarkGray, ConsoleColor.Black); break;
                    case LogType.Info: console.SetColor(ConsoleColor.Green, ConsoleColor.Black); break;
                    case LogType.Warning: console.SetColor(ConsoleColor.Yellow, ConsoleColor.Black); break;
                    case LogType.Error: console.SetColor(ConsoleColor.Red, ConsoleColor.Black); break;
                }
                console.WriteLine(c + ": " + m);
            }, name);
        }

        public void Log(string message, LogType type = LogType.Info)
        {
            logDelegate(name, message, type);
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
                result = (children[name] = new LogContext((c, m, t) => { logDelegate(this.name + "->" + c, m, t); }, name));
            return result;
        }
    }
}