using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace AmbientOS.Utils
{
    /// <summary>
    /// Manages an action that must not be executed concurrently.
    /// </summary>
    class SynchronizedAction
    {
        AutoResetEvent notRunning = new AutoResetEvent(true);

        class Node<T> {
            public T Value { get; }
            public Node<T> Next { get; set; } = null;
            public Node(T value)
            {
                Value = value;
            }
        }

        Node<EventWaitHandle> waitingThreads = null;

        /// <summary>
        /// Blocks until an execution of the action finishes, that started after the call to Run.
        /// In other words, if the action is not yet running, the method runs it.
        /// If it is already running, the method waits until it finishes and then runs it again.
        /// If multiple calls wait at the same time to restart the action, it is only restarted once.
        /// </summary>
        public void Run(Action action)
        {
            EventWaitHandle wakeMeUp = null;

            if (!notRunning.WaitOne(0)) {
                // the action is already running, so we must ensure that it is started again
                // multiple threads can wait at the same time, in this case only one thread should execute the action
                using (wakeMeUp = new ManualResetEvent(false)) {
                    var node = new Node<EventWaitHandle>(wakeMeUp);
                    do {
                        node.Next = waitingThreads;
                    } while (Interlocked.CompareExchange(ref waitingThreads, node, node.Next) != node.Next);

                    // Is it possible, if both events are signaled at the same time, that the thread reacts to notRunning instead of wakeMeUp?
                    // in this case we'd have an unnecessary run (but no problems should occur)
                    if (WaitHandle.WaitAny(new WaitHandle[] { wakeMeUp, notRunning }) == 0)
                        return;
                }
            }

            // We're responsible to run the action, so we also have to take care of waking the threads that waited alongside this thread.
            var toBeWokenUp = Interlocked.Exchange(ref waitingThreads, null);

            try {
                action();
            } finally {
                for (var t = toBeWokenUp; t != null; t = t.Next)
                    if (t.Value != wakeMeUp)
                        t.Value.Set();
                notRunning.Set();
            }
        }
    }
}
