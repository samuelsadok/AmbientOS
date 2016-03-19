using System;
using System.IO;
using AmbientOS.FileSystem;

namespace AmbientOS
{
    /// <summary>
    /// Provides basic utility functions for the Windows platform.
    /// These features are also available in sandboxed (Windows Store) apps.
    /// </summary>
    static partial class PlatformUtilities
    {


        /// <summary>
        /// Returns the path including name of the executing assembly
        /// </summary>
        public static IFile Assembly { get; } = FileSystem.Foreign.InteropFileSystem.GetFileFromPath(System.Reflection.Assembly.GetExecutingAssembly().Location);

        /// <summary>
        /// Determines if the application is stored on a network location
        /// </summary>
        public static bool IsOnNetworkDrive(IFileSystemObject file)
        {
            var f = file.AsImplementation<FileSystem.Foreign.InteropFileSystemObject>();
            string rootPath = Path.GetPathRoot(f.path);
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
