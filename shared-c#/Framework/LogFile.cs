using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AmbientOS.Environment;
using AmbientOS.FileSystem;

namespace AmbientOS.Utils
{
    public class LogFile : LogContext
    {
        private static LogDelegate ConstructLogDelegate(IFile file, TaskController controller)
        {
            var pendingLines = new Queue<string>();

            var flushAction = new SlowAction(c => {
                while (true) {
                    string line;
                    lock (pendingLines) {
                        if (!pendingLines.Any())
                            return;
                        line = pendingLines.Dequeue();
                    }
                    file.Append(Encoding.Default.GetBytes(line + "\r\n"));
                }
            });

            return (string context, string message, LogType type, TaskController ctrl) => {
                lock (pendingLines) {
                    pendingLines.Enqueue(message);
                }
                flushAction.Trigger(ctrl);
            };
        }


        public LogFile(IFile file, TaskController controller)
            : base(ConstructLogDelegate(file, controller), null, "")
        {
        }
    }
}
