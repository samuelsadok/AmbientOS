using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.Installer
{
    public class SoftwareDistributionProtocol
    {
        public const string PROTOCOL_IDENTIFIER = "ASDP/1.0"; // AppInstall software distribution protocol
        public const string UPDATE_SCRIPT_RESOURCE = "updates"; // client requests the update script (if any) for its current version
        public const string APPLICATION_RESOURCE = "apps"; // client requests the installer script for the application
        public const string FILE_RESOURCE = "files"; // client requests a file by hash code
    }
}
