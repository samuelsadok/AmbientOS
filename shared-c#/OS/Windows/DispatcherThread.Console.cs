using System;
using System.Collections.Generic;
using System.Threading;
using AmbientOS.Utils;

namespace AppInstall.OS
{
    
    public class DispatcherThread
    {
        private Thread thread;
        private Queue<Tuple<Action, AutoResetEvent>> actions = new Queue<Tuple<Action, AutoResetEvent>>();
        private Semaphore actionsQueuedSignal = new Semaphore(0, int.MaxValue);

        private DispatcherThread(TaskController controller)
        {
            thread = new Thread(() => {
                while (WaitHandle.WaitAny(new WaitHandle[] { controller.CancellationHandle, actionsQueuedSignal }) != 0) {
                    Tuple<Action, AutoResetEvent> action;
                    lock (actions) action = actions.Dequeue();
                    action.Item1.Invoke();
                    action.Item2.Set();
                }
            });
        }

        private void Start(bool forUI)
        {
            if (forUI)
                thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }
        

        /// <summary>
        /// Starts a new dispatcher thread that will be ready to process messages.
        /// </summary>
        public static DispatcherThread Create(bool forUI, TaskController controller)
        {
            var thread = new DispatcherThread(controller);
            thread.Start(forUI);
            return thread;
        }

        /// <summary>
        /// Determines if the caller is already on this dispatcher thread.
        /// </summary>
        private bool OnThread { get { return thread == Thread.CurrentThread; } }

        /// <summary>
        /// Executes a routine in the context of the dispatcher thread. This does also work when already in the dispatcher thread.
        /// </summary>
        public void Invoke(Action action, TaskController controller)
        {
            if (OnThread) { action(); return; }

            using (AutoResetEvent doneSignal = new AutoResetEvent(false)) {
                lock (actions) actions.Enqueue(new Tuple<Action, AutoResetEvent>(action, doneSignal));
                actionsQueuedSignal.Release();
                WaitHandle.WaitAny(new WaitHandle[] { controller.CancellationHandle, doneSignal });
                controller.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// Executes a routine in the context of the dispatcher thread. This does also work when already on the dispatcher thread.
        /// </summary>
        public T Evaluate<T>(Func<T> function, TaskController controller)
        {
            if (OnThread) return function();

            T result = default(T);
            Invoke(() => result = function(), controller);
            return result;
        }
    }
}
