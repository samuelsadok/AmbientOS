using System;
using System.Threading;

namespace AmbientOS
{
    public class CancelableThread
    {
        private Thread thread;

        public CancelableThread(Action action)
        {
            thread = new Thread(() => {
                try {
                    action();
                } catch (OperationCanceledException) {
                    // ignore
                }
            });
        }

        public void Start()
        {
            thread.Start();
        }
    }
}
