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
    [AOSService(
        "Partition Service",
        Description = "Exposes the volumes found on disks that are partitioned by the MBR or GPT scheme (GPT yet to be implemented)."
        )]
    class PartitionService
    {
        private class Volume : IVolumeImpl
        {
            public IVolume VolumeRef { get; }
            public DynamicEndpoint<VolumeInfo> Info { get; }
            public DynamicEndpoint<long> Size { get; }
            
            private readonly VolumeExtent[] extents;

            /// <summary>
            /// Bytes-per-sector metric of each extent
            /// </summary>
            private readonly long[] bytesPerSector;

            /// <summary>
            /// Length in bytes of each extent
            /// </summary>
            private readonly long[] extentLengths;

            [ContractInvariantMethod]
            protected void ObjectInvariant()
            {
                Contract.Invariant(extents != null);
                Contract.Invariant(bytesPerSector != null);
                Contract.Invariant(extentLengths != null);
            }

            /// <summary>
            /// Generates a new volume that consists of one or multiple extents.
            /// The extents can be on different disks.
            /// The read and write methods automatically operate on the correct extent(s).
            /// </summary>
            public Volume(IDisk disk, byte type, VolumeInfo info, VolumeExtent[] extents)
            {
                VolumeRef = new VolumeRef(this);
                
                this.extents = extents;

                bytesPerSector = extents.Select(e => e.Disk.Info.GetValue().BytesPerSector).ToArray();
                extentLengths = extents.Select((e, i) => e.Sectors * bytesPerSector[i]).ToArray();

                Info = new DynamicEndpoint<VolumeInfo>(info, PropertyAccess.ReadOnly);
                Size = new DynamicEndpoint<long>(() => extentLengths.Sum(), val => { throw new NotImplementedException(); });
            }

            public VolumeExtent[] GetExtents()
            {
                return extents.RetainAll();
            }

            private void DoOperation(long offset, long count, byte[] buffer, long bufferOffset, bool read)
            {
                for (int i = 0; count > 0;) {
                    if (i >= extents.Count())
                        throw new Exception("Attempt to read beyond the volume");

                    // skip extent as long as the offset is not within
                    if (offset >= extentLengths[i]) {
                        offset -= extentLengths[i++];
                        continue;
                    }

                    var extent = extents[i];
                    long effectiveCount;

                    var sector = extent.StartSector + offset / bytesPerSector[i];
                    var sectorOffset = offset % bytesPerSector[i];

                    if (sectorOffset != 0 || count < bytesPerSector[i]) {
                        // case 1: the operation is not sector aligned or less than a full sector
                        // in this case, we also have to read the sector when writing, as not to lose some of the data on the sector
                        effectiveCount = Math.Min(count, bytesPerSector[i] - sectorOffset);
                        var temp = new byte[bytesPerSector[i]];
                        extent.Disk.Read(extent.Track, sector, 1, temp, 0);

                        if (read) {
                            Array.Copy(temp, sectorOffset, buffer, bufferOffset, effectiveCount);
                        } else {
                            Array.Copy(buffer, bufferOffset, temp, sectorOffset, effectiveCount);
                            extent.Disk.Write(extent.Track, sector, 1, temp, 0);
                        }
                    } else {
                        // case 2: the operation is sector-aligned and at least as long as a sector
                        var sectorCount = Math.Min(count, extentLengths[i] - offset) / bytesPerSector[i];
                        Contract.Assert(sectorCount != 0);
                        if (read)
                            extent.Disk.Read(extent.Track, sector, sectorCount, buffer, bufferOffset);
                        else
                            extent.Disk.Write(extent.Track, sector, sectorCount, buffer, bufferOffset);
                        effectiveCount = sectorCount * bytesPerSector[i];
                    }

                    count -= effectiveCount;
                    offset += effectiveCount;
                    bufferOffset += effectiveCount;
                }

                Contract.Ensures(Contract.OldValue(bufferOffset) + count == bufferOffset);
            }

            public void Read(long offset, long count, byte[] buffer, long bufferOffset)
            {
                DoOperation(offset, count, buffer, bufferOffset, true);
            }

            public void Write(long offset, long count, byte[] buffer, long bufferOffset)
            {
                DoOperation(offset, count, buffer, bufferOffset, false);
            }

            public void ChangeSize(long sectorCount)
            {
                throw new NotImplementedException();
            }

            public void Flush()
            {
                foreach (var extent in extents)
                    extent.Disk.Flush();
            }
        }

        private List<string> ParseDisk(IDisk disk, bool verbose, out List<Volume> volumes)
        {
            DiskInfo diskInfo = disk.Info.GetValue();

            volumes = new List<Volume>(4);
            var issues = new List<string>();

            DebugLog("parsing partition tables...");

            for (int track = 0; track < diskInfo.Tracks; track++) {
                if (verbose)
                    DebugLog(string.Format("Track {0}:", track));

                // todo: test with 4k sectors
                var mbrSectors = Math.Max((512 + diskInfo.BytesPerSector - 1) / diskInfo.BytesPerSector, 1); // get the first sector, but at least 512 bytes
                var mbr = new byte[mbrSectors * diskInfo.BytesPerSector];
                disk.Read(track, 0, mbrSectors, mbr, 0);

                var diskID = mbr.ReadUInt32(0x01B8, Endianness.LittleEndian);

                var start = 0x01BE;
                if (mbr.ReadUInt16(0x017C, Endianness.LittleEndian) == 0xA55A)
                    start = 0x017E;
                if (mbr.ReadUInt16(0x00FC, Endianness.LittleEndian) == 0x55AA)
                    start = 0x00FE;

                if (mbr.ReadUInt16(0x01FE, Endianness.LittleEndian) != 0xAA55)
                    issues.Add("The signature at the end of the MBR is invalid (expected 0x55 0xAA).");

                bool haveGPT = false;

                for (int i = start; i < 0x01FE; i += 0x10) {
                    var status = mbr[i];
                    var type = mbr[i + 4];
                    if (type == 0)
                        continue;
                    if (type == 0xEE)
                        haveGPT = true;

                    var startSector = mbr.ReadUInt32(i + 0x8, Endianness.LittleEndian);
                    var volInfo = new VolumeInfo() {
                        ID = new Guid(diskID, (ushort)(startSector >> 16), (ushort)(startSector & 0xFFFF), 0, 0, 0, 0, 0, 0, 0, 0),
                        Type = new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, type),
                    };

                    var extent = new VolumeExtent() {
                        Track = track,
                        StartSector = startSector,
                        MaxSectors = mbr.ReadUInt32(i + 0xC, Endianness.LittleEndian),
                        Sectors = mbr.ReadUInt32(i + 0xC, Endianness.LittleEndian),
                        Disk = disk
                    };

                    if (extent.StartSector > diskInfo.Sectors || extent.Sectors > unchecked(diskInfo.Sectors - extent.StartSector))
                        issues.Add(string.Format("the partition entry at 0x{0:X2} points to a location beyond the disk", i));
                    else
                        volumes.Add(new Volume(disk, type, volInfo, new VolumeExtent[] { extent }));

                    DebugLog(string.Format("MBR partition entry at 0x{0:X2}:", i));
                    DebugLog(string.Format("ID: {0}", volInfo.ID));
                    DebugLog(string.Format("Status: 0x{0:X2}", status));
                    DebugLog(string.Format("Type: 0x{0:X2}{1}", type, type == 0xEE ? " (protective MBR for GPT)" : ""));
                    DebugLog(string.Format("Number of Sectors: {0} (0x{0:X16}), that's {1}", extent.Sectors, Utilities.GetSizeString(extent.Sectors * diskInfo.BytesPerSector, true)));
                    DebugLog(string.Format("Start Sector: {0} (0x{0:X16})", extent.StartSector));
                }

                // todo: check if volumes overlap, calculate max sector information

                if (haveGPT) {
                    // todo: instead of failing, read GPT (sector 1) and partition table
                    if (volumes.Count() <= 1) {
                        throw new AOSRejectException("The disk is partitioned using the GPT scheme. This is not yet supported (you can implement it though, looks quite easy", null, disk);
                        
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
                }
            }

            return issues;
        }

        [AOSAction("init")]
        public DynamicSet<IVolume> Init(IDisk disk)
        {
            // todo: locking
            List<Volume> list;
            var issues = ParseDisk(disk, false, out list);

            return new DynamicSet<IVolume>(list.Select(vol => vol.VolumeRef).ToArray()).Retain();
            //ObjectStore.PublishObject(volume, new Dictionary<string, string>() {
            //    { "type", volume.GetInfo().Type.ToString() }
            //});
            //ObjectStore.Action(volume, "info", shell, log);
        }

        [AOSAction("info")]
        public void Info(IDisk disk)
        {
            // todo: locking
            List<Volume> volumes;
            var issues = ParseDisk(disk, false, out volumes);

            if (issues.Count == 0)
                Log(string.Format("{0} volumes were found. The partitions seem to be healthy.", volumes.Count()), LogType.Success);
            else
                Context.CurrentContext.LogContext.Break();

            if (issues.Count > 1)
                Log("Multiple issues were found with the patitions:", LogType.Warning);

            foreach (var issue in issues)
                Log(issue, LogType.Warning);
        }

    }
}
