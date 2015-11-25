using System;
using System.Collections.Generic;
using System.Linq;
using AmbientOS.Utils;
using AmbientOS.Environment;

namespace AmbientOS.FileSystem.NTFS
{
    class NTFSVolume : IFileSystemImpl
    {
        public IFileSystem FileSystemRef { get; }

        /// <summary>
        /// The NTFS volume boot record
        /// </summary>
        [Endianness(Endianness.LittleEndian)]
        class VBR
        {
            byte jmp;
            Int16 addr;
            [FieldSpecs(Length = 8, StringFormat = StringFormat.ASCII)]
            public string magicNumber;
            public Int16 bytesPerSector;
            public byte sectorsPerCluster;
            Int32 reserved1;
            Int16 reserved2;
            byte reserved3;
            public byte mediaType; // 0xF8 means hard disk
            Int16 reserved4;
            Int32 reserved5;
            Int64 reserved6;
            public Int32 whatever; // usually 0x00800080
            public Int64 volumeLength; // sectors in volume
            public Int64 mftLocation;
            public Int64 mftMirrLocation;
            public sbyte clustersPerMFTRecord;
            byte reserved7;
            Int16 reserved8;
            public sbyte clustersPerIndexRecord;
            byte reserved9;
            Int16 reserved10;
            public Int64 volumeSerialNumber;
        }


        /// <summary>
        /// Takes a data block that starts with a fixup header and applies the fixup sequence to the data.
        /// when implementing writing:
        ///  1. Add one to the Update Sequence Number(0x0000 must be skipped)
        ///  2. For each sector, copy the last two bytes into the Update Sequence Array
        ///  3. Write the new Update Sequence Number to the end of each sector
        ///  4. Write the record to disk
        /// </summary>
        internal void ReadFixup(byte[] buffer, long offset, long length, uint magicNumber)
        {
            var fixupHeader = buffer.ReadObject<FixupHeader>(offset);
            if (fixupHeader.magicNumber != magicNumber)
                throw new FormatException(string.Format("expected magic number 0x{0:X8}", magicNumber));

            if ((fixupHeader.updateSequenceLength - 1) * bytesPerSector != length)
                throw new FormatException(string.Format("Update sequence too long"));

            var updateSequenceOffset = offset + fixupHeader.updateSequenceOffset;
            var sequenceNumber = buffer.ReadInt16(updateSequenceOffset, Endianness.LittleEndian);

            for (int i = 1; i < fixupHeader.updateSequenceLength; i++) {
                if (buffer.ReadInt16(offset + i * bytesPerSector - 2, Endianness.LittleEndian) != sequenceNumber)
                    throw new FormatException(string.Format("Sequence number mismatch in the update sequence. This may indicate an incomplete write or a bad sector."));

                updateSequenceOffset += 2;
                var currentNumber = buffer.ReadInt16(updateSequenceOffset, Endianness.LittleEndian);
                buffer.WriteVal(offset + i * bytesPerSector - 2, currentNumber, Endianness.LittleEndian);
            }
        }








        internal IVolume rawVolume;
        internal string name;
        internal long bytesPerSector;
        internal long sectorsPerCluster;
        internal long bytesPerCluster;
        internal long bytesPerMFTRecord;
        internal long bytesPerIndexRecord; // on reading, the size reported in the index root is used


        /// <summary>
        /// The master file table that contains all file records
        /// </summary>
        internal MFTFile MFT { get; }

        /// <summary>
        /// A copy of the first few entries of the MFT (4 records or a whole cluster, whichever is larger)
        /// </summary>
        internal MFTFile MFTMir { get; }

        /// <summary>
        /// Used to log every transaction on the file system. In case of sudden disconnection from the volume, this is used to rollback an incomplete transaction.
        /// </summary>
        internal NTFSLogFile LogFile { get; }

        /// <summary>
        /// Information about the volume (name, NTFS version, flags)
        /// </summary>
        internal NTFSFile Volume { get; }

        /// <summary>
        /// Definitions of each attribute type (e.g. min/max length)
        /// </summary>
        internal NTFSFile AttrDef { get; }

