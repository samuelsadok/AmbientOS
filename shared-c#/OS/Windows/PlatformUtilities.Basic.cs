using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.OS
{
    /// <summary>
    /// Provides basic utility functions for the Windows platform
    /// </summary>
    static partial class PlatformUtilities
    {
        /// <summary>
        /// Determines if the application is stored on a network location
        /// </summary>
        public static bool IsBinaryOnNetworkDrive()
        {
            string rootPath = Path.GetPathRoot(ApplicationControl.ApplicationBinaryPath);
            try {
                return ((new DriveInfo(rootPath)).DriveType == DriveType.Network);
            } catch (Exception) {
                try {
                    return (new Uri(rootPath)).IsUnc;
                } catch {
                    return false;
                }
            }
        }
    }
}
