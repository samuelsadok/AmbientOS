using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppInstall.Framework
{

    /// <summary>
    /// Represents a facility that manages several threads that host processing of items of a specified type.
    /// Processing begins as soon as Launch() is called and ends when all cancellation tokens are set.
    /// </summary>
    public class ProcessorPool<T> where T : IDisposable
    {
        private Semaphore availableSlots;
        private List<Tuple<long, CancellationTokenSource>> runningThreads = new List<Tuple<long, CancellationTokenSource>>(); // stores the launch timestamp and cancellation token for each thread

        /// <summary>
        /// Triggered whenever an item handler was cancelled to release resources for another operation.
        /// </summary>
        public event Action<ProcessorPool<T>> ItemHandlerCancelled;

        /// <summary>
        /// Triggered when an item processor raised an exception.
        /// </summary>
        public event EventHandler<Exception> ItemHandlerFailed;

        /// <summary>
        /// Triggered when an item provider raised an exception.
        /// </summary>
        public event EventHandler<Exception> ItemProviderFailed;



        /// <summary>
        /// Creates a queue that processes items in parallel.
        /// </summary>
        /// <param name="handler">The handler that processes an item</param>
        /// <param name="threadSlots">Maximum number of concurrently executed item handlers</param>
        public ProcessorPool(int threadSlots)
        {
            availableSlots = new Semaphore(threadSlots, threadSlots);
        }



        /// <summary>
        /// Starts processing of items.
        /// The processor will wait until a thread slot becomes free, retrieve a new item through the itemProvider function and then start processing the item using the free thread slot.
        /// If several of these processing tasks are excuted concurrently they will share the available thread slots, so the number of processing tasks must be smaller than the number of slots.
        /// This procedure returns immediately.
        /// </summary>
        /// <param name="itemProvider">This function is used to retrieve new items for processing. This function can block for an unlimited time.</param>
        /// <param name="itemProcessor">This function receives the items that were obtained through the itemProvider function. It must respect it's cancellation token which may be asserted when it takes to much time to complete.</param>
        /// <param name="maxDelay">If no thread is available for this timespan (in milliseconds) the oldest thread will be aborted.</param>
        public void Start(Func<T> itemProvider, Func<T, CancellationToken, Task> itemProcessor, int maxDelay, CancellationToken cancellationToken)
        {
            new Task(() => {
                while (!cancellationToken.IsCancellationRequested) {
                    // wait for a slot to become available
                    int result;
                    while ((result = WaitHandle.WaitAny(new WaitHandle[] { cancellationToken.WaitHandle, availableSlots }, maxDelay)) != 1) {
                        if (result == 0) // the pool was shut down
                            throw new OperationCanceledException(cancellationToken);
                        else if (!KillOldestThread()) // no thread became available, so let's kill one
                            throw new Exception("all thread slots are still occupied after having been revoked"); // this can happen if the handler does not respect the cancellation token
                    }

                    // retrieve the item to be processed
                    T item;
                    try {
                        item = itemProvider();
                    } catch (Exception ex) {
                        ItemProviderFailed.SafeInvoke(this, ex);
                        continue;
                    }

                    // execute the item processor
                    Tuple<long, CancellationTokenSource> thread = new Tuple<long, CancellationTokenSource>(DateTime.Now.Ticks, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
                    Task t = new Task(() => {
                        try {
                            itemProcessor(item, thread.Item2.Token).Wait();
                        } catch (Exception ex) {
                            ItemHandlerFailed.SafeInvoke(this, ex);
                        } finally {
                            item.Dispose();
                        }

                        lock (runningThreads) {
                            if (runningThreads.Contains(thread))
                                runningThreads.Remove(thread);
                            availableSlots.Release();
                        }
                    });

                    lock (runningThreads) {
                        runningThreads.Add(thread);
                        t.Start();
                    }
                }
            }).Start();
        }



        /// <summary>
        /// Kills the oldest running thread and releases it's associated resources but does not release the semaphore, assuming that the killed thread will be replaced by a new thread.
        /// Returns false if no thread was running.
        /// </summary>
        private bool KillOldestThread()
        {
            lock (runningThreads) {
                if (runningThreads.Count() == 0) return false;
                Tuple<long, CancellationTokenSource> oldest = runningThreads.Aggregate((t1, t2) => t1.Item1 < t2.Item1 ? t1 : t2);

                oldest.Item2.Cancel();
                runningThreads.Remove(oldest);
            }

            ItemHandlerCancelled.SafeInvoke(this);
            return true;
        }
    }
}
