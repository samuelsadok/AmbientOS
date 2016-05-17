using System;
using System.Threading;

namespace AmbientOS
{
    public class CancelableThread
    {
        private Thread thread;

        public Context Context { get { return Context.GetContext(thread); } }

        public CancelableThread(Action action)
        {
            thread = new Thread(() => {
                try {
                    action();
                } catch (OperationCanceledException) {
                    // ignore
                }
            });
            Context.ForwardContext(thread);
        }

        public void Start()
        {
            thread.Start();
        }
    }
}
