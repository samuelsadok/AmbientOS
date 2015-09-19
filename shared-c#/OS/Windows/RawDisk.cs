using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Management;
using System.IO;
using AppInstall.Framework;

namespace AppInstall.OS
{
    // todo: create block stream class to abstract the read and write functions
    public class RawDisk : Stream
    {

        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct DiskGeometry {
            public Int64 Cylinders;
            public Int32 MediaType;
            public Int32 TracksPerCylinder;
            public Int32 SectorsPerTrack;
            public UInt32 BytesPerSector;
            public Int64 Capacity { get { return Cylinders * TracksPerCylinder * SectorsPerTrack * BytesPerSector; } }
        }


        public struct VolumeInfo
        {
            public byte Type;
            public UInt64 Offset;
            public UInt64 Length; // only valid for MBR partitions
            public string Name; // only valid for GPT partitions
        }


        string device;
        DiskGeometry geometry = new DiskGeometry();


        public string Name { get { return device; } }
        public string Mapping { get { return GetDeviceMapping(device); } }
        public string Caption { get { return (string)GetManagementObjectForDisk(device)["Caption"]; } }
        public string MediaType { get { return (string)GetManagementObjectForDisk(device)["MediaType"]; } }
        public DiskGeometry Geometry { get { return geometry; } }



        private PInvoke.SafeFileHandle OpenDisk(PInvoke.Access access)
        {
            return PInvoke.CreateFile("\\\\.\\" + device, access, PInvoke.ShareMode.FILE_SHARE_READ | PInvoke.ShareMode.FILE_SHARE_WRITE, IntPtr.Zero, PInvoke.CreationDisposition.OPEN_EXISTING, PInvoke.FileFlags.OVERLAPPED, IntPtr.Zero);
        }

        /// <summary>
        /// Returns a list of every single device connected to the PC.
        /// </summary>
        private static string[] ListDevices() {
            return PInvoke.QueryDosDevice(null);
        }

        private static string[] ListPhysicalDrives() {
            return ListDevices().Where((string device) => device.StartsWith("PhysicalDrive")).ToArray();
        }

        private static string GetDeviceMapping(string device) {
            return PInvoke.QueryDosDevice(device).Single();
        }

        private static ManagementObject GetManagementObjectForDisk(string device)
        {
            WqlObjectQuery q = new WqlObjectQuery("SELECT * FROM Win32_DiskDrive");
            ManagementObjectSearcher res = new ManagementObjectSearcher(q);
            foreach (ManagementObject o in res.Get()) {
                // properties include: Caption & Model (normally identical), DeviceID & Name (normally identical), Description, Manufacturer, MediaType, SerialNumber (Vista or higher)
                if (((string)o["DeviceID"]).ToLower() == ("\\\\.\\" + device).ToLower()) return o;
            }
            return null;
        }


        public static RawDisk[] AllDisks() {
            List<RawDisk> result = new List<RawDisk>();
            foreach (string dev in RawDisk.ListPhysicalDrives()) {
                try {
                    result.Add(new RawDisk(dev));
                } catch (Exception) { }
            }
            return result.ToArray();
        }


        public RawDisk(string device)
        {
            this.device = device;

            // get drive info
            using (var disk = OpenDisk(0)) {
                int bytesReturned = 0;

                using (PInvoke.Win32Memory pGeom = new PInvoke.Win32Memory(Marshal.SizeOf(geometry))) {
                    PInvoke.DeviceIoControl(disk, PInvoke.IOCTL_DISK_GET_DRIVE_GEOMETRY, null, pGeom, ref bytesReturned, (new PInvoke.Overlapped()));
                    geometry = (DiskGeometry)Marshal.PtrToStructure(pGeom.Pointer, typeof(DiskGeometry));
                }
            }
        }


        public void Dismount() {
            
        }


        public byte[] ReadSectors(long firstSector, long count)
        {
            byte[] result = new byte[count * (long)Geometry.BytesPerSector];
            using (var disk = OpenDisk(PInvoke.Access.GENERIC_READ | PInvoke.Access.GENERIC_WRITE))
                PInvoke.ReadFile(disk, firstSector * (long)Geometry.BytesPerSector, result, 0, result.Count());
            return result;
        }


