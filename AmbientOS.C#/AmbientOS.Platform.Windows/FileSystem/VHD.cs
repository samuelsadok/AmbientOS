using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.Environment;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem
{
    [AOSObjectProvider()]
    [ForPlatform(PlatformType.Windows)]
    public static class WindowsVHD
    {
        /// <summary>
        /// Opens a virtual image file (*.vhd) and makes it available as a disk object.
        /// todo: add support for ISO files
        /// </summary>
        [AOSObjectProvider()]
        public static IDisk Mount([AOSObjectConstraint("Type", "file:vhd")] IFile file)
        {
            var fileImpl = file.AsImplementation<InteropFile>();
            if (fileImpl == null)
                throw new AOSRejectException("Windows can only mount native files as disk images", file);

            // open disk handle
            var openParameters = new PInvoke.OpenVirtualDiskParameters() {
                Version = PInvoke.OpenVirtualDiskVersion.Version1,
                Version1 = new PInvoke.OpenVirtualDiskParametersVersion1() { RWDepth = 1 }
            };

            var virtualStorageType = new PInvoke.VirtualStorageType() {
                DeviceId = PInvoke.VirtualStorageTypeDevice.VHD,
                VendorId = PInvoke.VirtualStorageTypeVendorMicrosoft
            };

            PInvoke.SafeDiskHandle disk = PInvoke.OpenVirtualDisk(virtualStorageType, fileImpl.path, PInvoke.VirtualDiskAccessFlags.All, PInvoke.OpenVirtualDiskFlags.None, openParameters);

            try {
                // attach disk - permanently
                var attachParameters = new PInvoke.AttachVirtualDiskParameters() {
                    Version = PInvoke.AttachVirtualDiskVersion.Version1,
                    Version1 = new PInvoke.AttachVirtualDiskParametersVersion1()
                };

                // This fails normally, when executed as a normal user.
                // Run as admin or add current account in Group Policies => Windows Settings => Security Settings => Local Policies => User Rights Assignment => Perform volume maintenance tasks
                PInvoke.AttachVirtualDisk(disk, IntPtr.Zero, PInvoke.AttachVirtualDiskFlags.NoDriveLetter, 0, attachParameters);

                var diskName = PInvoke.GetVirtualDiskPhysicalPath(disk);
                return (new WindowsDisk(diskName) { Handle = disk }).AsReference<IDisk>();
            } catch {
                disk.Dispose();
                throw;
            }
        }
    }
}
