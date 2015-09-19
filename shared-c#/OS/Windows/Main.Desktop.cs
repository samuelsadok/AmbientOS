using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using AppInstall.Framework;

namespace AppInstall.OS
{
    class DesktopProgram
    {
        /// <summary>
        /// The main entry point for the application
        /// </summary>
        static void Main(string[] args)
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            Environment.CurrentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            ApplicationControl.Start(args);
            ApplicationControl.ShutdownToken.WaitHandle.WaitOne();
        }
    }
}
