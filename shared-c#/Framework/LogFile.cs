using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AppInstall.Framework
{
    public class SafeQueue<T> : Queue<T>
    {
        private Mutex mutex = new Mutex(false);

        public SafeQueue(int capacity)
            : base(capacity)
        {
        }

        public SafeQueue()
        {
        }


        public new T Dequeue()
        {
            mutex.WaitOne();
            try {
                if (this.Any())
                    return base.Dequeue();
                return default(T);
            } finally {
                mutex.ReleaseMutex();
            }
        }

        public new void Enqueue(T item)
        {
            mutex.WaitOne();
            try {
                base.Enqueue(item);
            } finally {
                mutex.ReleaseMutex();
            }
        }

    }


    public class ProducerConsumerQueue<T> : SafeQueue<T>
    {
        private Semaphore freeSlots;
        private Semaphore usedSlots;
        private ManualResetEvent productionFinished = new ManualResetEvent(false);

        public ProducerConsumerQueue(int capacity)
            : base(capacity)
        {
            freeSlots = new Semaphore(capacity, capacity);
            usedSlots = new Semaphore(0, capacity);
        }

        /// <summary>
        /// Dequeues an item from the list.
        /// If neccessary, the call blocks until an item becomes available.
        /// If the producer signals the end of production, the function returns default(T)
        /// </summary>
        public new T Dequeue()
        {
            if (WaitHandle.WaitAny(new WaitHandle[] { usedSlots, productionFinished }) == 1)
                return default(T);
            T result = base.Dequeue();
            freeSlots.Release();
            return result;
        }

        public new void Enqueue(T item)
        {
            freeSlots.WaitOne();
            base.Enqueue(item);
            usedSlots.Release();
        }

        /// <summary>
        /// Signals to the consumer that no more items will become available.
        /// </summary>
        public void Terminate()
        {
            productionFinished.Set();
        }
    }


    public class LogFile : LogContext
    {
        private static LogDelegate ConstructLogDelegate(string path)
        {
            var pendingLines = new SafeQueue<string>();

            var flushAction = new SlowAction((c) => {
                using (var file = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) {
                    using (var stream = new StreamWriter(file)) {
                        string line;
                        while ((line = pendingLines.Dequeue()) != null)
                            stream.WriteLine(line);
                    }
                }
            });

            return (string context, string message, LogType type) => {
                pendingLines.Enqueue(message);
                flushAction.Trigger(ApplicationControl.ShutdownToken);
            };
        }


        public LogFile(string path)
            : base(ConstructLogDelegate(path), "")
        {
        }

    }
}
