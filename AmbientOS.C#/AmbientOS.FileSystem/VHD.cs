using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using AmbientOS.Environment;
using AmbientOS.UI;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem
{
    /// <remarks>
    /// Official VHD and VHDX specifications are available from Microsoft.
    /// </remarks>
    [AOSService(
        "VHD Image Service",
        Description="Opens VHD image files (*.vhd) and makes them available as a disk. The differencing disk image part of the specification is not yet implemented."
        )]
    class VHDService
    {

        public class VHD : IDiskImpl
        {
            public IDisk DiskRef { get; }
            public DynamicEndpoint<DiskInfo> Info { get; }

            private IFile file;
            private Guid guid;
            private long sectorCount;
            private uint[] BAT; // block allocation table: for each block of sectors, contains the corresponding absolute sector number in the file
            private uint sectorsPerBlock;

            public VHD(IFile file, Guid guid, long sectorCount, uint[] bat, uint sectorsPerBlock)
            {
                DiskRef = new DiskRef(this);

                this.file = file;
                this.guid = guid;
                this.sectorCount = sectorCount;
                BAT = bat;
                this.sectorsPerBlock = sectorsPerBlock;

                Info = new DynamicEndpoint<DiskInfo>(() => new DiskInfo() {
                    BytesPerSector = 512,
                    ID = guid,
                    Tracks = 1,
                    Sectors = sectorCount,
                    MaxSectors = sectorCount
                },
                val => { throw new NotImplementedException(); });
            }

            public void DoOperation(int track, long offset, long count, byte[] buffer, long bufferOffset, bool read)
            {
                if (track != 0)
                    throw new ArgumentOutOfRangeException($"{track}", track, "VHDs only have one track");
                if (bufferOffset + count * 512 > buffer.Length)
                    throw new ArgumentException("The buffer is too small");

                if (BAT == null) {
                    if (read)
                        file.Read(offset * 512, count * 512, buffer, bufferOffset);
                    else
                        file.Write(offset * 512, count * 512, buffer, bufferOffset);
                    return;
                }

                var blockOffset = offset % sectorsPerBlock;
                var blockNr = (offset - blockOffset) / sectorsPerBlock;

                while (count > 0) {
                    if (blockNr >= BAT.Count())
                        throw new IndexOutOfRangeException();

                    var batEntry = BAT[blockNr];

                    if (batEntry != 0xFFFFFFFF) {
                        var absoluteOffset = (batEntry + blockOffset + (long)(Math.Ceiling((double)(sectorsPerBlock / 8) / 512))) * 512;
                        if (read)
                            file.Read(absoluteOffset, 512, buffer, bufferOffset);
                        else
                            file.Write(absoluteOffset, 512, buffer, bufferOffset);
                    } else {
                        if (read)
                            Array.Clear(buffer, (int)bufferOffset, 512);
                        else
                            throw new NotImplementedException("Can't write to an unallocated block yet, you're welcome to implement it, it shouldn't be hard.");
                    }

                    bufferOffset += 512;
                    if (++blockOffset >= sectorsPerBlock) {
                        blockOffset = 0;
                        blockNr++;
                    }
                }
            }


            public void Read(int track, long offset, long count, byte[] buffer, long bufferOffset)
            {
                DoOperation(track, offset, count, buffer, bufferOffset, true);
            }


            public void Write(int track, long offset, long count, byte[] buffer, long bufferOffset)
            {
                DoOperation(track, offset, count, buffer, bufferOffset, false);
            }

            public void Flush()
            {
                file.Flush();
            }
        }

        [Endianness(Endianness.BigEndian)]
        private struct VHDHeader
        {
            [FieldSpecs(StringFormat = StringFormat.ASCII, Length = 8)]
            public string cookie;
            public Int32 features;
            public Int32 version;
            public Int64 offset;
            public Int32 timeStamp;
            public Int32 creatorApp;
            public Int32 creatorVersion;
            public Int32 creatorOS;
            public Int64 origSize;
            public Int64 currSize;
            public Int16 diskC;
            public byte diskH;
            public byte diskS;
            public UInt32 diskType;
            public Int32 checksum;
            public Guid guid;
            public Int32 savedState;
        }

        [Endianness(Endianness.BigEndian)]
        private struct VHDDynamicHeader
        {
            [FieldSpecs(StringFormat = StringFormat.ASCII, Length = 8)]
            public string cookie;
            public Int64 offset;
            public Int64 tableOffset;
            public UInt32 version;
            public UInt32 maxTableEntries;
            public UInt32 blockSize;
            public UInt32 checksum;
            public Guid parentGuid;
            public UInt32 parentTimeStamp;
            public UInt32 reserved;

            [FieldSpecs(Length = 8)]
            public ParentLocatorEntry[] parentLocatorEntries;

            public class ParentLocatorEntry {
                public UInt32 platformCode;
                public UInt32 dataSpace;
                public UInt32 dataLength;
                public UInt32 reserved;
                public Int64 dataOffset;
            };
        }

        /// <summary>
        /// Opens a VHD image.
        /// Returns a list of messages about issues that occurred.
        /// </summary>
        /// <param name="info">If not null, receives human readable information about the VHD image.</param>
        private VHD ParseHeader(IFile file, LogContext info, out List<string> issues)
        {
            var fileLength = file.Size.GetValue();
            if (fileLength.HasValue)
                if (fileLength.Value < 2 * 511)
                    throw new AOSRejectException("The file is too short to be a VHD image. A sane VHD image has at least header and a footer of 511 or 512 bytes length.", null, file);


            issues = new List<string>();

            byte[] footer = file.Read(fileLength.Value - 512, 512);
            bool usingFooter = true;

            // Checksum validation: Old applications (before MS Virtual PC 2004) generate a 511 byte footer instead of 512 bytes.
            // Furthermore, the specification dictates to look at the footer first, and if it's corrupt, to use the header instead.
            if (!ByteConverter.ValidateInt32Checksum(footer, 0, 512, 64, Endianness.BigEndian)) {
                var footer511 = footer.Skip(1).ToArray();
                if (!ByteConverter.ValidateInt32Checksum(footer511, 0, 511, 64, Endianness.BigEndian)) {
                    var header512 = file.Read(0, 512);
                    if (!ByteConverter.ValidateInt32Checksum(header512, 0, 512, 64, Endianness.BigEndian)) {
                        issues.Add("Both header and footer checksums are invalid. The VHD image must be considered corrupt.");
                    } else {
                        issues.Add("The footer checksum is invalid (for both 511 and 512 bytes length). Using header instead.");
                        footer = header512;
                        usingFooter = false;
                    }
                } else {
                    footer = footer511;
                }
            }


            var header = ByteConverter.ReadObject<VHDHeader>(footer, 0);


            if ((header.features & 0xFFFFFFFE) != 2)
                issues.Add(string.Format("The features field (offset 8) should have bit 1 set and bits 2 through 31 cleared (have 0x{0:X8})", header.version));

            if (header.version != 0x00010000)
                issues.Add(string.Format("Version should be 0x00010000 (have 0x{0:X8})", header.version));

            if (fileLength.HasValue)
                if (header.currSize != fileLength.Value - footer.Length)
                    issues.Add(string.Format("There is a mismatch between the indicated disk size and the actual image file size."));

            if (!new uint[] { 2, 3, 4 }.Contains(header.diskType))
                issues.Add(string.Format("Unknown disk type (expected 2, 3 or 4, have 0x{0:X8}). Will assume fixed size.", header.diskType));

            if (header.diskType != 2 && ((1024 > fileLength - footer.Length) || (header.offset > unchecked(fileLength - footer.Length - 1024))))
                issues.Add(string.Format("The data offset points to a location beyond the file boundaries (file length without footer is 0x{0:X16}, offset is 0x{1:X16}, data header length is 1024)", fileLength - footer.Length, header.offset));

            if (footer.Skip(85).Any(b => b != 0))
                issues.Add("Expected all zeros in the reserved region of the footer");

            if ((header.currSize & 0x1FF) != 0)
                issues.Add(string.Format("The current-size field should be a multiple of 512 (have 0x{0:X16})", header.currSize));

            // The CHS product isn't always exact, so this check had to be removed.
            //if ((ulong)diskC * diskH * diskS != currSize / 512)
            //    issues.Add("The current-size field doesn't match cylinders * heads * sectorsPerTrack * sectorSize");

            info.Debug("VHD {0} (length: {1}):", usingFooter ? "Footer" : "Header", footer.Length);
            info.Debug("Cookie: \"{0}\"", Encoding.ASCII.GetString(footer.Take(8).ToArray()));
            info.Debug("Features: 0x{0:X8}{1}", header.features, (header.features & 1) != 0 ? " (temporary)" : "");
            info.Debug("Version: 0x{0:X8}", header.version);
            info.Debug("Data Offset: 0x{0:X16}", header.offset);
            info.Debug("Time Stamp: 0x{0:X8} ({1})", header.timeStamp, new DateTime(2000, 1, 1, 0, 0, 0).AddSeconds(header.timeStamp).ToString());
            info.Debug("Creator Application: 0x{0:X8} ({1})", header.creatorApp, header.creatorApp == 0x00001337 ? "AmbientOS VHD Service" : "unknown");
            info.Debug("Creator Version: 0x{0:X8}", header.creatorVersion);
            info.Debug("Creator OS: 0x{0:X8} ({1})", header.creatorOS, header.creatorOS == 0x5769326B ? "Windows" : header.creatorOS == 0x4D616320 ? "Mac" : "unknown");
            info.Debug("Original Virtual Size (at creation time): {0}", Utilities.GetSizeString(header.origSize, true));
            info.Debug("Current Virtual Size: {0}", Utilities.GetSizeString(header.currSize, true));
            info.Debug("Disk Geometry: {0} cylinders, {1} heads, {2} sectors per track", header.diskC, header.diskH, header.diskS);
            info.Debug("Disk Type: {0} ({1})", header.diskType, header.diskType < 7 ? new string[] { "none", "deprecated", "fixed", "dynamic", "differencing", "deprecated", "deprecated" }[header.diskType] : "unknown");
            info.Debug("Checksum: 0x{0:X8}", header.checksum);
            info.Debug("Saved State: {0}", header.savedState);
            info.Debug("Unique ID: {0}", header.guid);


            // a fixed disk doesn't have any more structures
            if (header.diskType != 3 && header.diskType != 4)
                return new VHD(file, header.guid, header.currSize / 512, null, 0);

            if ((header.offset & 0x1FF) != 0)
                issues.Add("the dynamic disk header should be 512 byte aligned");

            var dynheader = file.Read(header.offset, 1024);

            if (!ByteConverter.ValidateInt32Checksum(dynheader, 0, 1024, 36, Endianness.BigEndian))
                issues.Add("The checksum of the dynamic disk header is invalid. The VHD image must be considered corrupt.");

            var header2 = ByteConverter.ReadObject<VHDDynamicHeader>(footer, 0);


            if (header2.offset != 0xFFFFFFFF)
                issues.Add(string.Format("Header Version should be 0xFFFFFFFF (have 0x{0:X8})", header2.offset));

            if (header2.version != 0x00010000)
                issues.Add(string.Format("Header Version should be 0x00010000 (have 0x{0:X8})", header2.version));

            if (header2.maxTableEntries * header2.blockSize != header.currSize)
                issues.Add(string.Format("max-table-entries ({0}) * block-size ({1}) does not equal current-size ({2})", header2.maxTableEntries, header2.blockSize, header.currSize));

            if (header2.maxTableEntries * 4 > fileLength || header2.tableOffset > unchecked(fileLength - header2.maxTableEntries * 4))
                issues.Add("the bitmap allocation table does not fit in the VHD image");

            ulong x = header2.blockSize >> 9;
            if (((header2.blockSize & 0x1FF) != 0) || ((x & (x - 1)) != 0))
                issues.Add(string.Format("the number of sectors that fit into a block must be a power of two (sector size 512, block size 0x{0:X16})", header2.blockSize));

            if (header2.reserved != 0)
                issues.Add("some reserved field that should be zero, isn't");

            if (dynheader.Skip(768).Any(b => b != 0))
                issues.Add("the reserved area in the dynamic disk header should be zero");

            if (header2.parentLocatorEntries.Any(e => e.reserved != 0))
                issues.Add("the reserved fields in the parent locator entries should all be zero");

            if (header.diskType != 4 && dynheader.Skip(64 + 512).Take(8 * 24).Any(b => b != 0))
                issues.Add("the parent locator entries should be 0 for anything other than differencing images");

            if (header.diskType == 4 && (header2.parentLocatorEntries.Any(e => e.dataLength > fileLength || e.dataOffset > unchecked(fileLength - e.dataLength))))
                issues.Add("some parent locator entries point beyond file boundaries");


            info.Break();
            info.Debug("Dynamic Disk Header:");
            info.Debug("Cookie: \"{0}\"", Encoding.ASCII.GetString(dynheader.Take(8).ToArray()));
            info.Debug("Data Offset (not used): 0x{0:X16}", header2.offset);
            info.Debug("Bitmap Allocation Table Offset (not used): 0x{0:X16}", header2.tableOffset);
            info.Debug("Header Version: 0x{0:X8}", header2.version);
            info.Debug("Max Table Entries: {0}", header2.maxTableEntries);
            info.Debug("Block Size: {0}", header2.blockSize);
            info.Debug("Checksum: 0x{0:X8}", header2.checksum);
            info.Debug("Parent Unique ID: {0}", header2.parentGuid);
            info.Debug("Parent Time Stamp: 0x{0:X8} ({1})", header2.parentTimeStamp, new DateTime(2000, 1, 1, 0, 0, 0).AddSeconds(header2.parentTimeStamp).ToString());
            info.Debug("Parent Unicode Name: \"{0}\"", Encoding.Unicode.GetString(dynheader.Skip(64).Take(512).ToArray()));
            foreach (var locator in header2.parentLocatorEntries) {
                info.Debug("Parent Locator: platform code: 0x{0:X8}, space: {1} sectors, length: {2} bytes, offset: 0x{3:X16}",
                    locator.platformCode,
                    locator.dataSpace,
                    locator.dataLength,
                    locator.dataOffset
                    );
            }


            // todo: consider the caching concept for the BAT: i.e. the BAT is only stored losely and can be evicted from memory (by the OS) if neccessary
            var batRaw = file.Read(header2.tableOffset, header2.maxTableEntries * 4);
            long batOffset = 0;
            uint[] bat = batRaw.ReadUInt32Arr(ref batOffset, header2.maxTableEntries, Endianness.BigEndian);

            if (header.diskType == 4)
                issues.Add("Differencing disk images are not supported yet. Go ahead and implement it, it shouldn't be hard and the specs are out there. I'll treat this as a dynamic disk image for now (i.e. unmodified sectors are read as 0 instead of old data).");

            return new VHD(file, header.guid, header.currSize / 512, bat, header2.blockSize / 512);
        }



        /// <summary>
        /// Opens the specified file as a virtual hard disk and makes it available as a disk object.
        /// </summary>
        [AOSAction("mount", "ext=vhd")]
        public DynamicSet<IDisk> Mount(IFile file, Context context)
        {
            // todo: think about locking
            List<string> issues;
            var disk = ParseHeader(file, context.Log, out issues);

            if (issues.Count == 0)
                context.Shell.Notify(new Text() {
                    Summary = "The VHD image seems to be healthy."
                }, Severity.Success);

            if (issues.Count > 0) {
                var answer = context.Shell.PresentDialog(new Text() {
                    Summary = "There are problems with reading this image.",
                    Details = "I found some issues with the file you tried to mount. If you continue, there is no guarantee that it will work.",
                    Debug = string.Join("\n", issues)
                }, new Option[] { new Option() {
                    Text = new Text() {
                        Summary = "Cancel",
                        Details = "Don't open the image file"
                    },
                    Level = Level.Recommended
                }, new Option() {
                    Text = new Text() {
                        Summary = "Open anyway",
                        Details = "I don't care, try it anyway"
                    },
                    Level = Level.Advanced
                } });
                if (answer == 0)
                    return new DynamicSet<IDisk>();
            }

            return new DynamicSet<IDisk>(disk.DiskRef).Retain();
            //ObjectStore.PublishObject(disk, new Dictionary<string, string>());
            //ObjectStore.Action("init", disk, shell, info);
        }

        /// <summary>
        /// Parses the header of a VHD image and returns information about it.
        /// </summary>
        [AOSAction("info", "ext=vhd")]
        public void Info(IFile file, Context context)
        {
            List<string> issues;
            var disk = ParseHeader(file, context.Log, out issues);

            if (issues.Count == 0)
                context.Shell.Notify(new Text() {
                    Summary = "The VHD image seems to be healthy."
                }, Severity.Success);

            if (issues.Count > 1)
                context.Log.Log("Multiple issues were found with the VHD image:", LogType.Warning);

            foreach (var issue in issues)
                context.Log.Log(issue, LogType.Warning);
        }
    }
}
