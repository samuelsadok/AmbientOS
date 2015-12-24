using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using AmbientOS.Environment;

namespace AmbientOS
{
    public class TaskController : IDisposable
    {
        public WaitHandle CancellationHandle { get { return cancellationHandle; } }
        public CancellationToken CancellationToken { get; }
        public LogContext LogContext { get; }

        private object lockRef = new object();
        private bool cancelled = false; //todo: sync access

        private ManualResetEvent cancellationHandle = new ManualResetEvent(false);

        public TaskController()
        {
            var source = new CancellationTokenSource();
            new Thread(() => {
                cancellationHandle.WaitOne();
                cancellationHandle.Dispose();
                source.Cancel();
            }).Start();
            CancellationToken = source.Token;
        }


        /// <summary>
        /// Cancels the task in a controlled manner, frees all resources and locks that were held by the task and terminates any threads and processes that are exlusively used by this task.
        /// On cancellation, the task shall leave any external data (such as files and other objects) in a consistent state.
        /// Cancellation does not neccessarily reverses changes done so far.
        /// If the operation is already cancelled, this method has no effect.
        /// </summary>
        public void Cancel()
        {
            lock (lockRef) {
                cancelled = true;
                cancellationHandle.Set();
            }
        }

        /// <summary>
        /// Pauses the task.
        /// </summary>
        public void Pause()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Resumes the task.
        /// </summary>
        public void Resume()
        {
            throw new NotImplementedException();
        }

        public void OnCancellation(Action action)
        {
            new Thread(() => {
                CancellationHandle.WaitOne();
                action();
            }).Start();
        }

        /// <summary>
        /// Waits for the specified WaitHandle.
        /// As soon as the task is cancelled, the method throws an exception.
        /// </summary>
        /// <exception cref="OperationCanceledException">The task was cancelled.</exception>
        public void WaitOne(WaitHandle handle)
        {
            WaitHandle.WaitAny(new WaitHandle[] { handle, CancellationHandle });
            ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Waits for any the specified WaitHandles.
        /// As soon as the task is cancelled, the method throws an exception.
        /// </summary>
        /// <exception cref="OperationCanceledException">The task was cancelled.</exception>
        public int WaitAny(params WaitHandle[] handles)
        {
            var result = WaitHandle.WaitAny(handles.Concat(new WaitHandle[] { CancellationHandle }).ToArray());
            ThrowIfCancellationRequested();
            return result;
        }

        public void ThrowIfCancellationRequested()
        {
            lock (lockRef) {
                if (cancelled)
                    throw new OperationCanceledException();
            }
        }

        public void Dispose()
        {
            Cancel();
        }
    }
}
