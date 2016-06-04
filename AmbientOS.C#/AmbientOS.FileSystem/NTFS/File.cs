using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem.NTFS
{
    [EnumType("System.Int16")]
    enum FileRecordFlags
    {
        /// <summary>
        /// The file record is in use.
        /// </summary>
        InUse = 0x0001,

        /// <summary>
        /// The file record represents a directory.
        /// </summary>
        IsDirectory = 0x0002,

        /// <summary>
        /// Was only observed on $Quota, $ObjId and ??
        /// </summary>
        Unknown = 0x0004,

        /// <summary>
        /// Was only observed on $Secure, $Quota, $ObjId and ??.
        /// These files have in common that they are neither have the $I30 index nor an unnamed data stream.
        /// </summary>
        IsSpecialFile = 0x0008
    }

    enum FileFlags
    {
        ReadOnly = 0x0001,
        Hidden = 0x0002,
        System = 0x0004,
        Archive = 0x0020,
        Device = 0x0040,
        Normal = 0x0080,
        Temporary = 0x0100,
        SparseFile = 0x0200,
        ReparsePoint = 0x0400,
        Compressed = 0x0800,
        Offline = 0x1000,
        NotContentIndexed = 0x2000,
        Encrypted = 0x4000,
    }

    /// <summary>
    /// This header is used in any vital NTFS structures.
    /// It allows for detection of incomplete multi-sector writes.
    /// size: 16B
    /// </summary>
    [Endianness(Endianness.LittleEndian)]
    class FixupHeader
    {
        public UInt32 magicNumber;
        public UInt16 updateSequenceOffset;
        public UInt16 updateSequenceLength;
        public UInt64 logFileSequenceNumber;
    }

    /// <summary>
    /// A file record, as found in the MFT
    /// </summary>
    class FileRecord
    {
        public FixupHeader header;
        public UInt16 sequenceNumber;
        public UInt16 referenceCount;
        public UInt16 attributeSequenceOffset;
        public FileRecordFlags flags;
        public UInt32 realSize;
        public UInt32 allocatedSize;
        public UInt64 baseRecordSegment;
        public UInt16 maxAttributeType;

        /// <summary>
        /// The offset of this file record within the first cluster that contains it.
        /// </summary>
        [FieldSpecs(Ignore = true)]
        long clusterOffset;

        /// <summary>
        /// The clusters from the MFT that contain this file record
        /// </summary>
        [FieldSpecs(Ignore = true)]
        readonly Cluster[] clusters;

        [FieldSpecs(Ignore = true)]
        public readonly List<NTFSAttribute> attributes;

        public bool IsFile { get { return !flags.HasFlag(FileRecordFlags.IsDirectory); } }

        public NTFS Volume { get; }

        /// <summary>
        /// The index in the MFT. Together with the sequence number this identifies the file on a volume.
        /// </summary>
        public long MFTIndex { get { return (clusters.First().VCN * Volume.bytesPerCluster + clusterOffset) / Volume.bytesPerMFTRecord; } }

        /// <summary>
        /// The sequence number of this file.
        /// This is incremented whenever the file record updated.
        /// </summary>
        public short SequenceNumber { get; }

        /// <summary>
        /// The MFT reference of this file, consisting of MFTIndex and SequenceNumber.
        /// </summary>
        public long FileReference { get { return ((long)SequenceNumber << 48) + MFTIndex; } }

        public FileRecord(NTFS volume, Cluster[] clusters, long offset)
        {
            Volume = volume;

            // read clusters into a consecutive buffer on which we can easily operate
            byte[] buffer;
            if (clusters.Count() != 1) {
                buffer = new byte[clusters.Count() * volume.bytesPerCluster];
                for (long i = 0; i < clusters.Count(); i++)
                    Array.Copy(clusters[i].data, 0, buffer, i * volume.bytesPerCluster, volume.bytesPerCluster);
            } else {
                buffer = clusters[0].data;
            }
            volume.ReadFixup(buffer, offset, volume.bytesPerMFTRecord, 0x454C4946); // todo: sometimes the actual cluster data is now fixed up, sometimes not


            // read file record header
            clusterOffset = offset;
            this.clusters = clusters;
            buffer.ReadObject(ref offset, this, Endianness.LittleEndian);

            SequenceNumber = Cluster.ReadBytes(clusters, header.updateSequenceOffset, 2).ReadInt16(0, Endianness.LittleEndian);

            // read all attributes
            attributes = new List<NTFSAttribute>(4);

            offset = attributeSequenceOffset;
            while (buffer.ReadUInt32(clusterOffset + offset, Endianness.LittleEndian) != 0xFFFFFFFF) {
                var attr = NTFSAttribute.FromBuffer(this, buffer, clusters, clusterOffset, offset);

                if (attr.length <= 0) // avoid endless loop
                    throw new Exception("encountered illegal attribute (length 0)");

                offset += attr.length;
                attributes.Add(attr);
            };
        }

        public static FileRecord FromClusters(NTFS volume, Cluster[] clusters, long offset)
        {
            // todo: why isn't the constructor used directly? may be a relic
            return new FileRecord(volume, clusters, offset);
        }

        /// <summary>
        /// Returns the specified attribute of the file.
        /// </summary>
        /// <param name="name">null to accept any attribute name</param>
        public IEnumerable<NTFSAttribute> GetAttributes(NTFSAttributeType type, string name)
        {
            return attributes.Where(a => a.type == type && (name == null ? true : a.name == name));
        }

        /// <summary>
        /// Reads the values of all attributes of the file that match the type and name.
        /// </summary>
        /// <param name="name">null to accept any attribute name</param>
        public IEnumerable<byte[]> ReadAttributes(NTFSAttributeType type, string name)
        {
            return GetAttributes(type, name).Select(attr => {
                var result = new byte[attr.GetSize()];
                attr.Read(0, result.Length, result, 0);
                return result;
            });
        }

        /// <summary>
        /// Reads the value of the first attribute of the file that matches the type and name.
        /// </summary>
        /// <param name="name">null to accept any attribute name</param>
        public byte[] ReadAttribute(NTFSAttributeType type, string name)
        {
            return ReadAttributes(type, name).First();
        }
    }

    abstract class NTFSFileSystemObject : IFileSystemObjectImpl
    {
        public NTFSFile AsFile { get; }
        public NTFSFolder AsFolder { get; }

        NTFSFileSystemObject parent;

        /// <summary>
        /// The MFT file record that represents this file.
        /// </summary>
        public FileRecord FileRecord { get; }

        /// <summary>
        /// The volume that contains this file.
        /// </summary>
        public NTFS Volume { get; }

        /// <summary>
        /// If this is a file, the data attribute is preloaded (not the actual value - except if it's resident)
        /// </summary>
        //public NTFSAttribute Data { get; }

        /// <summary>
        /// If this is a folder, the index tree "$I30" is preloaded (this is the index on the names of the contained files)
        /// </summary>
        //public IndexTree I30 { get; }

        /// <summary>
        /// Contains the name and some standard information about the file (e.g. times, sizes).
        /// </summary>
        public StandardInformationAttribute StandardInformation { get; }

        /// <summary>
        /// Contains the name and some additional information about the file.
        /// This is only updated on disk when the file is renamed.
        /// </summary>
        public FileNameAttribute FileName { get; }


        /// <summary>
        /// Creates a new file object from an NTFS MFT file record
        /// </summary>
        public static NTFSFileSystemObject FromBuffer(NTFSFileSystemObject parent, NTFS volume, Cluster[] clusters, long offset)
        {
            var fileRecord = FileRecord.FromClusters(volume, clusters, offset);

            if (fileRecord.flags.HasFlag(FileRecordFlags.IsSpecialFile))
                throw new NotImplementedException(); // we can't preload attributes on special files, since they don't have them

            if (fileRecord.IsFile) {
                return new NTFSFile(fileRecord, parent);
            } else {
                return new NTFSFolder(fileRecord, parent);
            }
        }

        /// <summary>
        /// Creates a new file object from an NTFS MFT file record
        /// </summary>
        public NTFSFileSystemObject(FileRecord fileRecord, NTFSFileSystemObject parent)
        {
            Volume = fileRecord.Volume;
            this.parent = parent;
            FileRecord = fileRecord;

            if (!FileRecord.flags.HasFlag(FileRecordFlags.InUse))
                throw new Exception("The file record is not in use");

            StandardInformation = FileRecord.ReadAttribute(NTFSAttributeType.StandardInformation, "").ReadObject<StandardInformationAttribute>(0);
            FileName = FileRecord.ReadAttribute(NTFSAttributeType.FileName, "").ReadObject<FileNameAttribute>(0);


            Name = new LambdaValue<string>(() => FileName.fileName, val => FileName.fileName = val);
            Path = new LambdaValue<string>(() => (parent != null ? parent.Path.Get() : "") + "/" + Name.Get().EscapeForURL());

            Times = new LambdaValue<FileTimes>(
                () => new FileTimes() {
                    CreatedTime = StandardInformation.creationTime,
                    ReadTime = StandardInformation.readTime,
                    ModifiedTime = StandardInformation.alteredTime
                },
                val => {
                    if (val.CreatedTime.HasValue)
                        StandardInformation.creationTime = val.CreatedTime.Value;
                    if (val.ReadTime.HasValue)
                        StandardInformation.readTime = val.ReadTime.Value;
                    if (val.ModifiedTime.HasValue)
                        StandardInformation.alteredTime = val.ModifiedTime.Value;
                });
        }

        /// <summary>
        /// Writes the StandardInformation attribute to disk.
        /// </summary>
        private void UpdateStandardInformation()
        {
            var attr = FileRecord.GetAttributes(NTFSAttributeType.StandardInformation, "").First();
            byte[] buffer = new byte[attr.GetSize()];
            buffer.WriteVal(0, StandardInformation);
            attr.Write(0, buffer.Length, buffer, 0);
        }

        /// <summary>
        /// Writes the FileName attribute to disk.
        /// </summary>
        private void UpdateFileName()
        {
            var requiredSize = 0x42 + FileName.fileName.Length;
            var attr = FileRecord.GetAttributes(NTFSAttributeType.FileName, "").First();

            if (requiredSize != attr.GetSize())
                attr.ChangeSize(requiredSize);

            byte[] buffer = new byte[requiredSize];
            buffer.WriteVal(0, FileName);
            attr.Write(0, buffer.Length, buffer, 0);
        }


        #region IFileSystemObject

        public IFileSystem GetFileSystem()
        {
            return Volume.FileSystemRef.Retain();
        }

        public DynamicValue<string> Name { get; }
        public DynamicValue<string> Path { get; }
        public DynamicValue<FileTimes> Times { get; }

        /// <summary>
        /// Shall return all tree contained by this file.
        /// For normal folders, this is just the $I30 tree.
        /// </summary>
        protected abstract IndexTree[] GetTrees();

        /// <summary>
        /// Returns the total size of the file or folder (recursive) on disk. This includes the full allocated size including the file system structures that make up this file or folder.
        /// For folders the size is determined recursively.
        /// When querying the size-on-disk of the root folder of a volume, the result should be very close to the occupied disk space.
        /// Returns null if the size cannot be determined.
        /// </summary>
        public long? GetSizeOnDisk()
        {
            long? size = FileRecord.GetAttributes(NTFSAttributeType.DontCare, null).Select(attr => attr.GetAllocatedSize()).Sum();
            size += GetTrees()
                .SelectMany(tree => tree.GetAllValues())
                .Select(file => Volume.MFT.GetFile(file.Value, this))
                .Select(file => file.GetSizeOnDisk()).Sum();
            return size;
        }

        public void Delete(DeleteMode mode)
        {
            throw new NotImplementedException();
        }

        public void SecureDelete(int passes)
        {
            throw new NotImplementedException();
        }

        #endregion
    }


    class NTFSFile : NTFSFileSystemObject, IFileImpl
    {
        public DynamicValue<string> Type { get; }
        public DynamicValue<long?> Length { get; }

        internal NTFSAttribute Data { get; }

        public NTFSFile(FileRecord fileRecord, NTFSFileSystemObject parent)
            : base(fileRecord, parent)
        {
            Data = fileRecord.GetAttributes(NTFSAttributeType.Data, "").First();

            Type = this.GetStreamTypeFromFileName();

            Length = new LambdaValue<long?>(() => Data.GetSize(), val => {
                if (val == null)
                    throw new ArgumentNullException($"{val}");
                Data.ChangeSize(val.Value);
            });
        }

        protected override IndexTree[] GetTrees()
        {
            return new IndexTree[0];
        }

        public void Read(long offset, long count, byte[] buffer, long bufferOffset)
        {
            Data.Read(offset, count, buffer, bufferOffset);
        }

        public void Write(long offset, long count, byte[] buffer, long bufferOffset)
        {
            Data.Write(offset, count, buffer, bufferOffset);
        }

        public void Flush()
        {
            Data.Flush();
        }
    }

    class NTFSFolder : NTFSFileSystemObject, IFolderImpl
    {
        readonly IndexTree I30;

        public NTFSFolder(FileRecord fileRecord, NTFSFileSystemObject parent)
            : base(fileRecord, parent)
        {
            I30 = new IndexTree(this, "$I30");
        }

        protected override IndexTree[] GetTrees()
        {
            return new IndexTree[] { I30 };
        }

        public long? GetContentSize()
        {
            long? size = 0;

            foreach (var child in GetChildren()) {
                var file = child as NTFSFile;
                if (file != null)
                    size += file.Length.Get();

                var folder = child as NTFSFolder;
                if (folder != null)
                    size += folder.GetContentSize();
            }

            return size;
        }

        public IEnumerable<NTFSFileSystemObject> GetChildren()
        {
            return I30.GetAllValues().Select(f => Volume.MFT.GetFile(f.Value, this));
        }

        IEnumerable<IFileSystemObject> IFolderImpl.GetChildren()
        {
            return GetChildren().Select(obj => obj.AsReference<IFileSystemObject>());
        }

        public NTFSFileSystemObject GetChild(string name, bool file, OpenMode mode)
        {
            var files = I30.GetValues(name).Select(f => Volume.MFT.GetFile(f, this)).Where(f => f.FileRecord.IsFile == file).ToArray();

            if (files.Any()) {
                if (!mode.HasFlag(OpenMode.Existing))
                    throw new System.IO.FileLoadException("the " + (file ? "file" : "folder") + " already exists", name);

                if (files.Count() > 1) { // if the same name occurs multiple times, select the one with a name in Win32 namespace
                    var win32files = files.Where(val => val.FileRecord.ReadAttributes(NTFSAttributeType.FileName, "")
                        .Select(attr => attr.ReadObject<FileNameAttribute>(0))
                        .Any(attr => (attr.nameSpace & 1) != 0)
                    ).ToArray();
                    if (win32files.Any())
                        files = win32files;
                }

                return files.First();
            } else {
                if (!mode.HasFlag(OpenMode.New))
                    throw new System.IO.FileNotFoundException("the " + (file ? "file" : "folder") + " does not exist", name);

                throw new NotImplementedException("can't create file yet");
            }
        }

        IFileSystemObject IFolderImpl.GetChild(string name, bool file, OpenMode mode)
        {
            return GetChild(name, file, mode).AsReference<IFileSystemObject>();
        }

        public bool ChildExists(string name, bool file)
        {
            return I30.GetValues(name).Select(f => Volume.MFT.GetFile(f, this)).Where(f => f.FileRecord.IsFile == file).Any();
        }
    }
}