        /// <summary>
        /// The root directory (this is a directory, not a file)
        /// </summary>
        internal NTFSFile RootDir { get; }

        /// <summary>
        /// Cluster allocation bitmap
        /// </summary>
        internal NTFSFile Bitmap { get; }

        /// <summary>
        /// Boot record (includes the VBR and a few following sectors)
        /// </summary>
        internal NTFSFile Boot { get; }

        /// <summary>
        /// A sparse file that describes all of the bad clusters (by this definition it cannot be read)
        /// </summary>
        internal NTFSFile BadClus { get; }

        /// <summary>
        /// ?
        /// </summary>
        internal NTFSFile Secure { get; }

        /// <summary>
        /// An array (128kB) that contains upper case chars for each unicode char
        /// </summary>
        internal ushort[] UpCase { get; }

        /// <summary>
        /// ??
        /// </summary>
        NTFSFile Extend { get; }

        /// <summary>
        /// NTFS actually imposes very few restrictions on names.
        /// However, Windows is much stricter with file names.
        /// </summary>
        NamingConventions namingConventions = new NamingConventions() {
            MaxNameLength = 260,
            ForbiddenChars = new char[0],
            ForbiddenLeadingChars = new char[0],
            ForbiddenTrailingChars = new char[0],
            ForbiddenNames = new string[0],
            CaseSensitive = true
        };



        private void DumpFile(string path, string target, LogContext log)
        {
            log.Debug("dumping " + path + "...");
            var file = GetRoot().NavigateToFile(path, OpenMode.Existing).AsImplementation<NTFSFile>();
            foreach (var attr in file.FileRecord.attributes)
                log.Debug("  attribute: 0x{0:X8} \"{1}\"", Utilities.EnumToInt(attr.type), attr.name);
            var buffer = file.FileRef.Read(0, file.GetSize().Value);
            using (var logFile = System.IO.File.OpenWrite(target))
                logFile.Write(buffer, 0, buffer.Count());
            log.Debug("dump succeeded, length was {0}", buffer.Length);
        }


