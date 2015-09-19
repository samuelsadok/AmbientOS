using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Installer
{
    /// <summary>
    /// Imitates part of the installer system interface allow for unified code on platforms
    /// where other update channels are used.
    /// </summary>
    public class InstallerSystem
    {
        public void Init(params object[] p)
        {
            // todo: check for updates and notify user
        }
    }
}