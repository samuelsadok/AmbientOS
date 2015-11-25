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
        public IUI UI { get; set; }
        public IEnvironment Environment { get; set; }
        public LogContext Log { get; set; }
        public TaskController Controller { get; set; }
    }
}
