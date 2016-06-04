using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32.SafeHandles;
using AmbientOS.Environment;
using static AmbientOS.LogContext;

namespace AmbientOS.FileSystem
{
    [AOSObjectProvider()]
    [ForPlatform(PlatformType.Windows)]
    public class WindowsVolume : IVolumeImpl
    {
        public DynamicValue<Guid> ID { get; }
        public DynamicValue<FileSystemFlags> Flags { get; }
        public DynamicValue<string> Type { get; }
        public DynamicValue<long?> Length { get; }
        
        private readonly VolumeExtent[] extents;

        /// <summary>
        /// Returns a name of the form "\\?\Volume{GUID}".
        /// </summary>
        public string Name { get { return @"\\?\Volume{" + ID.Get() + "}"; } }


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
            ID = new LocalValue<Guid>(guid);
            Flags = new LocalValue<FileSystemFlags>(0); // todo: implement
            Type = new LocalValue<string>("volume:windows");

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
                        var disk = new WindowsDisk(e.DiskNumber);
                        return new VolumeExtent() {
                            Parent = disk.AsReference<IDisk>(),
                            StartBlock = e.StartingOffset / disk.BlockSize.Get(),
                            Blocks = e.ExtentLength / disk.BlockSize.Get(),
                            MaxSectors = e.ExtentLength / disk.BlockSize.Get()
                        };
                    }).ToArray();


                    // read general info
                    var gptBuf = new byte[8];
                    PInvoke.DeviceIoControl(volume, PInvoke.IOCTL_VOLUME_GET_GPT_ATTRIBUTES, null, gptBuf);
                    var attr = gptBuf.ReadUInt64(0, Endianness.Current);
                    // todo: convey GPT attributes

                    // read capacity
                    // none of these work in the general case
                    //var capacity = PInvoke.DeviceIoControl<PInvoke.StorageReadCapacity>(volume, PInvoke.IOCTL_STORAGE_READ_CAPACITY);
                    //var capacity = PInvoke.DeviceIoControl<long>(volume, PInvoke.IOCTL_DISK_GET_LENGTH_INFO);
                    Length = new LocalValue<long?>(extents.Extents.Sum(e => e.ExtentLength));
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

        public VolumeExtent[] GetExtents()
        {
            return extents;
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
            return ID.Get().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return ID.Get() == (obj as WindowsVolume)?.ID.Get();
        }


        /// <summary>
        /// Enumerates all Windows volumes on the system.
        /// </summary>
        public static IEnumerable<WindowsVolume> EnumerateVolumes()
        {
            foreach (var name in PInvoke.FindVolumes()) {
                WindowsVolume volume;
                try {
                    volume = new WindowsVolume(name);
                } catch (Exception ex) {
                    Log(string.Format("encountered faulty volume ({0}): {1}", name, ex), LogType.Warning);
                    continue;
                }
                yield return volume;
            }
        }

        /// <summary>
        /// Returns a reference to the file system on the specified Windows volume
        /// </summary>
        [AOSObjectProvider()]
        public static IFileSystem Mount([AOSObjectConstraint("Type", "volume:windows")] IVolume volume)
        {
            var volumeImpl = volume.AsImplementation<WindowsVolume>();
            if (volumeImpl == null)
                throw new ArgumentException("This service can only mount the filesystem of a native Windows volume");

            // .NET framework is not happy with the "?" in //?/Volume
            return new InteropFileSystem(volumeImpl.Name.Replace('?', '.'), (a, b) => {
                using (var fsRef = a.AsReference<IFileSystem>())
                    return new WindowsFolder(fsRef, b).AsReference<IFolder>();
            }).AsReference<IFileSystem>();
        }
    }
}