        public NTFSVolume(IVolume rawVolume, string verb, Context context, out List<string> issues)
        {
            FileSystemRef = new FileSystemRef(this);

            issues = new List<string>();

            this.rawVolume = rawVolume;
            var info = rawVolume.GetInfo();

            var vbrBuffer = new byte[512]; // The VBR is 512 bytes long, period. Even on a 4k sector disk.
            rawVolume.Read(0, vbrBuffer.Length, vbrBuffer, 0);
            var vbr = vbrBuffer.ReadObject<VBR>(0);
            rawVolume.Read(rawVolume.GetSize() - vbrBuffer.Length, vbrBuffer.Length, vbrBuffer, 0);
            var vbrMirr = vbrBuffer.ReadObject<VBR>(0);

            if (vbr.magicNumber != "NTFS    ")
                throw new AOSRejectException("The volume does not contain an NTFS file system", verb, rawVolume);

            if (vbr.volumeLength * vbr.bytesPerSector != rawVolume.GetSize())
                issues.Add(string.Format("the filesystem reports a volume size different from the actual volume size (expected: {0} bytes, actual: {1} bytes)", vbr.volumeLength * vbr.bytesPerSector, rawVolume.GetSize()));

            bytesPerSector = vbr.bytesPerSector;
            sectorsPerCluster = vbr.sectorsPerCluster;
            bytesPerCluster = bytesPerSector * sectorsPerCluster;
            bytesPerMFTRecord = vbr.clustersPerMFTRecord < 0 ? (1 << -(vbr.clustersPerMFTRecord)) : vbr.clustersPerMFTRecord * vbr.sectorsPerCluster;
            bytesPerIndexRecord = vbr.clustersPerIndexRecord < 0 ? (1 << -(vbr.clustersPerIndexRecord)) : vbr.clustersPerIndexRecord * vbr.sectorsPerCluster;

            if (bytesPerMFTRecord % bytesPerSector != 0)
                throw new Exception("bytes per MFT record must be a multiple of the sector size");
            if (bytesPerIndexRecord % bytesPerIndexRecord != 0)
                throw new Exception("bytes per index buffer must be a multiple of the sector size");


            // todo: compare with mirror VBR (last sector/cluster?)


            context.Log.Debug("Magic Number: 0x{0:X16}", vbr.magicNumber);
            context.Log.Debug("Bytes per sector: {0}", vbr.bytesPerSector);
            context.Log.Debug("Sectors per cluster: 0x{0:X2}", vbr.sectorsPerCluster);
            context.Log.Debug("Media Type: 0x{0:X2}", vbr.mediaType);
            context.Log.Debug("Whatever: 0x{0:X8}", vbr.whatever);
            context.Log.Debug("Volume Length (Sectors): 0x{0:X8}", vbr.volumeLength);
            context.Log.Debug("MFT location (cluster): 0x{0:X16}", vbr.mftLocation);
            context.Log.Debug("MFT mirror location (cluster): 0x{0:X16}", vbr.mftMirrLocation);
            context.Log.Debug("Clusters per MFT record (cluster): 0x{0:X2} => {1}", vbr.clustersPerMFTRecord, Utilities.GetSizeString(bytesPerMFTRecord, false));
            context.Log.Debug("Clusters per index record (cluster): 0x{0:X2}", vbr.clustersPerIndexRecord);
            context.Log.Debug("Volume Serial Number: 0x{0:X16}", vbr.volumeSerialNumber);



            // load basic NTFS files
            try {
                MFT = new MFTFile(this, vbr.mftLocation, 0);
                MFTMir = new MFTFile(MFT.GetFile(1, null));
                LogFile = new NTFSLogFile(MFT.GetFile(2, null));
                Volume = MFT.GetFile(3, null);
            } catch (Exception) {
                MFTMir = new MFTFile(this, vbr.mftMirrLocation, 1);
                MFT = new MFTFile(MFTMir.GetFile(0, null));
                LogFile = new NTFSLogFile(MFTMir.GetFile(2, null));
                Volume = MFTMir.GetFile(3, null);
            }

            // load some more basic NTFS files
            AttrDef = MFT.GetFile(4, null);
            RootDir = MFT.GetFile(5, null);
            Bitmap = MFT.GetFile(6, null);
            Boot = MFT.GetFile(7, null);
            BadClus = MFT.GetFile(8, null);

            // the remaining entries are only available on higher NTFS versions


            context.Log.Debug("Volume name: \"{0}\"", GetName());

            var ntfsVolInfo = Volume.FileRecord.ReadAttribute(NTFSAttributeType.VolumeInformation, null);
            var flags = ntfsVolInfo.ReadInt16(0, Endianness.LittleEndian);
            context.Log.Debug("NTFS Version: {0}.{1}", ntfsVolInfo[8], ntfsVolInfo[9]);
            context.Log.Debug("Volume flags: 0x{0:X4}{1}", flags,
                ((flags & 0x1) != 0 ? " dirty" : "") +
                ((flags & 0x2) != 0 ? " resize-logfile" : "") +
                ((flags & 0x4) != 0 ? " upgrade-on-mount" : "") +
                ((flags & 0x8) != 0 ? " mounted-on-NT4" : "") +
                ((flags & 0x10) != 0 ? " delete-usn-underway" : "") +
                ((flags & 0x20) != 0 ? " repair-obj-ids" : "") +
                ((flags & 0x8000) != 0 ? " modified-by-chkdsk" : ""));


            if (ntfsVolInfo[8] >= 3) {
                var upcaseFile = MFT.GetFile(0xA, RootDir);
                var upcaseFileSize = upcaseFile.GetSize().Value;
                long temp = 0;
                if (upcaseFileSize == 2 * (1 << 16))
                    UpCase = upcaseFile.FileRef.Read(0, upcaseFileSize).ReadUInt16Arr(ref temp, 1 << 16, Endianness.LittleEndian);
                else
                    issues.Add("the $UpCase file does not have the expected length");
            }


            foreach (var f in GetRoot().NavigateToFolder("$Extend/$RmMetadata/$TxfLog", OpenMode.Existing).GetChildren())
                context.Log.Debug("{0}: {1}", f.Cast<IFile>() != null ? "f" : "d", f.GetName());


            //for (long i = 0; i < MFT.DataAttribute.GetSize() / bytesPerMFTRecord; i++) {
            //    try {
            //        log.Debug(string.Format("file: {0:X12}, flags: {1:X4}, path: {2}",
            //            i,
            //            GetFile(i, null).FileRecord.flags,
            //            GetFile(i, null).GetInfo().Path));
            //    } catch (Exception ex) {
            //        log.Debug(string.Format("file: {0:X12}, {1}",
            //            i,
            //            ex.Message));
            //    }
            //}


            System.IO.Directory.CreateDirectory(@"C:\Developer\vhd\newdump");
            DumpFile("$MFT", @"C:\Developer\vhd\newdump\mft.bin", context.Log);
            DumpFile("$LogFile", @"C:\Developer\vhd\newdump\logfile.bin", context.Log);
            DumpFile("$Bitmap", @"C:\Developer\vhd\newdump\bitmap.bin", context.Log);
            LogFile.Dump(@"C:\Developer\vhd\newdump", context.Controller);
            //LogFile.Recover(@"C:\Developer\vhd\newdump\recovery_debug.txt", true);
        }

