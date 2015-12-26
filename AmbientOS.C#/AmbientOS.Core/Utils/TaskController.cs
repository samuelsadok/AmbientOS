using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using AmbientOS.Environment;

namespace AmbientOS
{
    public enum TaskState
    {
        /// <summary>
        /// The operation was not yet started or is paused.
        /// </summary>
        Inactive,

        /// <summary>
        /// The operation is in progress.
        /// </summary>
        Active,

        /// <summary>
        /// The operation was cancelled, completed or failed.
        /// Once in this state, a task cannot transition to another state again.
        /// </summary>
        Terminated
    }


    public class TaskController : IDisposable
    {
        public WaitHandle CancellationHandle { get { return terminatedHandle; } }
        public CancellationToken CancellationToken { get; }
        public LogContext LogContext { get; }

        private readonly object lockRef = new object();
        private int currentState;
        private int requestedState;
        private int transitioning = 0;

        private readonly ManualResetEvent activeHandle = new ManualResetEvent(false);
        private readonly ManualResetEvent inactiveHandle = new ManualResetEvent(false);
        private readonly ManualResetEvent terminatedHandle = new ManualResetEvent(false);

        private readonly List<Action> onResumeActions = new List<Action>();
        private readonly List<Action> onPauseActions = new List<Action>();
        private readonly List<Action> onCancellationActions = new List<Action>();

        public TaskController(TaskState state)
        {
            requestedState = (int)state;
            currentState = (int)state;

            WaitHandleForState(state).Set();

            var source = new CancellationTokenSource();
            CancellationToken = source.Token;

            OnCancellation(() => {
                source.Cancel();
            });
        }

        /// <summary>
        /// Creates a task controller in the active state.
        /// </summary>
        public TaskController()
            : this(TaskState.Active)
        {
        }

        private ManualResetEvent WaitHandleForState(TaskState state)
        {
            switch (state) {
                case TaskState.Active: return activeHandle;
                case TaskState.Inactive: return inactiveHandle;
                case TaskState.Terminated: return terminatedHandle;
                default: throw new ArgumentException("Unknown state", $"{state}");
            }
        }

        private List<Action> ActionListForState(TaskState state)
        {
            switch (state) {
                case TaskState.Active: return onResumeActions;
                case TaskState.Inactive: return onPauseActions;
                case TaskState.Terminated: return onCancellationActions;
                default: throw new ArgumentException("Unknown state", $"{state}");
            }
        }

        /// <summary>
        /// Handles state transitions as long as the requested state and the actual state are not consistent.
        /// If another thread is already handling state transitions, this returns immediately.
        /// </summary>
        private void HandleStateTransitions()
        {
            var exceptions = new List<Exception>();

            while (requestedState != currentState) {
                int alreadyTransitioning = Interlocked.Exchange(ref transitioning, 1);
                if (alreadyTransitioning == 1)
                    return; // if some other thread is already responsible for handling transitions, we can exit here
                
                Action[] actions;

                lock (lockRef) {
                    var newState = (TaskState)requestedState;
                    var oldState = (TaskState)Interlocked.Exchange(ref currentState, (int)newState);

                    if (newState != oldState) {
                        WaitHandleForState(oldState).Reset();
                        WaitHandleForState(newState).Set();
                        actions = ActionListForState(newState).ToArray();
                    } else {
                        actions = new Action[0];
                    }
                }

                foreach (var action in actions) {
                    try {
                        action();
                    } catch (Exception ex) {
                        exceptions.Add(ex);
                    }
                }

                Interlocked.Exchange(ref transitioning, 0);
            }

            if (exceptions.Any())
                throw exceptions.Count() == 1 ? exceptions.First() : new AggregateException(exceptions);
        }

        /// <summary>
        /// Executes the specified action whenever the task transitions to the specified state.
        /// If the task is already in that state, the action is executed immediately.
        /// </summary>
        private void OnStateChange(TaskState state, Action action)
        {
            if (action == null)
                throw new ArgumentNullException($"{action}");

            lock (lockRef) {
                ActionListForState(state).Add(action);

                if (currentState == (int)state)
                    action();
            }
        }

        /// <summary>
        /// Transitions to the specified new state, if not already in that state.
        /// </summary>
        /// <exception cref="InvalidOperationException">An invalid state transition was requested, such as an attempt to exit the terminated state.</exception>
        public void Transition(TaskState newState)
        {
            Interlocked.Exchange(ref requestedState, (int)newState);
            HandleStateTransitions();
        }

        /// <summary>
        /// Starts or resumes the task.
        /// If the task is already active, this method has no effect.
        /// </summary>
        public void Resume()
        {
            Transition(TaskState.Active);
        }

        /// <summary>
        /// Pauses the task.
        /// If the task is already inactive, this method has no effect.
        /// </summary>
        public void Pause()
        {
            Transition(TaskState.Inactive);
        }

        /// <summary>
        /// Cancels the task in a controlled manner, frees all resources and locks that were held by the task and terminates any threads and processes that are exlusively used by this task.
        /// On cancellation, the task shall leave any external data (such as files and other objects) in a consistent state.
        /// Cancellation does not neccessarily reverse changes done so far.
        /// If the operation is already cancelled, this method has no effect.
        /// </summary>
        public void Cancel()
        {
            Transition(TaskState.Terminated);
        }

        /// <summary>
        /// Executes the specified action when the task is started or resumed.
        /// If the task is currently active, the action is excecuted immediately.
        /// </summary>
        public void OnResume(Action action)
        {
            OnStateChange(TaskState.Active, action);
        }

        /// <summary>
        /// Executes the specified action when the task is paused.
        /// If the task is currently inactive, the action is excecuted immediately.
        /// </summary>
        public void OnPause(Action action)
        {
            OnStateChange(TaskState.Inactive, action);
        }

        /// <summary>
        /// Executes the specified action on cancellation.
        /// If the task was already terminated, the action is excecuted immediately.
        /// </summary>
        public void OnCancellation(Action action)
        {
            OnStateChange(TaskState.Terminated, action);
        }

        /// <summary>
        /// Waits for the specified timespan.
        /// As soon as the task is cancelled, the method throws an exception.
        /// todo: this should block while the task is paused, even if the operation times out
        /// </summary>
        public void Wait(TimeSpan timeout)
        {
            CancellationHandle.WaitOne(timeout);
            ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Waits for the specified WaitHandle.
        /// As soon as the task is cancelled, the method throws an exception.
        /// todo: this should block while the task is paused, even if the handle is signaled
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
        /// todo: this should block while the task is paused, even if a handle is signaled
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
            if (currentState == (int)TaskState.Terminated)
                throw new OperationCanceledException();
        }

        public void Dispose()
        {
            Cancel();
        }
    }
}
