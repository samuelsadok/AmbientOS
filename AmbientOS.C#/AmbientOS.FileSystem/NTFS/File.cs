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
        public long clusterOffset;

        /// <summary>
        /// The clusters from the MFT that contain this file record
        /// </summary>
        [FieldSpecs(Ignore = true)]
        public Cluster[] clusters;

        [FieldSpecs(Ignore = true)]
        public List<NTFSAttribute> attributes;

        public bool IsFile { get { return !flags.HasFlag(FileRecordFlags.IsDirectory); } }

        public static FileRecord FromClusters(NTFSFile file, Cluster[] clusters, long offset)
        {
            // read clusters into a consecutive buffer on which we can easily operate
            byte[] buffer;
            if (clusters.Count() != 1) {
                buffer = new byte[clusters.Count() * file.Volume.bytesPerCluster];
                for (long i = 0; i < clusters.Count(); i++)
                    Array.Copy(clusters[i].data, 0, buffer, i * file.Volume.bytesPerCluster, file.Volume.bytesPerCluster);
            } else {
                buffer = clusters[0].data;
            }
            file.Volume.ReadFixup(buffer, offset, file.Volume.bytesPerMFTRecord, 0x454C4946); // todo: sometimes the actual cluster data is now fixed up, sometimes not

            // read file record header
            var result = new FileRecord();
            result.clusterOffset = offset;
            result.clusters = clusters;
            buffer.ReadObject(ref offset, result, Endianness.LittleEndian);

            // read all attributes
            result.attributes = new List<NTFSAttribute>(4);

            offset = result.attributeSequenceOffset;
            while (buffer.ReadUInt32(result.clusterOffset + offset, Endianness.LittleEndian) != 0xFFFFFFFF) {
                var attr = NTFSAttribute.FromBuffer(file, buffer, clusters, result.clusterOffset, offset);

                if (attr.length <= 0) // avoid endless loop
                    throw new Exception("encountered illegal attribute (length 0)");

                offset += attr.length;
                result.attributes.Add(attr);
            };

            return result;
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




    class NTFSFile : IFileImpl, IFolderImpl
    {
        public IFileSystemObject FileSystemObjectRef { get; }
        public IFile FileRef { get; }
        public IFolder FolderRef { get; }

        NTFSFile parent;

        /// <summary>
        /// The MFT file record that represents this file.
        /// </summary>
        public FileRecord FileRecord { get; }

        /// <summary>
        /// The index in the MFT. Together with the sequence number this identifies the file on a volume.
        /// </summary>
        public long MFTIndex { get; }

        /// <summary>
        /// The sequence number of this file.
        /// This is incremented whenever the file record updated.
        /// </summary>
        public short SequenceNumber { get; }

        /// <summary>
        /// The MFT reference of this file, consisting of MFTIndex and SequenceNumber.
        /// </summary>
        public long FileReference { get { return ((long)SequenceNumber << 48) + MFTIndex; } }

        /// <summary>
        /// The volume that contains this file.
        /// </summary>
        public NTFSVolume Volume { get; }

        /// <summary>
        /// If this is a file, the data attribute is preloaded (not the actual value - except if it's resident)
        /// </summary>
        public NTFSAttribute Data { get; }

        /// <summary>
        /// If this is a folder, the index tree "$I30" is preloaded (this is the index on the names of the contained files)
        /// </summary>
        public IndexTree I30 { get; }

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
        public NTFSFile(NTFSFile parent, NTFSVolume volume, Cluster[] clusters, long offset)
        {
            FileSystemObjectRef = new FileSystemObjectRef(this);

            //try { // todo: remove try-catch
            Volume = volume;
            this.parent = parent;
            FileRecord = FileRecord.FromClusters(this, clusters, offset);

            MFTIndex = (clusters.First().VCN * Volume.bytesPerCluster + offset) / Volume.bytesPerMFTRecord;
            SequenceNumber = Cluster.ReadBytes(clusters, FileRecord.header.updateSequenceOffset, 2).ReadInt16(0, Endianness.LittleEndian);

            if (!FileRecord.flags.HasFlag(FileRecordFlags.InUse))
                throw new Exception("The file record is not in use");

            StandardInformation = FileRecord.ReadAttribute(NTFSAttributeType.StandardInformation, "").ReadObject<StandardInformationAttribute>(0);
            FileName = FileRecord.ReadAttribute(NTFSAttributeType.FileName, "").ReadObject<FileNameAttribute>(0);

            if (!FileRecord.flags.HasFlag(FileRecordFlags.IsSpecialFile)) { // don't preload attributes on special files, since they don't have them
                if (FileRecord.IsFile) {
                    Data = FileRecord.GetAttributes(NTFSAttributeType.Data, "").First();
                    FileRef = new FileRef(this);
                } else {
                    I30 = new IndexTree(this, "$I30");
                    FolderRef = new FolderRef(this);
                }
            }
            //} catch (Exception ex) {
            //    Exception exc;
            //    try {
            //        exc = new Exception(string.Format("flags: {0:X4}, path: {1}, ex: {2}", FileRecord.flags.ToInt(), this.GetInfo().Path, ex.Message));
            //    } catch {
            //        exc = new Exception("total failure");
            //    }
            //    throw exc;
            //}

            Name = new DynamicEndpoint<string>(() => FileName.fileName, val => FileName.fileName = val);
            Path = new DynamicEndpoint<string>(() => (parent != null ? parent.Path.Get() : "") + "/" + Name.Get().EscapeForURL());

            Times = new DynamicEndpoint<FileTimes>(
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

            Size = new DynamicEndpoint<long?>(
                () => {
                    long? size = 0;

                    if (Data != null)
                        size += Data.GetSize();

                    if (I30 != null)
                        size += I30.GetAllValues()
                            .Select(file => Volume.MFT.GetFile(file.Value, this))
                            .Select(file => file.Size.Get()).Sum();

                    return size;
                },
                val => {
                    if (Data == null)
                        throw new InvalidOperationException();
                    if (val == null)
                        throw new ArgumentNullException($"{val}");
                    Data.ChangeSize(val.Value);
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


        #region "common file/folder methods"

        public IFileSystem GetFileSystem()
        {
            return Volume.FileSystemRef.Retain();
        }

        public DynamicEndpoint<string> Name { get; }
        public DynamicEndpoint<string> Path { get; }
        public DynamicEndpoint<FileTimes> Times { get; }
        public DynamicEndpoint<long?> Size { get; }

        /// <summary>
        /// Returns the total size of the file or folder (recursive) on disk. This includes the full allocated size including the file system structures that make up this file or folder.
        /// For folders the size is determined recursively.
        /// When querying the size-on-disk of the root folder of a volume, the result should be very close to the occupied disk space.
        /// Returns null if the size cannot be determined.
        /// </summary>
        public long? GetSizeOnDisk()
        {
            long? size = FileRecord.GetAttributes(NTFSAttributeType.DontCare, null).Select(attr => attr.GetAllocatedSize()).Sum();

            if (I30 != null)
                size += I30.GetAllValues()
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

        public void Flush()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region "folder specific methods"

        public IEnumerable<NTFSFile> GetChildren()
        {
            if (I30 == null)
                throw new InvalidOperationException();

            return I30.GetAllValues().Select(f => Volume.MFT.GetFile(f.Value, this));
        }

        IEnumerable<IFileSystemObject> IFolderImpl.GetChildren()
        {
            return GetChildren().Select(obj => obj.FileSystemObjectRef.Retain());
        }

        public NTFSFile GetChild(string name, bool file, OpenMode mode)
        {
            if (I30 == null)
                throw new InvalidOperationException();

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
            return GetChild(name, file, mode).FileSystemObjectRef.Retain();
        }

        public bool ChildExists(string name, bool file)
        {
            if (I30 == null)
                throw new InvalidOperationException();

            return I30.GetValues(name).Select(f => Volume.MFT.GetFile(f, this)).Where(f => f.FileRecord.IsFile == file).Any();
        }

        #endregion

        #region "file specific methods"

        public void Read(long offset, long count, byte[] buffer, long bufferOffset)
        {
            if (Data == null)
                throw new InvalidOperationException();
            Data.Read(offset, count, buffer, bufferOffset);
        }

        public void Write(long offset, long count, byte[] buffer, long bufferOffset)
        {
            if (Data == null)
                throw new InvalidOperationException();
            Data.Write(offset, count, buffer, bufferOffset);
        }

        public void AddCustomAppearance(Dictionary<string, string> dict, Type type)
        {
            if (type == typeof(IFile))
                ((IFile)this).AddCustomAppearance(dict);
        }

        #endregion
    }
}
