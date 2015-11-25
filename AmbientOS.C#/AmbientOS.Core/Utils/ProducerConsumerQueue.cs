using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace AmbientOS.Utils
{
    /// <summary>
    /// Adds thread safety to the Enqueue and Dequeue functions of the Queue.
    /// </summary>
    [Obsolete("this class adds no value (consider ProducerConsumerQueue)")]
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
                return base.Dequeue();
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


    public class ProducerConsumerQueue<T>
    {
        private readonly Queue<T> queue = new Queue<T>();
        private readonly Semaphore freeSlots;
        private readonly Semaphore usedSlots;
        private readonly ManualResetEvent productionFinished = new ManualResetEvent(false);

        /// <summary>
        /// Creates a producer-consumer queue with the specified capacity.
        /// Enqueue attempts will block while the queue is full.
        /// </summary>
        public ProducerConsumerQueue(int capacity)
        {
            freeSlots = new Semaphore(capacity, capacity);
            usedSlots = new Semaphore(0, capacity);
        }

        /// <summary>
        /// Creates a producer-consumer queue of arbitrary capacity.
        /// </summary>
        public ProducerConsumerQueue()
            : this(int.MaxValue)
        {
        }

        /// <summary>
        /// Dequeues an item from the list.
        /// If neccessary, the call blocks until an item becomes available.
        /// If the producer signals the end of production, the function returns false.
        /// </summary>
        public bool TryDequeue(out T item, TaskController controller)
        {
            if (controller.WaitAny(usedSlots, productionFinished) == 1) {
                item = default(T);
                return false;
            }

            try {
                lock (queue) {
                    item = queue.Dequeue();
                }
                return true;
            } finally {
                freeSlots.Release();
            }
        }

        /// <summary>
        /// Enqueues an item to the queue and signals one of the blocked consumers (if any).
        /// This method blocks while the queue is full.
        /// </summary>
        public void Enqueue(T item, TaskController controller)
        {
            controller.WaitOne(freeSlots);

            try {
                lock (queue) {
                    queue.Enqueue(item);
                }
            } finally {
                usedSlots.Release();
            }
        }

        /// <summary>
        /// Signals to the consumer(s) that no more items will become available.
        /// </summary>
        public void Terminate()
        {
            productionFinished.Set();
        }
    }
}
