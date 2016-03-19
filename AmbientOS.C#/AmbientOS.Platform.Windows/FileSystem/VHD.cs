using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.Environment;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem
{
    [AOSService(
        "Windows Virtual Image Service",
        Description = "Opens virtual image files (*.vhd) and makes them available as a disk. The differencing disk image part of the specification is not yet implemented."
        )]
    [ForPlatform(PlatformType.Windows)]
    public class WindowsVHDService
    {
        /// <summary>
        /// Opens the specified file as a virtual hard disk and makes it available as a disk object.
        /// </summary>
        [AOSAction("mount", "ext=vhd")]
        public DynamicSet<IDisk> Mount(IFile file, Context context)
        {
            var foreignFile = file.AsImplementation<Foreign.InteropFile>();
            if (foreignFile == null)
                throw new NotSupportedException("Windows can only mount native files as disk images");

            // open disk handle
            var openParameters = new PInvoke.OpenVirtualDiskParameters() {
                Version = PInvoke.OpenVirtualDiskVersion.Version1,
                Version1 = new PInvoke.OpenVirtualDiskParametersVersion1() { RWDepth = 1 }
            };

            var virtualStorageType = new PInvoke.VirtualStorageType() {
                DeviceId = PInvoke.VirtualStorageTypeDevice.VHD,
                VendorId = PInvoke.VirtualStorageTypeVendorMicrosoft
            };

            PInvoke.SafeDiskHandle disk = PInvoke.OpenVirtualDisk(virtualStorageType, foreignFile.path, PInvoke.VirtualDiskAccessFlags.All, PInvoke.OpenVirtualDiskFlags.None, openParameters);

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
                return new DynamicSet<IDisk>(new WindowsDiskService.WindowsDisk(diskName) { Handle = disk }.DiskRef).Retain();
            } catch {
                disk.Dispose();
                throw;
            }
        }
    }
}
