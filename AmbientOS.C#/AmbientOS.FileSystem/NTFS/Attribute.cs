using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem.NTFS
{
    enum NTFSAttributeType
    {
        DontCare = 0, // this is not related to NTFS, we just use it in the implementation
        StandardInformation = 0x10,
        AttributeList = 0x20,
        FileName = 0x30,
        ObjectID = 0x40,
        SecurityDescriptor = 0x50,
        VolumeName = 0x60,
        VolumeInformation = 0x70,
        Data = 0x80,
        IndexRoot = 0x90,
        IndexAllocation = 0xA0,
        Bitmap = 0xB0,
        ReparsePoint = 0xC0,
        EAInformation = 0xD0,
        EA = 0xE0,
        LoggedUtilityStream = 0x100,
        EndOfList = unchecked((int)0xFFFFFFFF)
    }

    /// <summary>
    /// Every (?) file has this attribute
    /// </summary>
    [Endianness(Endianness.LittleEndian)]
    class StandardInformationAttribute
    {
        [FieldSpecs(DateFormat = DateFormat.NTFS)]
        public DateTime creationTime;
        [FieldSpecs(DateFormat = DateFormat.NTFS)]
        public DateTime alteredTime;
        [FieldSpecs(DateFormat = DateFormat.NTFS)]
        public DateTime mftChangedTime;
        [FieldSpecs(DateFormat = DateFormat.NTFS)]
        public DateTime readTime;

        public FileFlags Flags;
        public UInt32 MaxVersions;
        public UInt32 VersionNumber;
        public UInt32 ClassID;

        /// <summary>
        /// A key into the $O and $Q indexes of $Quota. 0 if quotas are disabled.
        /// </summary>
        [FieldSpecs(Optional = true)]
        public UInt32 OwnerID;

        /// <summary>
        /// NOT the same as security identifier.
        /// This one is a key into the $SII and $SDS attributes in the $Secure file.
        /// </summary>
        [FieldSpecs(Optional = true)]
        public UInt32 SecurityID;

        /// <summary>
        /// Sum of the (allocated?) length of all streams. 0 if quotas are disabled.
        /// </summary>
        [FieldSpecs(Optional = true)]
        public Int64 QuotaCharged;

        /// <summary>
        /// Related to the $UsnJrnl file
        /// </summary>
        [FieldSpecs(Optional = true)]
        public Int64 UpdateSequenceNumber;
    }

    /// <summary>
    /// Information in this attribute actually becomes outdated until the filename is changed.
    /// This attribute (the version in the filerecord) is only written to disk when the filename changes.
    /// The version in the index however is always updated immediately (i.e. on time/size change).
    /// </summary>
    [Endianness(Endianness.LittleEndian)]
    class FileNameAttribute
    {
        UInt64 parentReference;

        [FieldSpecs(DateFormat = DateFormat.NTFS)]
        public DateTime creationTime;
        [FieldSpecs(DateFormat = DateFormat.NTFS)]
        public DateTime writeTime;
        [FieldSpecs(DateFormat = DateFormat.NTFS)]
        public DateTime mftEditTime;
        [FieldSpecs(DateFormat = DateFormat.NTFS)]
        public DateTime readTime;

        public UInt64 allocatedSize;
        public UInt64 realSize;
        public UInt32 flags;
        public UInt32 reparseInfo;

        [FieldSpecs(LengthOf = "fileName")]
        byte fileNameLength;
        public byte nameSpace;
        [FieldSpecs(StringFormat = StringFormat.Unicode)]
        public string fileName;
    }


    /// <summary>
    /// An attribute in a file record
    /// </summary>
    class NTFSAttribute
    {
        /// <summary>
        /// The offset of this attribute within the file record.
        /// </summary>
        public long FileRecordOffset { get; }

        /// <summary>
        /// The volume that contains this attribute
        /// </summary>
        public NTFSFile File { get; }

        /// <summary>
        /// The NTFS logfile client of the volume that contains this attribute.
        /// </summary>
        public NTFSLogFile.Client Log
        {
            get
            {
                if (log == null)
                    log = File.Volume.LogFile.NTFSClient;
                return log;
            }
        }
        [FieldSpecs(Ignore = true)]
        private NTFSLogFile.Client log = null;

        /// <summary>
        /// Indicates if write access to this attribute should be logged.
        /// By default, this is only false if the attribute is an unnamed data stream, but it can be overridden after construction.
        /// This has no effect on resident attributes.
        /// </summary>
        //public bool Logged { get; set; }

        /// <summary>
        /// The size of a block in this attribute, if it contains blocks that are protected by the fixup-header.
        /// By default, for index allocations this is the index buffer size and for anything else it's 0.
        /// For the MFT data stream, this should be set to the file record size.
        /// </summary>
        public int SectorsPerBlock { get; set; }

        /// <summary>
        /// The ID of this attribute in the attribute table.
        /// Note: this approach does not support multiple clients (not the case in normal NTFS anyway).
        /// </summary>
        public int LogFileAttributeID { get; set; } = 0;

        public NTFSAttributeType type;

        /// <summary>
        /// This is NOT the actual attribute length, only the length of its representation in the file record
        /// </summary>
        public UInt32 length;

        public byte nonResident;

        public bool Resident { get { return nonResident == 0; } }

        public byte nameLength;
        public UInt16 nameOffset;
        public UInt16 compressed;
        public UInt16 id;

        [Endianness(Endianness.LittleEndian)]
        public class ResidentHeader
        {
            public UInt32 length;
            public UInt16 offset;
            public UInt16 indexed;

            /// <summary>
            /// The content of this resident attribute.
            /// This is always valid.
            /// </summary>
            [FieldSpecs(Ignore = true)]
            public byte[] buffer;

            /// <summary>
            /// The clusters that contain this attribute.
            /// This will clusters (ordered by VCN) that belong to the MFT data stream.
            /// In all but corner cases, this will only be one cluster.
            /// </summary>
            [FieldSpecs(Ignore = true)]
            public Cluster[] clusters;
        };
        [FieldSpecs(Ignore = true)]
        public ResidentHeader residentHeader;

        [Endianness(Endianness.LittleEndian)]
        public class NonResidentHeader
        {
            /// <summary>
            /// The VCN (in the runlist) where the data starts. This is 0 most of the time (why wouldn't it be?).
            /// </summary>
            public Int64 startClusterVCN;

            /// <summary>
            /// The last VCN (in the runlist) of the data. This is the number of allocated clusters minus 1 (why wouldn't it be?).
            /// </summary>
            public Int64 lastClusterVCN;

            public UInt16 runlistOffset;
            public UInt16 compressionEngine;
            public UInt32 reserved;
            public Int64 allocatedSize;
            public Int64 realSize;
            public Int64 initializedSize;

            [FieldSpecs(Ignore = true)]
            public List<Tuple<long, long>> runlist; // holds the (VCN, length) tuple for each datarun

            [FieldSpecs(Ignore = true)]
            public Dictionary<long, Cluster> clusters = new Dictionary<long, Cluster>();

            /// <summary>
            /// Reads a runlist from a byte array.
            /// The returned tuples hold the VCN and cluster count of each datarun.
            /// </summary>
            public static List<Tuple<long, long>> ReadRunlist(byte[] runlist)
            {
                var runlistOffset = 0;
                long currentLCN = 0;
                var result = new List<Tuple<long, long>>(2);

                while (runlist[runlistOffset] != 0) {
                    var offsetBytes = (runlist[runlistOffset] >> 4) & 0xF;
                    var countBytes = runlist[runlistOffset] & 0xF;
                    runlistOffset++;

                    // load unsigned length field
                    long clusterCount = 0;
                    for (int i = 0; i < countBytes; i++)
                        clusterCount |= ((long)runlist[runlistOffset++] << (8 * i));

                    // load signed offset field
                    long clusterOffset = 0;
                    for (int i = 0; i < offsetBytes; i++)
                        clusterOffset |= ((long)runlist[runlistOffset++] << (8 * i));
                    int offsetShift = 64 - 8 * offsetBytes;
                    currentLCN += ((clusterOffset << offsetShift) >> offsetShift);

                    result.Add(new Tuple<long, long>(currentLCN, clusterCount));
                }

                return result;
            }

            /// <summary>
            /// Writes a runlist into a byte array.
            /// </summary>
            public static byte[] WriteRunlist(IEnumerable<Tuple<long, long>> runlist)
            {
                long currentLCN = 0;
                long runlistOffset = 0;

                var result = new byte[8];

                foreach (var datarun in runlist) {
                    // prepare cluster count
                    var number1 = new byte[8];
                    number1.WriteVal(0, datarun.Item2, Endianness.LittleEndian);
                    var length1 = GetCompressedLength(number1, false);

                    // prepare LCN
                    var number2 = new byte[8];
                    number2.WriteVal(0, datarun.Item1 - currentLCN, Endianness.LittleEndian);
                    var length2 = GetCompressedLength(number2, true);

                    // resize output if neccessary
                    var newOffset = 1 + length1 + length2;
                    while (newOffset > result.Length)
                        Array.Resize(ref result, result.Length + 8);

                    // write to output
                    result[runlistOffset] = (byte)(length1 + length2 << 4);
                    Array.Copy(number1, 0, result, runlistOffset + 1, length1);
                    Array.Copy(number2, 0, result, runlistOffset + 1 + length1, length2);

                    currentLCN = datarun.Item1;
                    runlistOffset = newOffset;
                }

                return result;
            }

            private static int GetCompressedLength(byte[] number, bool signed)
            {
                for (int length = 8; length > 1; length--)
                    if (number[length - 1] != ((signed ? (number[length - 2] & 0x80) != 0 : false) ? 0xFF : 0x00))
                        return length;
                return 1;
            }
        }

        [FieldSpecs(Ignore = true)]
        public NonResidentHeader nonResidentHeader;

        [FieldSpecs(Ignore = true)]
        public string name;

        private NTFSAttribute(NTFSFile file, long fileRecordOffset)
        {
            File = file;
            FileRecordOffset = fileRecordOffset;
        }

        /// <summary>
        /// Reads an attribute from a buffer at the specified position
        /// </summary>
        /// <param name="clusterOffset">the offset of the file record within the cluster</param>
        /// <param name="recordOffset">the offset of the attribute within the file record</param>
        public static NTFSAttribute FromBuffer(NTFSFile file, byte[] buffer, Cluster[] clusters, long clusterOffset, long recordOffset)
        {
            var result = new NTFSAttribute(file, recordOffset);
            var actualOffset = (clusterOffset += recordOffset);
            buffer.ReadObject(ref clusterOffset, result, Endianness.LittleEndian);
            result.name = Encoding.Unicode.GetString(buffer, (int)(actualOffset + result.nameOffset), result.nameLength * 2);

            result.SectorsPerBlock = result.type == NTFSAttributeType.IndexAllocation ? (int)(file.Volume.bytesPerIndexRecord / file.Volume.bytesPerSector) : 0;

            if (result.Resident) {
                result.residentHeader = buffer.ReadObject<ResidentHeader>(clusterOffset);
                result.residentHeader.buffer = new byte[result.residentHeader.length];
                Array.Copy(buffer, actualOffset + result.residentHeader.offset, result.residentHeader.buffer, 0, result.residentHeader.buffer.Length);
                result.residentHeader.clusters = clusters;
            } else {
                result.nonResidentHeader = buffer.ReadObject<NonResidentHeader>(clusterOffset);
                var runlist = new byte[actualOffset + result.length - clusterOffset];
                Array.Copy(buffer, actualOffset + result.nonResidentHeader.runlistOffset, runlist, 0, runlist.Length);
                result.nonResidentHeader.runlist = NonResidentHeader.ReadRunlist(runlist);
            }

            return result;
        }


        /// <summary>
        /// Returns the cluster with the specified VCN.
        /// The cluster is loaded from disk if neccessary, using the specified LCN.
        /// This method is only valid for non-resident attributes.
        /// This operation is thread-safe.
        /// </summary>
        /// <param name="load">If the entire cluster will be overwritten, this can be set to false to skip reading from disk.</param>
        public Cluster GetCluster(long vcn, long lcn, bool load)
        {
            Cluster result;
            lock (nonResidentHeader.clusters) {
                if (!nonResidentHeader.clusters.TryGetValue(vcn, out result))
                    nonResidentHeader.clusters[vcn] = result = new Cluster() {
                        VCN = vcn,
                        LCN = lcn,
                        data = null,
                        dirty = false
                    };

                if (result.data != null || !load)
                    return result;
            }

            // Note: we give up the lock while reading to enable reading clusters concurrently.
            // The worst that can happen is that we read the same cluster twice.

            byte[] data = new byte[File.Volume.bytesPerCluster];
            File.Volume.rawVolume.Read(lcn * File.Volume.bytesPerCluster, File.Volume.bytesPerCluster, data, 0);

            lock (nonResidentHeader.clusters)
                result.data = data;
            return result;
        }

        /// <summary>
        /// Returns the cluster with the specified VCN.
        /// The cluster is loaded from disk if neccessary, using the dataruns of this attribute.
        /// This method is only valid for non-resident attributes.
        /// This operation is thread-safe.
        /// </summary>
        public Cluster GetCluster(long VCN, bool load)
        {
            Cluster result;
            lock (nonResidentHeader.clusters)
                if (nonResidentHeader.clusters.TryGetValue(VCN, out result))
                    if (result.data != null || !load)
                        return result;

            // walk the runlist until we find the correct LCN
            var offset = VCN;
            foreach (var datarun in nonResidentHeader.runlist) {
                if (offset < datarun.Item2)
                    return GetCluster(VCN, datarun.Item1 + offset, load);
                offset -= datarun.Item2;
            }

            throw new Exception("attempt to load cluster beyond non-resident attribute");
        }


        /// <summary>
        /// Reads from the attribute by copying the data from the cluster cache into the buffer.
        /// </summary>
        public void Read(long offset, long length, byte[] buffer, long bufferOffset)
        {
            if (Resident) {
                if (length > residentHeader.buffer.Length || offset > unchecked(residentHeader.buffer.Length - length))
                    throw new ArgumentOutOfRangeException("Attempt to read beyond the attribute");
                Array.Copy(residentHeader.buffer, offset, buffer, bufferOffset, length);

            } else {

                if (length > nonResidentHeader.realSize || offset > unchecked(nonResidentHeader.realSize - length))
                    throw new ArgumentOutOfRangeException("Attempt to read beyond the attribute");

                var vcn = offset / File.Volume.bytesPerCluster;
                var firstClusterOffset = offset % File.Volume.bytesPerCluster;

                // handle unaligned beginning
                if (firstClusterOffset != 0) {
                    var actualLength = Math.Min(File.Volume.bytesPerCluster - firstClusterOffset, length);
                    Array.Copy(GetCluster(vcn++, true).data, firstClusterOffset, buffer, bufferOffset, actualLength);
                    bufferOffset += (int)actualLength;
                    length -= actualLength;
                }

                var clusterCount = length / File.Volume.bytesPerCluster;
                var remainder = length % File.Volume.bytesPerCluster;

                while (clusterCount-- > 0) {
                    Array.Copy(GetCluster(vcn++, true).data, 0, buffer, bufferOffset, File.Volume.bytesPerCluster);
                    bufferOffset += File.Volume.bytesPerCluster;
                }

                if (remainder > 0)
                    Array.Copy(GetCluster(vcn, true).data, 0, buffer, bufferOffset, remainder);
            }
        }


        /// <summary>
        /// Writes to the attribute.
        /// </summary>
        /// <param name="logged">If true, the write is logged, even if the attribute is non-resident. Resident attributes are always logged.</param>
        public void Write(long offset, long length, byte[] buffer, long bufferOffset)
        {
            var val = new byte[length];
            Array.Copy(buffer, bufferOffset, val, 0, length);

            using (var t = Log.StartTransaction()) {
                if (Resident) {
                    new NTFSLogFile.UpdateValueOperation(t, val, true, false).Log(File.Volume.MFT.Data, File.MFTIndex, (ushort)FileRecordOffset, (ushort)offset);
                } else if (SectorsPerBlock != 0) {
                    throw new NotImplementedException();
                    //new NTFSLogFile.UpdateValueOperation(t, val, false, false).Log(this, offset, );
                } else {

                    var vcn = offset / File.Volume.bytesPerCluster;
                    var firstClusterOffset = offset % File.Volume.bytesPerCluster;

                    // handle unaligned beginning
                    if (firstClusterOffset != 0) {
                        var actualLength = Math.Min(File.Volume.bytesPerCluster - firstClusterOffset, length);
                        var cluster = GetCluster(vcn++, true);
                        cluster.dirty = true; // todo: add cluster to dirty list (same below)
                        Array.Copy(buffer, bufferOffset, cluster.data, firstClusterOffset, actualLength);
                        bufferOffset += (int)actualLength;
                        length -= actualLength;
                    }

                    var clusterCount = length / File.Volume.bytesPerCluster;
                    var remainder = length % File.Volume.bytesPerCluster;

                    while (clusterCount-- > 0) {
                        var data = new byte[File.Volume.bytesPerCluster];
                        Array.Copy(data, 0, buffer, bufferOffset, File.Volume.bytesPerCluster);
                        bufferOffset += File.Volume.bytesPerCluster;

                        var cluster = GetCluster(vcn++, false);
                        cluster.dirty = true;
                        cluster.data = data;
                    }

                    if (remainder > 0) {
                        var cluster = GetCluster(vcn, true);
                        cluster.dirty = true;
                        Array.Copy(cluster.data, 0, buffer, bufferOffset, remainder);
                    }
                }

                t.Commit();
            }
        }

        /// <summary>
        /// Returns the size in bytes of the attribute value.
        /// </summary>
        public long GetSize()
        {
            return Resident ? residentHeader.length : nonResidentHeader.realSize;
        }

        /// <summary>
        /// Returns the size of this attribute on disk.
        /// For resident attributes, this is always 0, since it's contained in the non-resident data stream of the MFT.
        /// </summary>
        public long GetAllocatedSize()
        {
            return Resident ? 0 : nonResidentHeader.allocatedSize;
        }

        public void ChangeSize(long newSize)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return string.Format("{0} {1} of {2}",
                (name == "" ? null : name) ?? "unnamed",
                Utilities.EnumToString(type),
                File.GetPath());
        }
    }
}

