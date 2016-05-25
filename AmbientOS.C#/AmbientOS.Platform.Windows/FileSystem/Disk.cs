using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Management;
using Microsoft.Win32.SafeHandles;
using static AmbientOS.LogContext;

namespace AmbientOS.FileSystem
{
    /// <summary>
    /// Provides read-write access to a raw disk in Windows
    /// </summary>
    [ForPlatform(PlatformType.Windows)]
    public class WindowsDisk : IDiskImpl, IDisposable
    {
        /// <summary>
        /// Returns a name of the form "\\.\PhysicalDriveX" where X is the number of this disk.
        /// </summary>
        public string Name { get { return @"\\.\PhysicalDrive" + Number; } }
        public long Number { get; }
        public string Mapping { get { return GetDeviceMapping(Name); } }
        public string Caption { get { return (string)GetDiskProperty(Name, "Caption"); } }
        public string MediaType { get { return (string)GetDiskProperty(Name, "MediaType"); } }
        public string SerialNumber { get { return (string)GetDiskProperty(Name, "SerialNumber"); } }

        public DynamicEndpoint<Guid> ID { get; }
        public DynamicEndpoint<string> Type { get; }
        public DynamicEndpoint<long> BlockSize { get; }
        public DynamicEndpoint<long?> BlockCount { get; }

        /// <summary>
        /// If not null, this handle will be closed when this disk is disposed.
        /// </summary>
        public SafeHandle Handle { get; set; } = null;


        private SafeFileHandle OpenDisk(PInvoke.Access access)
        {
            return PInvoke.CreateFile(Name, access, PInvoke.ShareMode.ReadWrite, IntPtr.Zero, PInvoke.CreationDisposition.OPEN_EXISTING, PInvoke.FileFlags.OVERLAPPED, IntPtr.Zero);
        }

        private static string GetDeviceMapping(string device)
        {
            return PInvoke.QueryDosDevice(device).Single();
        }

        /// <summary>
        /// Queries a property of a disk.
        /// Properties include: Caption & Model (normally identical), DeviceID & Name (normally identical), Description, Manufacturer, MediaType, SerialNumber (Vista or higher).
        /// Returns null if the disk was not found.
        /// </summary>
        private static object GetDiskProperty(string name, string property)
        {
            var query = new WqlObjectQuery("SELECT * FROM Win32_DiskDrive");
            object result = null;
            using (var res = new ManagementObjectSearcher(query)) {
                foreach (var obj in res.Get()) {
                    if (((string)obj["DeviceID"]).ToLower() == (name).ToLower())
                        result = obj[property];
                    obj.Dispose();
                }
            }
            return result;
        }


        private static long GetDiskNumber(string name)
        {
            string[] PREFIXES = new string[] { @"\\?\", @"\\.\" };
            const string PREFIX = "physicaldrive";
            var str = name.ToLower().Substring(PREFIXES.FirstOrDefault(p => name.StartsWith(p))?.Length ?? 0).TrimEnd('\\');
            if (str.StartsWith(PREFIX))
                str = str.Substring(PREFIX.Length);

            long number;
            if (!long.TryParse(str, out number))
                throw new Exception(string.Format("The path \"{0}\" is not a valid Windows disk name.", name));
            return number;
        }

        /// <summary>
        /// Generates a Windows disk object from the provided number.
        /// </summary>
        public WindowsDisk(long number)
        {
            Number = number;

            ID = new DynamicEndpoint<Guid>(new Guid(), PropertyAccess.ReadOnly); // todo: implement through serial number or something else
            Type = new DynamicEndpoint<string>("disk:windows", PropertyAccess.ReadOnly);

            // get drive info
            using (var disk = OpenDisk(PInvoke.Access.None)) {
                var geometry = PInvoke.DeviceIoControl<PInvoke.DiskGeometry>(disk, PInvoke.IOCTL_DISK_GET_DRIVE_GEOMETRY);
                var sectors = geometry.Cylinders * geometry.TracksPerCylinder * geometry.SectorsPerTrack; // TODO: THIS SECTOR CALCULATION IS BULLSHIT

                BlockSize = new DynamicEndpoint<long>(geometry.BytesPerSector, PropertyAccess.ReadOnly);
                BlockCount = new DynamicEndpoint<long?>(sectors, PropertyAccess.ReadOnly);
            }
        }

        /// <summary>
        /// Generates a Windows disk object from the provided name.
        /// </summary>
        /// <param name="name">A name of the form \\.\PhysicalDriveX\ (prefix and suffix are optional)</param>
        public WindowsDisk(string name)
            : this(GetDiskNumber(name))
        {
        }

        public void ReadBlocks(long offset, long count, byte[] buffer, long bufferOffset)
        {
            // todo: handle transfers > 2GB
            using (var disk = OpenDisk(PInvoke.Access.Read))
                PInvoke.ReadFile(disk, offset * BlockSize.Get(), buffer, (int)bufferOffset, (int)count * (int)BlockSize.Get());
        }

        /// <summary>
        /// Writes to the Windows disk.
        /// Various restrictions apply. For details see the MSDN documentation of WriteFile (and look for the notes on disks).
        /// </summary>
        public void WriteBlocks(long offset, long count, byte[] buffer, long bufferOffset)
        {
            // todo: handle transfers > 2GB
            using (var disk = OpenDisk(PInvoke.Access.Write))
                PInvoke.WriteFile(disk, offset * BlockSize.Get(), buffer, (int)bufferOffset, (int)count * (int)BlockSize.Get());
        }

        public void Flush()
        {
            // the handle is closed after each write, which presumably flushes the cache
        }

        public void Dispose()
        {
            Handle?.Dispose();
        }

        public override int GetHashCode()
        {
            return Number.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Number == (obj as WindowsDisk)?.Number;
        }

        /// <summary>
        /// Enumerates all Windows disks on the system.
        /// </summary>
        public static IEnumerable<WindowsDisk> EnumerateDisks()
        {
            foreach (var name in PInvoke.GetAllDevices().Where(dev => dev.StartsWith("PhysicalDrive"))) {
                WindowsDisk disk;
                try {
                    disk = new WindowsDisk(name);
                } catch (Exception ex) {
                    Log(string.Format("encountered faulty drive ({0}): {1}", name, ex), LogType.Warning);
                    continue;
                }
                yield return disk;
            }
        }
    }
}

