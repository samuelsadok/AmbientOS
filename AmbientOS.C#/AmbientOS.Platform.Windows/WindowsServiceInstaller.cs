using AmbientOS.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.Platform
{
    /// <summary>
    /// Provides the installer for the windows service
    /// </summary>
    [System.ComponentModel.RunInstaller(true)]
    public class WindowsServiceInstaller : System.Configuration.Install.Installer
    {
        public WindowsServiceInstaller()
        {
            ServiceProcessInstaller p = new ServiceProcessInstaller();
            ServiceInstaller s = new ServiceInstaller();

            p.Account = ServiceAccount.LocalSystem;
            s.StartType = ServiceStartMode.Automatic;
            s.ServiceName = Assembly.GetEntryAssembly().GetTitle("Unnamed AmbientOS Service");
            s.Description = Assembly.GetEntryAssembly().GetDescription("(no description available)");

            Installers.Add(p);
            Installers.Add(s);
        }
    }
}
