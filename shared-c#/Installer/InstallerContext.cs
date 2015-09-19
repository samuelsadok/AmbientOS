using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.Installer
{
    public class InstallerContext
    {
        public string InstallerFolder { get; set; }
        public string ApplicationPath { get; set; }
        public string ApplicationBinaryPath { get; set; }
        public string ApplicationName { get; set; }
        public bool IsSystemService { get; set; }
        public bool TerminateApplication { get; set; }
        public bool RelaunchApplication { get; set; }

        [System.Xml.Serialization.XmlIgnore()]
        public LogContext LogContext { get; set; }

        [System.Xml.Serialization.XmlIgnore()]
        public SoftwareDistributionClient SoftwareServerClient { get; set; }

    }
}
