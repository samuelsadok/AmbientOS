using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.Environment;
using AmbientOS.UI;
using AmbientOS.Utils;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AmbientOS
{
    public class Context : IDisposable
    {
        private static List<Context> bootContexts = new List<Context>();

        private int isDisposed = 0;
        private Context Parent { get; }

        private bool initialized;
        private IShell shell;
        private IEnvironment environment;
        private LogContext logContext;
        private readonly TaskController controller;

        public IShell Shell { get { if (!initialized) DelayedSetup(); return shell; } }
        public IEnvironment Environment { get { if (!initialized) DelayedSetup(); return environment; } }
        public LogContext LogContext { get { if (!initialized) DelayedSetup(); return logContext; } }
        public TaskController Controller { get { return controller; } }

        public Context(bool bootContext)
        {
            initialized = (isSetup != 0);

            shell = DefaultShell;
            environment = DefaultEnvironment;
            logContext = DefaultLog;
            controller = DefaultController;
        }

        public Context()
            : this(false)
        {
        }

        // public Context(TaskController controller)
        //     : this()
        // {
        //     this.controller = controller;
        // }

        public Context(Context parent, string name)
        {
            Parent = parent;
            initialized = true;
            shell = parent.Shell;
            environment = parent.Environment;
            logContext = parent.LogContext.SubContext(name);
            controller = parent.Controller;
        }

        private void DelayedSetup()
        {
            if (isSetup == 0)
                throw new Exception("No context is set up yet.");

            lock (this) {
                if (!initialized) {
                    shell = DefaultShell;
                    environment = DefaultEnvironment;
                    logContext = DefaultLog;
                    initialized = true;
                }
            }
        }


        private static int isSetup = 0;
        private static IShell DefaultShell { get; set; } = null;
        private static IEnvironment DefaultEnvironment { get; set; } = null;
        private static LogContext DefaultLog { get; set; } = null;
        private static TaskController DefaultController { get; set; } = new TaskController();

        public static void Setup(IShell shell, IEnvironment environment, LogContext log, TaskController controller)
        {
            if (Interlocked.Exchange(ref isSetup, 1) != 0)
                throw new Exception("The context can only be set up once.");

            DefaultShell = shell.Retain();
            DefaultEnvironment = environment.Retain();
            DefaultLog = log;
            DefaultController = controller;
        }

        void IDisposable.Dispose()
        {
            if (Interlocked.Exchange(ref isDisposed, 1) != 0)
                return;

            if (Parent == null)
                throw new Exception("Cannot dispose topmost context.");

            CurrentContext = Parent;
        }



        static readonly ConditionalWeakTable<Thread, Context> contexts = new ConditionalWeakTable<Thread, Context>();

        /// <summary>
        /// Returns the context that is associated with the current thread.
        /// </summary>
        public static Context CurrentContext
        {
            get
            {
                return contexts.GetOrCreateValue(Thread.CurrentThread);
            }
            set
            {
                contexts.Add(Thread.CurrentThread, value);
            }
        }

        /// <summary>
        /// Returns the context of the specified thread.
        /// </summary>
        public static Context GetContext(Thread thread)
        {
            return contexts.GetOrCreateValue(thread);
        }

        /// <summary>
        /// Associates the specified thread with the same context as the current thread.
        /// </summary>
        /// <param name="thread"></param>
        public static void ForwardContext(Thread thread)
        {
            contexts.Add(thread, CurrentContext);
        }

        public static Context EnterSubContext(string name)
        {
            return CurrentContext = new Context(CurrentContext, name);
        }
    }
}
