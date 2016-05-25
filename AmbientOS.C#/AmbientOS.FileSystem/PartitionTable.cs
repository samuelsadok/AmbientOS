using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using AmbientOS.Environment;
using AmbientOS.Utils;
using static AmbientOS.LogContext;

namespace AmbientOS.FileSystem
{
    /// <summary>
    /// Exposes the volumes found on disks that are partitioned by the MBR scheme
    /// </summary>
    public class PartitionTable : IPartitionTableImpl
    {
        readonly Volume[] volumes;

        internal PartitionTable(Volume[] volumes)
        {
            this.volumes = volumes;
        }

        public DynamicSet<IVolume> GetPartitions()
        {
            return new DynamicSet<IVolume>(volumes.Select(vol => vol.AsReference<IVolume>()).ToArray()).Retain();
        }


        private static List<Volume> ParseDisk(IBlockStream disk, bool verbose, out List<string> issues)
        {
            var volumes = new List<Volume>(4);
            issues = new List<string>();

            DebugLog("parsing partition tables...");

            // todo: test with 4k sectors
            long bytesPerSector;
            long? totalSectors;
            DynamicProperty.GetValues(disk.BlockSize, out bytesPerSector, disk.BlockCount, out totalSectors);

            var mbrSectors = Math.Max((512 + bytesPerSector - 1) / bytesPerSector, 1); // get the first sector, but at least 512 bytes
            var mbr = new byte[mbrSectors * bytesPerSector];
            disk.ReadBlocks(0, mbrSectors, mbr, 0);

            var diskID = mbr.ReadUInt32(0x01B8, Endianness.LittleEndian);

            var start = 0x01BE;
            if (mbr.ReadUInt16(0x017C, Endianness.LittleEndian) == 0xA55A)
                start = 0x017E;
            if (mbr.ReadUInt16(0x00FC, Endianness.LittleEndian) == 0x55AA)
                start = 0x00FE;

            if (mbr.ReadUInt16(0x01FE, Endianness.LittleEndian) != 0xAA55)
                issues.Add("The signature at the end of the MBR is invalid (expected 0x55 0xAA).");

            uint? gptStart = null;

            for (int i = start; i < 0x01FE; i += 0x10) {
                var status = mbr[i];

                var type = mbr[i + 4];
                if (type == 0)
                    continue;


                var startSector = mbr.ReadUInt32(i + 0x8, Endianness.LittleEndian);
                var sectors = mbr.ReadUInt32(i + 0xC, Endianness.LittleEndian);
                var id = new Guid(diskID, (ushort)(startSector >> 16), (ushort)(startSector & 0xFFFF), 0, 0, 0, 0, 0, 0, 0, 0);

                if (type == 0xEE) { // protective partition for GPT scheme
                    gptStart = startSector + 1; // the GPT header always starts at sector 1 (and the protective partition starts at sector 0)
                    continue;
                }


                if (startSector > totalSectors) {
                    issues.Add(string.Format("the partition entry at 0x{0:X2} points to a location beyond the disk", i));
                } else if (startSector + sectors > totalSectors) {
                    issues.Add(string.Format("the partition entry at 0x{0:X2} extends beyond the disk", i));
                } else {
                    var extent = new VolumeExtent() {
                        Parent = disk,
                        StartBlock = startSector,
                        Blocks = sectors,
                        MaxSectors = sectors
                    };
                    volumes.Add(new Volume(id, 0, string.Format("mbr:{0:2X}", type), extent));
                }

                DebugLog(string.Format("MBR partition entry at 0x{0:X2}:", i));
                DebugLog(string.Format("ID: {0}", id));
                DebugLog(string.Format("Status: 0x{0:X2}", status));
                DebugLog(string.Format("Type: 0x{0:X2}{1}", type, type == 0xEE ? " (protective MBR for GPT)" : ""));
                DebugLog(string.Format("Number of Sectors: {0} (0x{0:X16}), that's {1}", sectors, Utilities.GetSizeString(sectors * bytesPerSector, true)));
                DebugLog(string.Format("Start Sector: {0} (0x{0:X16})", startSector));
            }


            if (gptStart == null)
                return volumes;




            // todo: instead of failing, read GPT (sector 1) and partition table
            if (volumes.Count() <= 1) {
                throw new AOSRejectException("The disk is partitioned using the GPT scheme. This is not yet supported (you can implement it though, looks quite easy", disk);

                /*
                var gptHeader = this.ReadSectors(1, 1);
                var listStart = gptHeader.ReadUInt64(0x48, Endianness.LittleEndian);
                var listLength = gptHeader.ReadInt32(0x50, Endianness.LittleEndian);
                var elementSize = gptHeader.ReadInt32(0x54, Endianness.LittleEndian);

                this.Seek((int)(listStart * Geometry.BytesPerSector), SeekOrigin.Begin);
                var data = this.ReadBytes((int)(listLength * elementSize), ApplicationControl.ShutdownToken).WaitForResult(ApplicationControl.ShutdownToken);

                var result = new List<VolumeInfo>(listLength);

                for (int i = 0; i < listLength; i++) {
                    var entry = data.Skip(i * elementSize).Take(elementSize).ToArray();
                    var firstSector = entry.ReadUInt64(0x20, Endianness.LittleEndian);

                    if (firstSector != 0) {
                        result.Add(new VolumeInfo() {
                            Offset = firstSector * this.Geometry.BytesPerSector,
                            Length = (entry.ReadUInt64(0x28, Endianness.LittleEndian) + 1 - firstSector) * this.Geometry.BytesPerSector,
                            Name = System.Text.Encoding.Unicode.GetString(entry.Skip(0x38).TakeWhile((b) => b != 0).ToArray())
                        });
                    }
                }

                return result.ToArray();*/

            } else {
                issues.Add("The disk uses the GPT scheme, but seems to have some legacy partitions. Ignoring GPT for now (not implemented).");
            }

            return volumes;
        }

        [AOSObjectProvider()]
        public static IPartitionTable Init(IBlockStream disk)
        {
            // todo: locking
            List<string> issues;
            var volumes = ParseDisk(disk, false, out issues);

            var allExtents = volumes.SelectMany(vol => vol.GetExtents().Select(ext => new { extent = ext, volume = vol })).ToArray();
            var allExtentCombinations = allExtents.SelectMany(ext1 => allExtents.Select(ext2 => new { ext1 = ext1, ext2 = ext2 }));

            Func<VolumeExtent, VolumeExtent, bool> overlap = (ext1, ext2) =>
                ((ext1.StartBlock <= ext2.StartBlock) && (ext1.StartBlock + ext1.Blocks > ext2.StartBlock)) ||
                ((ext2.StartBlock <= ext1.StartBlock) && (ext2.StartBlock + ext2.Blocks > ext1.StartBlock));
            var anyOverlap = allExtentCombinations.Where(comb => comb.ext1.volume != comb.ext2.volume).Any(comb => overlap(comb.ext1.extent, comb.ext2.extent));

            if (anyOverlap)
                issues.Add("There are overlapping volumes on the disk");

            return new PartitionTable(volumes.ToArray()).AsReference<IPartitionTable>();
        }
    }
}
