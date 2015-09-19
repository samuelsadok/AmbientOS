using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.OS;

namespace AppInstall
{
    class Application
    {
        public static string ApplicationName { get { return "AmbientOS Installer"; } }
        public static string ApplicationDescription { get { return "Tool for installing AmbientOS"; } }


        public Application(string[] args)
        {
        }


        public void Main()
        {
            Platform.DefaultLog.Log("Installer application started. Ok you can go now, that's all there is to it. Press any key to leave. Really, nothing more is going on here. kthxbye.");
        }
    }
}
