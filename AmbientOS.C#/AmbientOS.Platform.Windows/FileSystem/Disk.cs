using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Management;
using Microsoft.Win32.SafeHandles;
using static AmbientOS.LogContext;

namespace AmbientOS.FileSystem
{
    [AOSService("Windows Disk Service", Description = "Exposes the disks managed by Windows")]
    [ForPlatform(PlatformType.Windows)]
    public class WindowsDiskService
    {
        public class WindowsDisk : IDiskImpl, IDisposable
        {
            public IDisk DiskRef { get; }
            public DynamicEndpoint<DiskInfo> Info { get; }

            /// <summary>
            /// Returns a name of the form "\\.\PhysicalDriveX" where X is the number of this disk.
            /// </summary>
            public string Name { get { return @"\\.\PhysicalDrive" + Number; } }
            public long Number { get; }
            public string Mapping { get { return GetDeviceMapping(Name); } }
            public string Caption { get { return (string)GetDiskProperty(Name, "Caption"); } }
            public string MediaType { get { return (string)GetDiskProperty(Name, "MediaType"); } }
            public string SerialNumber { get { return (string)GetDiskProperty(Name, "SerialNumber"); } }

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
                DiskRef = new DiskRef(this);
                Number = number;

                // get drive info
                DiskInfo info;
                using (var disk = OpenDisk(PInvoke.Access.None)) {
                    var geometry = PInvoke.DeviceIoControl<PInvoke.DiskGeometry>(disk, PInvoke.IOCTL_DISK_GET_DRIVE_GEOMETRY);
                    var sectors = geometry.Cylinders * geometry.TracksPerCylinder * geometry.SectorsPerTrack; // TODO: THIS SECTOR CALCULATION IS BULLSHIT
                    info = new DiskInfo() {
                        // todo: convey serial number, but right now we're limited by the disk interface
                        BytesPerSector = geometry.BytesPerSector,
                        Sectors = sectors,
                        MaxSectors = sectors,
                        Tracks = 1
                    };
                }

                Info = new DynamicEndpoint<DiskInfo>(
                    () => info,
                    val => { throw new NotImplementedException("Can't change metadata of a Windows disk"); });
            }

            /// <summary>
            /// Generates a Windows disk object from the provided name.
            /// </summary>
            /// <param name="name">A name of the form \\.\PhysicalDriveX\ (prefix and suffix are optional)</param>
            public WindowsDisk(string name)
                : this(GetDiskNumber(name))
            {
            }

            public void Read(int track, long offset, long count, byte[] buffer, long bufferOffset)
            {
                if (track != 0)
                    throw new ArgumentOutOfRangeException($"{track}");
                using (var disk = OpenDisk(PInvoke.Access.Read))
                    PInvoke.ReadFile(disk, offset * Info.Get().BytesPerSector, buffer, (int)bufferOffset, (int)count * (int)Info.Get().BytesPerSector);
            }

            /// <summary>
            /// Writes to the Windows disk.
            /// Various restrictions apply. For details see the MSDN documentation of WriteFile (and look for the notes on disks).
            /// </summary>
            public void Write(int track, long offset, long count, byte[] buffer, long bufferOffset)
            {
                if (track != 0)
                    throw new ArgumentOutOfRangeException($"{track}");
                using (var disk = OpenDisk(PInvoke.Access.Write))
                    PInvoke.WriteFile(disk, offset * Info.Get().BytesPerSector, buffer, (int)bufferOffset, (int)count * (int)Info.Get().BytesPerSector);
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

            #region "Stream Implementation"

            /*

            public override bool CanRead { get { return true; } }
            public override bool CanWrite { get { return true; } }
            public override bool CanSeek { get { return true; } }
            public override long Length { get { return Geometry.Capacity; } }
            public override long Position { get; set; }

            public override long Seek(long offset, SeekOrigin origin)
            {
                if (origin == SeekOrigin.End) {
                    Position = Length + offset;
                } else {
                    if (origin == SeekOrigin.Current)
                        offset += Position;
                    Position = offset;
                }

                if (Position > Length) {
                    Position = Length;
                    throw new ArgumentOutOfRangeException();
                }

                return Position;
            }


            public override int Read(byte[] buffer, int offset, int count)
            {
                var position = Position;
                var actualCount = (int)Math.Min(count, Length - position);
                var result = actualCount;

                long firstSector = position / Geometry.BytesPerSector;
                long lastSector = (position + actualCount - 1) / Geometry.BytesPerSector;
                long firstSectorBytes = Geometry.BytesPerSector - (position % Geometry.BytesPerSector);
                long lastSectorBytes = ((position + actualCount - 1) % Geometry.BytesPerSector) + 1;


                // make sure the access is sector aligned at the start
                if (firstSectorBytes != Geometry.BytesPerSector) {
                    var sector = ReadSectors(firstSector, 1);
                    int bytes = (int)Math.Min(firstSectorBytes, actualCount);
                    Array.Copy(sector, 0, buffer, offset, bytes);
                    firstSector++;
                    offset += bytes;
                    actualCount -= bytes;
                }

                // make sure the access is sector aligned at the end
                if (lastSectorBytes != Geometry.BytesPerSector && lastSector >= firstSector) {
                    var sector = ReadSectors(lastSector, 1);
                    Array.Copy(sector, 0, buffer, offset + actualCount - lastSectorBytes, lastSectorBytes);
                    lastSector--;
                    actualCount -= (int)lastSectorBytes;
                }

                if (firstSector <= lastSector)
                    using (var disk = OpenDisk(PInvoke.Access.GENERIC_READ | PInvoke.Access.GENERIC_WRITE))
                        PInvoke.ReadFile(disk, firstSector * (long)Geometry.BytesPerSector, buffer, offset, (int)actualCount);

                Position += result;
                return result;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Flush()
            {
                // todo: write to cache buffer
                throw new NotImplementedException();
            }

            */
            #endregion
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


        [AOSAction("mount", "isWrapper=true")] // windows can only mount disks that are actually native windows disks
        public DynamicSet<IVolume> Mount(IDisk disk)
        {
            var winDisk = disk.AsImplementation<WindowsDisk>();
            if (winDisk == null)
                throw new ArgumentException("This service can only mount volumes of a native Windows disk");

            var volumes = WindowsVolumeService.EnumerateVolumes()
                .Where(vol => vol.GetExtents()?.Any(extent => extent.Disk.AsImplementation<WindowsDisk>().Number == winDisk.Number) ?? false)
                .Select(vol => vol.VolumeRef).ToArray();
            return new DynamicSet<IVolume>(volumes).Retain();
        }
    }
}

