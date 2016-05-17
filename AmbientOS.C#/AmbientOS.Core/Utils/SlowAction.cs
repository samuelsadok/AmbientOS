using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static AmbientOS.TaskController;

namespace AmbientOS.Utils
{
    /// <summary>
    /// Allows an action to be triggered in a lazy fashion.
    /// The action is guaranteed not to be executed several times concurrently.
    /// When the action is triggered, it is guaranteed to execute at least once AFTER being triggered.
    /// It is NOT guaranteed to execute as often as it is triggered.
    /// </summary>
    public class SlowAction
    {
        private readonly Action action;
        private readonly object lockRef = new object();
        private EventWaitHandle nextExecutionFinishedHandle; // a non-null value in this field indicates that another execution of the action must be done
        private EventWaitHandle executionFinishedHandle; // a non-null value in this field indicates that an execution is currently in progress

        public ActivityTracker Tracker { get; private set; }

        public SlowAction(Action action)
        {
            if (action == null) throw new ArgumentNullException("action");

            Tracker = new ActivityTracker();

            this.action = () => {
                Tracker.SwitchToActive();
                try {
                    action();
                } catch (Exception ex) {
                    Tracker.SwitchToFailed(ex);
                    return;
                }
                Tracker.SwitchToSucceeded();
            };
        }

        /* issue: results in ambiguous function call
        /// <summary>
        /// Creates a slow action from an async action.
        /// </summary>
        public SlowAction(Func<CancellationToken, Task> asyncAction)
            : this(c => asyncAction(c).Wait(c))
        {
        }
        */

        /// <summary>
        /// Determines if the execution handler thread should be launched
        /// </summary>
        /// <param name="soft">if true and an execution is already running, no new execution will be enqueued</param>
        /// <param name="waitHandle">set to the wait handle that is triggered upon completition of the execution</param>
        private bool ShouldExecute(bool soft, out WaitHandle waitHandle)
        {
            lock (lockRef) {
                if (soft && executionFinishedHandle != null) {
                    waitHandle = executionFinishedHandle;
                    return false;
                }

                if (nextExecutionFinishedHandle == null)
                    nextExecutionFinishedHandle = new ManualResetEvent(false);
                waitHandle = nextExecutionFinishedHandle;
                if (executionFinishedHandle == null) {
                    executionFinishedHandle = nextExecutionFinishedHandle;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Determines if another execution is required in which case it returns the wait handle that waits for the execution to complete
        /// </summary>
        private EventWaitHandle AcquireHandleForExecution()
        {
            EventWaitHandle result;
            lock (lockRef) {
                result = executionFinishedHandle = nextExecutionFinishedHandle;
                nextExecutionFinishedHandle = null;
                return result;
            }
        }

        /// <summary>
        /// Triggers the underlying action and returns a wait handle that will be triggered upon completition of the first execution of the action that was started after triggering.
        /// </summary>
        /// <param name="soft">if true and an execution is already in progress, no new execution is enqueued</param>
        /// <param name="cancellationToken">cancels the action (must be the same in every call => todo: fix)</param>
        public WaitHandle Trigger(bool soft)
        {
            var parentContext = Context.CurrentContext;
            WaitHandle result;
            if (ShouldExecute(soft, out result))
                Task.Run(() => {
                    Context.CurrentContext = parentContext;
                    EventWaitHandle handle;
                    while ((handle = AcquireHandleForExecution()) != null) {
                        action();
                        handle.Set();
                    }
                });
            return result;
        }

        /// <summary>
        /// Triggers the underlying action and returns a wait handle that will be triggered upon completition of the first execution of the action that was started after triggering.
        /// </summary>
        /// <param name="cancellationToken">cancels the action</param>
        public WaitHandle Trigger()
        {
            return Trigger(false);
        }

        /// <summary>
        /// Triggers the underlying action and returns a wait handle that will be triggered upon completition of the current execution of the action.
        /// If an execution is already in progress, no new execution is enqueued.
        /// </summary>
        /// <param name="controller">cancels the action</param>
        public WaitHandle SoftTrigger()
        {
            return Trigger(true);
        }

        /// <summary>
        /// Triggers the underlying action and blocks until it was completed.
        /// </summary>
        /// <param name="controller">causes the routine to stop waiting but does not revoke or cancel the triggered action</param>
        public async Task TriggerAndWait()
        {
            await Trigger(false).WaitAsync(); // todo: propagate errors from this triggering
        }

        /// <summary>
        /// Triggers the underlying action and blocks until it was completed.
        /// If an execution is already in progress, no new execution is enqueued and this function blocks until the current execution completes.
        /// </summary>
        /// <param name="controller">causes the routine to stop waiting but does not revoke or cancel the triggered action</param>
        public async Task SoftTriggerAndWait()
        {
            await Trigger(true).WaitAsync(); // todo: propagate errors from this triggering
        }

        /// <summary>
        /// Starts a new task that triggers the action at the specified interval.
        /// That means that the action is executed at most at the specified frequency.
        /// Periodic triggering can be used in combination with explicit triggering.
        /// </summary>
        /// <param name="interval">interval in milliseconds</param>
        /// <param name="cancellationToken">Cancels the periodic triggering.</param>
        public void TriggerPeriodically(TimeSpan interval)
        {
            Task.Run(() => {
                while (true) {
                    Trigger(false);
                    Wait(interval); // throws an exception when cancelled.
                };
            });
        }
    }
}