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
    public class Context
    {
        public string Name { get; set; }
        public IShell Shell { get; set; }
        public IEnvironment Environment { get; set; }
        public LogContext Log { get; set; }
        public TaskController Controller { get; set; }

        public Context SubContext(string name)
        {
            return new Context() {
                Name = name,
                Shell = Shell,
                Environment = Environment,
                Log = Log.SubContext(name),
                Controller = Controller
            };
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
    }
}