        //private NTFSFile NavigateTo(string path, bool isFile)
        //{
        //    var elements = path == "" ? new string[0] : path.Split('/').Select(e => AppInstall.Networking.NetUtils.UnescapeFromURL(e)).ToArray();
        //
        //    if (!elements.Any() && isFile)
        //        throw new System.IO.FileNotFoundException("the root directory is not a file");
        //
        //    var file = RootDir;
        //    for (int i = 0; i < elements.Length; i++)
        //        file = file.NavigateTo(elements[i], i == elements.Length - 1 && isFile);
        //
        //    return file;
        //}

        public NamingConventions GetNamingConventions()
        {
            return namingConventions;
        }

        public string GetName()
        {
            var attr = Volume.FileRecord.ReadAttribute(NTFSAttributeType.VolumeName, null);
            return attr.ReadString(0, attr.Length / 2, StringFormat.Unicode, Endianness.LittleEndian); ;
        }

        public void SetName(string name)
        {
            if (name == null)
                throw new ArgumentNullException($"{name}");

            var buffer = new byte[name.Length * 2];
            buffer.WriteVal(0, name, StringFormat.Unicode, Endianness.LittleEndian);

            var attr = Volume.FileRecord.GetAttributes(NTFSAttributeType.VolumeName, null).First();
            attr.ChangeSize(buffer.Length);
            attr.Write(0, buffer.Length, buffer, 0);
        }

        public IFolder GetRoot()
        {
            return RootDir.FolderRef.Retain();
        }

        public long? GetTotalSpace()
        {
            return rawVolume.GetSize();
        }

        public long? GetFreeSpace()
        {
            long freeClusters = 0;

            var buffer = new byte[16777216];
            var length = Math.Min(Bitmap.Data.GetSize(), (rawVolume.GetSize() / bytesPerCluster + 7) / 8);
            long offset = 0;

            while (length > 0) {
                var blockSize = Math.Min(length, buffer.Length);
                Bitmap.Data.Read(offset, blockSize, buffer, 0);
                length -= blockSize;

                for (var i = 0; i < blockSize; i++) {
                    var b = buffer[i];
                    if (b == 0) {
                        freeClusters += 8;
                    } else if (b != 0xFF) {
                        for (int bit = 0; bit < 8; bit++) {
                            if ((b & 1) == 0)
                                freeClusters++;
                            b >>= 1;
                        }
                    }
                }
            }

            return freeClusters * bytesPerCluster;
        }

        public IEnumerable<string> GetFiles(string query)
        {
            throw new NotImplementedException();
        }

        public void Move(IFileSystemObject file, IFolder destination, string newName)
        {
            file.Move(destination, newName, true);
        }

        public IFileSystemObject Copy(IFileSystemObject file, IFolder destination, string newName, MergeMode mode)
        {
            return file.Copy(destination, newName, mode, true);
        }
    }
}
