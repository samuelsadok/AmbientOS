using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace AppInstall.Framework
{
    /// <summary>
    /// Provides a way of executing an important task persistently until succeeds.
    /// </summary>
    public class EssentialTask
    {
        private Func<bool> attempt;

        /// <param name="task">The action that should be executed in a persistent way</param>
        /// <param name="errorHandler">The error handler that is executed each time the action fails. Exceptions thrown in this handler are ignored.</param>
        public EssentialTask(Action task, Action<Exception> errorHandler)
        {
            attempt = () => {
                    try {
                        task();
                        return true;
                    } catch (Exception ex) {
                        try {
                            errorHandler(ex);
                        } catch {
                        }
                        return false;
                    }
                };
        }

        /// <summary>
        /// Starts attemting to execute the action. This routine returns after the first attempt
        /// and reports whether it succeeded. If not, subsequent attempts are done in the
        /// background until it succeeds.
        /// </summary>
        /// <param name="interval">The delay between the attempts</param>
        /// <param name="cancellationToken">this token may stop the loop, even if the action didn't succeed</param>
        public bool Start(TimeSpan interval, CancellationToken cancellationToken)
        {
            var firstAttempt = attempt();
            if (!firstAttempt) {
                new Task(() => {
                    while (!cancellationToken.IsCancellationRequested && !attempt())
                        if (cancellationToken.WaitHandle.WaitOne(interval))
                            return;
                }).Start();
            }
            return firstAttempt;
        }
    }
}
