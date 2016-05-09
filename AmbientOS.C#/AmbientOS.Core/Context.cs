using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.Environment;
using AmbientOS.UI;
using AmbientOS.Utils;

namespace AmbientOS
{
    public class Context
    {
        public string Name { get; set; }
        public IShell Shell { get; set; }
        public IEnvironment Environment { get; set; }
        public LogContext Log { get; set; }
        public TaskController Controller { get; set; }

        public Context SubContext(string name)
        {
            return new Context() {
                Name = name,
                Shell = Shell,
                Environment = Environment,
                Log = Log.SubContext(name),
                Controller = Controller
            };
        }
    }
}
