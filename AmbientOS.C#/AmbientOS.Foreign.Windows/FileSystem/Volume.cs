using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32.SafeHandles;
using AmbientOS.Environment;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem
{
    [AOSApplication("Windows Volume Service", Description = "Exposes the volumes managed by Windows")]
    public class WindowsVolumeService
    {
        public class WindowsVolume : IVolumeImpl
        {
            public IVolume VolumeRef { get; }

            private readonly VolumeInfo info;
            private readonly VolumeExtent[] extents;
            private readonly long capacity;

            /// <summary>
            /// Returns a name of the form "\\?\Volume{GUID}".
            /// </summary>
            public string Name { get { return @"\\?\Volume{" + Guid + "}"; } }
            public Guid Guid { get; }


            private SafeFileHandle OpenVolume(PInvoke.Access access)
            {
                return PInvoke.CreateFile(Name, access, PInvoke.ShareMode.ReadWrite, IntPtr.Zero, PInvoke.CreationDisposition.OPEN_EXISTING, PInvoke.FileFlags.OVERLAPPED, IntPtr.Zero);
            }

            private static Guid GetVolumeGuid(string name)
            {
                string[] PREFIXES = new string[] { @"\\?\", @"\\.\" };
                const string PREFIX = "Volume{";
                var str = name.Substring(PREFIXES.FirstOrDefault(p => name.StartsWith(p))?.Length ?? 0).TrimEnd('\\').TrimEnd('}');
                if (str.StartsWith(PREFIX))
                    str = str.Substring(PREFIX.Length);

                Guid guid;
                if (!Guid.TryParse(str, out guid))
                    throw new Exception(string.Format("The path \"{0}\" is not a valid Windows volume name.", name));
                return guid;
            }

            /// <summary>
            /// Generates a Windows volume object from the provided Guid.
            /// </summary>
            public WindowsVolume(Guid guid)
            {
                VolumeRef = new VolumeRef(this);
                Guid = guid;

                // Returns the mountpoints of this volume (pops up an undesirable message on some volumes)
                //var mountpoints = PInvoke.FindVolumeMountPoints(Name + @"\").ToArray();

                // Returns FS name (fails with "Device not ready" on some volumes)
                //var fs = PInvoke.GetVolumeInformation(Name + @"\");

                // returns all root paths of the volume
                var roots = PInvoke.GetVolumePathNamesForVolumeName(Name + @"\");

                try {
                    using (var volume = OpenVolume(0)) {

                        // read extents (fails with "Incorrect function" on some volumes)
                        var buffer = new byte[8 + 0x18];
                        if (PInvoke.DeviceIoControl(volume, PInvoke.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, null, buffer, PInvoke.ERROR.MORE_DATA) == PInvoke.ERROR.MORE_DATA) {
                            buffer = new byte[8 + 0x18 * buffer.ReadInt32(0, Endianness.Current)];
                            PInvoke.DeviceIoControl(volume, PInvoke.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, null, buffer);
                        }

                        var extents = buffer.ReadObject<PInvoke.VolumeDiskExtents>(0, Endianness.Current);
                        this.extents = extents.Extents.Select(e => {
                            var disk = new WindowsDiskService.WindowsDisk(e.DiskNumber);
                            var diskInfo = disk.GetInfo();
                            return new VolumeExtent() {
                                Track = 0,
                                Disk = disk.DiskRef.Retain(),
                                StartSector = e.StartingOffset / diskInfo.BytesPerSector,
                                MaxSectors = e.StartingOffset / diskInfo.BytesPerSector,
                                Sectors = e.ExtentLength / diskInfo.BytesPerSector
                            };
                        }).ToArray();


                        // read general info
                        var gptBuf = new byte[8];
                        PInvoke.DeviceIoControl(volume, PInvoke.IOCTL_VOLUME_GET_GPT_ATTRIBUTES, null, gptBuf);
                        var attr = gptBuf.ReadUInt64(0, Endianness.Current);
                        // todo: convey GPT attributes

                        info = new VolumeInfo() {
                            ID = guid,
                            Type = new Guid()
                        };

                        // read capacity
                        // none of these work in the general case
                        //var capacity = PInvoke.DeviceIoControl<PInvoke.StorageReadCapacity>(volume, PInvoke.IOCTL_STORAGE_READ_CAPACITY);
                        //var capacity = PInvoke.DeviceIoControl<long>(volume, PInvoke.IOCTL_DISK_GET_LENGTH_INFO);
                        capacity = extents.Extents.Sum(e => e.ExtentLength);
                    }

                } catch (System.ComponentModel.Win32Exception) {
                    // todo: log error


                }
            }

            /// <summary>
            /// Generates a Windows volume object from the provided name.
            /// </summary>
            /// <param name="name">A name of the form \\.\Volume{GUID}\ (prefix and suffix are optional)</param>
            public WindowsVolume(string name)
                : this(GetVolumeGuid(name))
            {
            }

            public VolumeInfo GetInfo()
            {
                return info;
            }

            public VolumeExtent[] GetExtents()
            {
                return extents;
            }

            public long GetSize()
            {
                return capacity;
            }

            public long SetSize(long size)
            {
                throw new NotImplementedException("Can't change size of a Windows volume. To implement support for this, take at a look at the DeviceIoControl operations FSCTL_EXTEND_VOLUME and IOCTL_DISK_GROW_PARTITION.");
            }

            public void Read(long offset, long count, byte[] buffer, long bufferOffset)
            {
                using (var volume = OpenVolume(PInvoke.Access.Read))
                    PInvoke.ReadFile(volume, offset, buffer, (int)bufferOffset, (int)count);
            }

            /// <summary>
            /// Writes to the Windows volume.
            /// Various restrictions apply. For details see the MSDN documentation of WriteFile (and look for the notes on volumes).
            /// todo: implement volume locking
            /// </summary>
            public void Write(long offset, long count, byte[] buffer, long bufferOffset)
            {
                using (var volume = OpenVolume(PInvoke.Access.Write))
                    PInvoke.WriteFile(volume, offset, buffer, (int)bufferOffset, (int)count);
            }

            public void Flush()
            {
                // the handle is closed after each write, which presumably flushes the cache
            }

            public override int GetHashCode()
            {
                return Guid.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return Guid == (obj as WindowsVolume)?.Guid;
            }
        }


        /// <summary>
        /// Enumerates all Windows volumes on the system.
        /// </summary>
        public static IEnumerable<WindowsVolume> EnumerateVolumes(LogContext log)
        {
            foreach (var name in PInvoke.FindVolumes()) {
                WindowsVolume volume;
                try {
                    volume = new WindowsVolume(name);
                } catch (Exception ex) {
                    log.Log(string.Format("encountered faulty volume ({0}): {1}", name, ex), LogType.Warning);
                    continue;
                }
                yield return volume;
            }
        }


        [AOSAction("mount", "isWrapper=true")]
        public DynamicSet<IFileSystem> Mount(IVolume disk, Context context)
        {
            var winVol = disk.AsImplementation<WindowsVolume>();
            if (winVol == null)
                throw new ArgumentException("This service can only mount the filesystem of a native Windows volume");

            // .NET framework is not happy with the "?" in //?/Volume
            return new DynamicSet<IFileSystem>(
                new Foreign.InteropFileSystem(winVol.Name.Replace('?', '.'), (a, b) => {
                    using (var fs = a.FileSystemRef.Retain())
                        return new WindowsFolder(fs, b).FolderRef.Retain();
                }).FileSystemRef).Retain();
        }
    }
}
