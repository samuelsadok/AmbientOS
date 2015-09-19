using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AppInstall.OS
{
    
    public class DispatcherThread
    {
        private Dispatcher dispatcher;

        private DispatcherThread(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        /// <summary>
        /// Starts a new dispatcher thread that will be ready to process messages.
        /// </summary>
        public static DispatcherThread Create(bool forUI)
        {
            Dispatcher dispatcher = null;

            ManualResetEvent readySignal = new ManualResetEvent(false);
            Thread t = new Thread(() => {
                dispatcher = Dispatcher.CurrentDispatcher;
                readySignal.Set();
                System.Windows.Threading.Dispatcher.Run();
            });

            if (forUI)
                t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            readySignal.WaitOne();

            return new DispatcherThread(dispatcher);
        }

        /// <summary>
        /// Determines if the caller is already on this dispatcher thread.
        /// </summary>
        private bool OnThread { get { return dispatcher.Thread == Thread.CurrentThread; } }

        /// <summary>
        /// Executes a routine in the context of the dispatcher thread. This does also work when already in the dispatcher thread.
        /// </summary>
        public void Invoke(Action action)
        {
            Application.UILog.Log("dispatcher " + dispatcher.Thread.ManagedThreadId + " invoked from " + Thread.CurrentThread.ManagedThreadId);
            if (OnThread) { action(); return; }

            ManualResetEvent doneSignal = new ManualResetEvent(false);
            Action newAction = () => { action(); doneSignal.Set(); };
            dispatcher.BeginInvoke(newAction);
            doneSignal.WaitOne();
        }

        /// <summary>
        /// Executes a routine in the context of the dispatcher thread. This does also work when already on the dispatcher thread.
        /// </summary>
        public T Evaluate<T>(Func<T> action)
        {
            if (OnThread) return action();

            T result = default(T);
            Invoke(() => result = action());
            return result;
        }
    }
}