        /// <summary>
        /// Writes data to the disk
        /// </summary>
        /// <param name="preserve">Specifies a set of the areas that should be preserved. The first element of the tuple specifies the start address relative to the entire block, the second element specifies the length.</param>
        public void Write(byte[] data, long startAddress, Tuple<long, long>[] preserve)
        {
            // check inputs
            if (startAddress < 0) throw new ArgumentException("startAddress");
            foreach (Tuple<long, long> area in preserve)
                if ((area.Item1 < 0) || (area.Item1 + area.Item2 > startAddress + data.Count())) throw new ArgumentException("preserve (" + area.Item1 + ", " + area.Item2 + ")");

            // make the data sector-aligned
            long move = 0;
            startAddress = Math.DivRem(startAddress, (long)Geometry.BytesPerSector, out move) * (long)Geometry.BytesPerSector;

            List<Tuple<long, long>> newPreserve = new List<Tuple<long, long>>();
            if (move != 0) newPreserve.Add(new Tuple<long, long>(0, move));
            for (int i = 0; i < preserve.Count(); i++)
                newPreserve.Add(new Tuple<long, long>(preserve[i].Item1 + move, preserve[i].Item2));

            long overhead = 0;
            long count = Math.DivRem(data.Count() + move, (long)Geometry.BytesPerSector, out overhead) * (long)Geometry.BytesPerSector;

            if (overhead != 0) {
                newPreserve.Add(new Tuple<long, long>(count + overhead, (long)Geometry.BytesPerSector - overhead));
                count += (long)Geometry.BytesPerSector;
            }


            // create new buffers
            byte[] oldData = new byte[count];
            byte[] newData = new byte[count];
            Array.Copy(data, 0, newData, move, data.Count());


            // read data from disk
            using (var disk = OpenDisk(PInvoke.Access.GENERIC_READ | PInvoke.Access.GENERIC_WRITE)) {
                PInvoke.ReadFile(disk, startAddress, oldData, 0, oldData.Count());

                // merge data
                foreach (Tuple<long, long> area in newPreserve)
                    Array.Copy(oldData, area.Item1, newData, area.Item1, area.Item2);


                // write new data to disk
                PInvoke.WriteFile(disk, newData, startAddress);
            }
        }


        /// <summary>
        /// Returns the 4 partitions described by the partition table in the MBR.
        /// </summary>
        public VolumeInfo[] GetMBRPartitions()
        {
            this.Seek(0, SeekOrigin.Begin);
            var mbr = this.ReadBytes(512, ApplicationControl.ShutdownToken).WaitForResult(ApplicationControl.ShutdownToken);

            var result = new VolumeInfo[4];
            int index = 0;

            for (int i = 0x01BE; i < 0x01FE; i += 0x10) {
                result[index++] = new VolumeInfo() {
                    Type = mbr[i + 4],
                    Offset = ByteConverter.ToUInt32LE(mbr, i + 0x8) * this.Geometry.BytesPerSector,
                    Length = ByteConverter.ToUInt32LE(mbr, i + 0xC) * this.Geometry.BytesPerSector,
                    Name = "[mbr]"
                };
            }

            return result;
        }

        /// <summary>
        /// Returns the partitions described by the GUID Partition Table.
        /// </summary>
        public VolumeInfo[] GetGPTPartitions()
        {
            var gptHeader = this.ReadSectors(1, 1);
            var listStart = ByteConverter.ToUInt64LE(gptHeader, 0x48);
            var listLength = (int)ByteConverter.ToUInt32LE(gptHeader, 0x50);
            var elementSize = (int)ByteConverter.ToUInt32LE(gptHeader, 0x54);

            this.Seek((int)(listStart * Geometry.BytesPerSector), SeekOrigin.Begin);
            var data = this.ReadBytes((int)(listLength * elementSize), ApplicationControl.ShutdownToken).WaitForResult(ApplicationControl.ShutdownToken);

            var result = new List<VolumeInfo>(listLength);

            for (int i = 0; i < listLength; i++) {
                var entry = data.Skip(i * elementSize).Take(elementSize).ToArray();
                var firstSector = ByteConverter.ToUInt64LE(entry, 0x20);

                if (firstSector != 0) {
                    result.Add(new VolumeInfo() {
                        Offset = firstSector * this.Geometry.BytesPerSector,
                        Length = (ByteConverter.ToUInt64LE(entry, 0x28) + 1 - firstSector) * this.Geometry.BytesPerSector,
                        Name = System.Text.Encoding.Unicode.GetString(entry.Skip(0x38).TakeWhile((b) => b != 0).ToArray())
                    });
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Determines whether this disk uses a legacy MBR or a GPT and returns the partitions described in the respective structure.
        /// </summary>
        public VolumeInfo[] GetPartitions()
        {
            var mbrPartitions = GetMBRPartitions();
            if (mbrPartitions.Any((part) => part.Type == 0xEE))
                return GetGPTPartitions();
            else
                return mbrPartitions;
        }



        #region "Stream Implementation"

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


        #endregion
    }
}

