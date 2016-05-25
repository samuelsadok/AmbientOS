using System;
using System.Collections.Generic;
using System.Linq;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem.NTFS
{
    partial class NTFSLogFile
    {
        public abstract class LogFileEntry
        {
            /// <summary>
            /// The NTFS log file to which this record belongs
            /// </summary>
            public Client Client { get; }

            /// <summary>
            /// The header of this log entry
            /// </summary>
            public LogEntryHeader Header { get; }

            /// <summary>
            /// Returns the LSN of this log entry.
            /// This value is not always valid.
            /// </summary>
            public long LSN { get { return Header.thisLSN; } }

            /// <summary>
            /// Returns the LSN that comes after this entry.
            /// This takes into account the effective length of the current entry.
            /// </summary>
            public long NextLSN { get { return Header.nextLSN; } }

            /// <summary>
            /// The volume that holds this log entry
            /// </summary>
            public NTFS Volume { get { return Client.LogFile.Volume; } }

            /// <summary>
            /// Creates a new log entry from scratch.
            /// Most fields of the header will be invalid and set up later.
            /// </summary>
            protected LogFileEntry(Client client)
            {
                Client = client;
                Header = new LogEntryHeader(client);
            }

            /// <summary>
            /// Creates a log entry with an already existing header.
            /// This should only be used for derserializing.
            /// </summary>
            protected LogFileEntry(Client client, LogEntryHeader header)
            {
                Client = client;
                Header = header;
            }

            /// <summary>
            /// Reads a logfile entry from a buffer.
            /// Returns null if the entry is invalid (that is the actual LSN doesn't match the specified LSN).
            /// </summary>
            /// <param name="bufferProvider">Should read 4kB from $LogFile at the specified offset. The data read must already be fixed (update sequence applied).</param>
            /// <param name="LSN">The LSN to be loaded.</param>
            public static LogFileEntry FromBuffer(Client client, long LSN)
            {
                var offset = (LSN << 3) % client.LogFile.RestartArea.fileSize; //.File.Length;

                // load the correct page
                var pageNumber = offset / I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST;
                var initialPageNumber = pageNumber;
                var pageOffset = offset % I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST;
                var pageBuffer = client.LogFile.ReadRecordBuffer(pageNumber);

                // we assume that the log entry header is never splitted across pages (seems reasonable but not yet verified)
                var recordHeader = pageBuffer.ReadObject<LogEntryHeader>(pageOffset);
                if (recordHeader.thisLSN != LSN)
                    return null;

                pageOffset += client.LogFile.RestartArea.recordHeaderLength;


                byte[] recordData = new byte[recordHeader.clientDataLength];

                // load the data of the log entry - may span multiple pages
                while (true) {
                    var currentCount = Math.Min(I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST - pageOffset, recordHeader.clientDataLength);
                    Array.Copy(pageBuffer, pageOffset, recordData, recordData.Length - recordHeader.clientDataLength, currentCount);

                    recordHeader.clientDataLength -= (uint)currentCount;
                    pageOffset += currentCount;

                    // note: we always need to handle the end of the block, sometimes just for calculating the next LSN
                    if (pageOffset > I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST - client.LogFile.RestartArea.recordHeaderLength) {
                        pageOffset = client.LogFile.RestartArea.logPageDataOffset;
                        pageNumber++;
                    }

                    if (recordHeader.clientDataLength == 0)
                        break;

                    pageBuffer = client.LogFile.ReadRecordBuffer(pageNumber);
                }

                recordHeader.startPage = initialPageNumber;
                recordHeader.endPage = pageNumber;
                recordHeader.nextLSN = LSN + ((pageNumber * I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST + pageOffset - offset) >> 3);

                switch (recordHeader.recordType) {
                    case LogRecordType.clientRestart: return LogRestart.FromBuffer(client, recordHeader, recordData);
                    case LogRecordType.clientRecord: return LogRecord.FromBuffer(client, recordHeader, recordData);
                    default: throw new Exception("invalid record type");
                }
            }

            public void Write()
            {
                var header = new byte[0x30];
                header.WriteVal(0, Header);
                var body = ToBuffer();

                // todo: write to log page
            }

            /// <summary>
            /// Shall generate a buffer that contains the log entry content.
            /// It shall not include the log entry header.
            /// The content does not have to be aligned to 8 bytes.
            /// </summary>
            protected abstract byte[] ToBuffer();

            public abstract void Redo(bool dryrun);
            public abstract void Undo();

            public virtual string ToString(bool verbose)
            {
                var result = string.Format("LSN: {0:X16} ({1:X8}:{2}), prev: {3:X8}, undo: {4:X8}, tID: {5:X16}",
                    Header.thisLSN,
                    Header.startPage,
                    Header.endPage - Header.startPage + 1,
                    Header.clientPreviousLSN,
                    Header.clientUndoNextLSN,
                    Header.transactionID);

                if (verbose) {
                    result += string.Format(", data: {0:X4}, type: {1}, flags: {2:X4}, reserved: {3:X4}",
                        Header.clientDataLength,
                        Header.recordType == LogRecordType.clientRecord ? "rcrd" : Header.recordType == LogRecordType.clientRestart ? "rstr" : "????",
                        Header.flags,
                        Header.reserved);
                }

                return result;
            }

            public override string ToString()
            {
                return ToString(false);
            }

            public enum LogRecordType
            {
                /// <summary>
                /// A normal record, associated with an operation.
                /// </summary>
                clientRecord = 1,

                /// <summary>
                /// A commit entry. This entry represents a sane state of the file system.
                /// </summary>
                clientRestart
            }


            [Endianness(Endianness.LittleEndian)]
            public class LogEntryHeader
            {
                /// <summary>
                /// LSN of this record
                /// </summary>
                public long thisLSN;

                /// <summary>
                /// LSN of previous record (of this client)
                /// </summary>
                public long clientPreviousLSN;

                /// <summary>
                /// LSN of next undo record (of this client)
                /// </summary>
                public long clientUndoNextLSN;

                /// <summary>
                /// The length of the data in this record.
                /// When the current page overflows, the data is not contiguous but continued after the header of the next page.
                /// </summary>
                //[FieldSpecs(LengthOf = "data")]
                public UInt32 clientDataLength;

                /// <summary>
                /// Identifies the client to which this entry belongs.
                /// </summary>
                public ClientID clientID;

                /// <summary>
                /// Specifies if this is a checkpoint (restart entry) or a normal log record
                /// </summary>
                public LogRecordType recordType;

                /// <summary>
                /// an index into the transaction table
                /// </summary>
                public Int32 transactionID;

                /// <summary>
                /// bit 0: indicates if the log entry crosses the page boundary
                /// bits 2:1:
                /// 
                /// value 0 (flags = 0/1):
                /// 
                /// 
                /// value 1 (flags = 2/3):
                /// seems always related to deleting an attribute, index entry or file record or committing the transaction
                /// but also this was found 6x:
                /// update non-resident value in $SDS Data of /$Secure
                /// 
                /// value 2 (flags = 4/5):
                /// seems always related to creating an attribute, index entry or file record, loading an attribute or dumping a table
                /// 
                /// value 3 (flags = 6/7):
                /// implies that the operation is one of:
                ///     - dummy operation
                ///     - write to unnamed Data
                ///     - operation 0x25
                /// 
                /// commit implies that value is 1
                /// table dump implies that value is 2
                /// write to unnamed Data implies value is one of: 3, 0
                /// checkpoint implies that value is 0
                /// 
                /// </summary>
                public Int32 flags;

                /// <summary>
                /// no values other than 0 have been observed
                /// </summary>
                public UInt16 reserved;

                /// <summary>
                /// The LSN that comes after this one.
                /// This field may not be valid depending on how the header was created.
                /// </summary>
                [FieldSpecs(Ignore = true)]
                public long nextLSN;

                /// <summary>
                /// The log page on which this logrecord starts.
                /// This is for debuggîng only.
                /// </summary>
                [FieldSpecs(Ignore = true)]
                public long startPage;

                /// <summary>
                /// The log page on which this logrecord ends.
                /// This is for debuggîng only.
                /// </summary>
                [FieldSpecs(Ignore = true)]
                public long endPage;

                /// <summary>
                /// Only for derserializing
                /// </summary>
                private LogEntryHeader()
                {
                }

                public LogEntryHeader(Client client)
                {
                    clientID = client.ID;
                }
            }
        }


        /// <summary>
        /// Two restarts are never consecutive, a dummy record (with operation 0) is inserted if neccessary
        /// </summary>
        public class LogRestart : LogFileEntry
        {
            public Int64 unknown1;

            /// <summary>
            /// Specifies the LSN of the last checkpoint or commit (whichever is closer) before this checkpoint.
            /// </summary>
            public Int64 lastCommitOrCheckpointLSN;

            /// <summary>
            /// Specifies the LSN of the 1D record that precedes this checkpoint (if present)
            /// </summary>
            public Int64 AttributeListLSN;

            /// <summary>
            /// Specifies the LSN of the 1E record that precedes this checkpoint (if present)
            /// </summary>
            public Int64 AttributeNameListLSN;

            /// <summary>
            /// Specifies the LSN of the 1F record that precedes this checkpoint (if present)
            /// </summary>
            public Int64 DirtyClusterListLSN;

            /// <summary>
            /// This is most likely the transaction table dump (not observed yet)
            /// </summary>
            public Int64 TransactionListLSN;

            /// <summary>
            /// Specifies the data length of the 1D record that precedes this checkpoint (if present)
            /// </summary>
            public UInt32 AttributeListLength;

            /// <summary>
            /// Specifies the data length of the 1E record that precedes this checkpoint (if present)
            /// </summary>
            public UInt32 AttributeNameListLength;

            /// <summary>
            /// Specifies the data length of the 1F record that precedes this checkpoint (if present)
            /// </summary>
            public UInt32 DirtyClusterListLength;

            /// <summary>
            /// This is most likely the transaction table length (not observed yet)
            /// </summary>
            public UInt32 TransactionListLength;

            public Int64 unknown2;
            public Int64 unknown3;
            public Int64 unknown4;
            public Int64 unknown5;
            public Int64 unknown6;

            /// <summary>
            /// So far it seems that when there is no 1F record (last1FLSN = 0), this is equal to lastNot1D1E1FLSN.
            /// If there is a 1F record, this points to some quite old record, which may even be before another restart.
            /// </summary>
            public Int64 unknownLSN;

            private LogRestart(Client client, LogEntryHeader header)
                : base(client, header)
            {
            }

            public static LogRestart FromBuffer(Client client, LogEntryHeader header, byte[] buffer)
            {
                var result = new LogRestart(client, header);
                long offset = 0;
                buffer.ReadObject(ref offset, result, Endianness.LittleEndian);
                return result;
            }

            /// <summary>
            /// Loads the table dumps referenced by this checkpoint.
            /// This is neccessary for recovery, as operations following this checkpoint will require these tables.
            /// </summary>
            public override void Redo(bool dryrun)
            {
                AttributeList newAttributeList;
                AttributeNameList newAttributeNameList;
                DirtyClusterList newClusterList;
                TransactionList newTransactionList;

                if (AttributeListLSN == 0)
                    newAttributeList = new AttributeList(Client);
                else
                    newAttributeList = (AttributeList)((LogRecord)FromBuffer(Client, AttributeListLSN)).RedoOperation;

                if (AttributeNameListLSN == 0)
                    newAttributeNameList = new AttributeNameList(Client);
                else
                    newAttributeNameList = (AttributeNameList)((LogRecord)FromBuffer(Client, AttributeNameListLSN)).RedoOperation;

                if (DirtyClusterListLSN == 0)
                    newClusterList = new DirtyClusterList(Client);
                else
                    newClusterList = (DirtyClusterList)((LogRecord)FromBuffer(Client, DirtyClusterListLSN)).RedoOperation;

                if (TransactionListLSN == 0)
                    newTransactionList = new TransactionList(Client);
                else
                    newTransactionList = (TransactionList)((LogRecord)FromBuffer(Client, TransactionListLSN)).RedoOperation;


                // TODO: REMOVE!!! THIS IS WRONG, NO FIXUP APPLIED, WRONG PLACE (is it?), ETC
                foreach (var cluster in Client.DirtyClusterList.GetAll().ToArray().Except(newClusterList.GetAll().ToArray())) {
                    if (!dryrun)
                        Volume.rawStream.Write(cluster.LCN * Volume.bytesPerCluster, Volume.bytesPerCluster, cluster.data, 0);
                    cluster.dirty = false;
                }

                Client.AttributeList = newAttributeList;
                Client.AttributeNameList = newAttributeNameList;
                Client.DirtyClusterList = newClusterList;
                Client.TransactionList = newTransactionList;
            }

            public override void Undo()
            {
                throw new NotSupportedException();
            }

            public override string ToString(bool verbose)
            {
                return base.ToString(verbose) + string.Format(" ### CHECKPOINT ###  last commit/checkpoint {0:X8} attribute table dump {1:X8} ({2:X4}) attribute name table dump {3:X8} ({4:X4}) dirty clusters table dump {5:X8} ({6:X4}) transaction table dump {7:X8} ({8:X4}) unknown LSN: {9:X8}, unknowns: {10:X8} {11:X16} {12:X16} {13:X16} {14:X16} {15:X16}\r\n",
                    lastCommitOrCheckpointLSN,
                    AttributeListLSN, AttributeListLength,
                    AttributeNameListLSN, AttributeNameListLength,
                    DirtyClusterListLSN, DirtyClusterListLength,
                    TransactionListLSN, TransactionListLength,
                    unknownLSN,
                    unknown1, unknown2, unknown3, unknown4, unknown5, unknown6);
            }

            protected override byte[] ToBuffer()
            {
                var result = new byte[0x70];
                result.WriteVal(0, this);
                return result;
            }
        }

        [Endianness(Endianness.LittleEndian)]
        public class LogRecord : LogFileEntry
        {
            /// <summary>
            /// The main operation that this record represents
            /// </summary>
            public LogOperationType redoOperation;

            /// <summary>
            /// The operation that reverses the main operation.
            /// </summary>
            public LogOperationType undoOperation;

            public UInt16 redoOffset;
            public UInt16 redoLength;
            public UInt16 undoOffset;
            public UInt16 undoLength;
            public UInt16 targetAttribute;

            /// <summary>
            /// Specifies how many LCNs are included at the end of this header.
            /// If this is zero, the unknown3 field has some value instead.
            /// </summary>
            [FieldSpecs(LengthOf = "LCNs")]
            UInt16 lcnCount;

            /// <summary>
            /// Specifies, at what offset of the specified cluster and sector the attribute header can be found.
            /// </summary>
            public UInt16 recordOffset;

            /// <summary>
            /// Specifies, where in the attribute the data of this record should be written.
            /// </summary>
            public UInt16 attributeOffset;

            /// <summary>
            /// The target sector, relative to the target VCN/LCN
            /// </summary>
            public UInt16 targetSector;

            /// <summary>
            /// This MAY be the number of sectors per logical block.
            /// i.e. for file records, this is normally 2, for index buffers this is normally 8
            /// </summary>
            public UInt16 sectorsPerBlock;

            /// <summary>
            /// Specifies the virtual cluster number of the MFT where this attribute can be found.
            /// (VCN 0 is the first cluster of the MFT)
            /// </summary>
            public UInt32 targetVCN;

            /// <summary>
            /// Only 0 observed
            /// </summary>
            public UInt32 unknown1;

            /// <summary>
            /// Specifies the linear cluster numbers where the attribute can be found.
            /// LCN 0 is the first cluster of the volume.
            /// </summary>
            public Int64[] LCNs;


            public LogOperation RedoOperation { get; private set; }
            public LogOperation UndoOperation { get; private set; }


            /// <summary>
            /// Creates a new log record from scratch.
            /// </summary>
            public LogRecord(Client client)
                : this(client, 0x18, null, 0, 2, 0, 0, null) // todo: see if 2 is correct as arg
            {
            }

            /// <summary>
            /// Creates a new log record from scratch.
            /// </summary>
            public LogRecord(Client client, ushort attrID, Cluster[] clusters, ushort startSector, ushort sectorsPerBlock, ushort recordOffset, ushort attributeOffset, LogOperation redoOperation)
                : base(client)
            {
                targetAttribute = attrID;

                this.recordOffset = recordOffset;
                this.attributeOffset = attributeOffset;
                this.targetSector = startSector;
                this.sectorsPerBlock = sectorsPerBlock;

                if ((clusters?.Length ?? 0) > 0) {
                    targetVCN = (uint)clusters.Select(c => c.VCN).First();
                    LCNs = (clusters?.Length ?? 0) >= 0 ? clusters.Select(c => c.LCN).ToArray() : new long[0];
                } else {
                    targetVCN = 0;
                    LCNs = new long[0];
                }
                RedoOperation = redoOperation;
            }

            /// <summary>
            /// Creates a log record with an already existing header.
            /// This should only be used for derserializing.
            /// </summary>
            private LogRecord(Client client, LogEntryHeader header)
                : base(client, header)
            {
            }

            public static LogRecord FromBuffer(Client client, LogEntryHeader header, byte[] buffer)
            {
                var t = client.TransactionList.Get(header.transactionID);

                var result = new LogRecord(client, header);
                long offset = 0;
                buffer.ReadObject(ref offset, result, Endianness.LittleEndian);

                var redoData = buffer.Skip(result.redoOffset).Take(result.redoLength).ToArray();
                var undoData = buffer.Skip(result.undoOffset).Take(result.undoLength).ToArray();
                //var checksum = buffer.GetInt32Checksum(0, buffer.Length - 8);

                result.RedoOperation = LogOperation.FromBuffer(t, result.redoOperation, redoData, undoData);
                result.UndoOperation = LogOperation.FromBuffer(t, result.undoOperation, undoData, redoData);

                if (result.RedoOperation != null)
                    result.RedoOperation.LogRecord = result;
                if (result.UndoOperation != null)
                    result.UndoOperation.LogRecord = result;

                return result;
            }

            /// <summary>
            /// Sets up this log entry.
            /// This will generate the associated undo operation.
            /// This must not be called if the entry was read from disk.
            /// Else, it must be called before calling Redo or Undo (only once).
            /// </summary>
            public void Setup()
            {
                var clusters = GetReferencedClusters(false);
                if (RedoOperation != null && UndoOperation == null)
                    UndoOperation = RedoOperation.GetUndoOperation(clusters.Item1, clusters.Item2);
            }

            public override void Redo(bool dryrun)
            {
                if (RedoOperation == null && UndoOperation == null) // this check may not be neccessary
                    return;

                var clusters = GetReferencedClusters(true);
                if (RedoOperation != null)
                    RedoOperation.Execute(clusters.Item1, clusters.Item2, dryrun);
            }

            public override void Undo()
            {
                if (UndoOperation == null)
                    throw new NotSupportedException("this operation cannot be undone");

                var clusters = GetReferencedClusters(true); // do we need to mark them as dirty, like in redo? probably it's already guaranteed to be dirty when undoing
                UndoOperation.Execute(clusters.Item1, clusters.Item2, false);
            }

            protected override byte[] ToBuffer()
            {
                redoOperation = RedoOperation?.Type ?? LogOperationType.NoOp;
                undoOperation = UndoOperation?.Type ?? LogOperationType.NoOp;

                var redoData = RedoOperation?.ToBuffer() ?? new byte[0];
                var undoData = UndoOperation?.ToBuffer() ?? new byte[0];

                redoOffset = (UInt16)(0x20 + Math.Max(LCNs.Count(), 1) * 8);
                redoLength = (UInt16)(redoData.Length);
                undoOffset = (UInt16)((redoOffset + redoLength + 7) / 8 * 8);
                undoLength = (UInt16)(undoData.Length);

                var result = new byte[undoOffset + undoLength];
                result.WriteVal(0, this);
                Array.Copy(redoData, 0, result, redoOffset, redoLength);
                Array.Copy(undoData, 0, result, undoOffset, undoLength);
                return result;
            }

            public override string ToString(bool verbose)
            {
                var result = new System.Text.StringBuilder();

                if (lcnCount > 0x01)
                    result.Append("*** multiple LSNs *** ");

                if (RedoOperation is UnsupportedOperation || UndoOperation is UnsupportedOperation)
                    verbose = true;

                result.Append(base.ToString(verbose));

                if (verbose) {
                    result.Append(string.Format(" redo: {0:X2}, undo: {1:X2}, attr: {2:X4}, r.off: {3:X4}, a.off {4:X4}, vcn: {5:X8}, sector: {6:X4} ({7:X4}), lcns ({8}): {9}, reserved: {10:X8}",
                        redoOperation.ToInt(), undoOperation.ToInt(),
                        targetAttribute,
                        recordOffset, attributeOffset,
                        targetVCN,
                        targetSector,
                        sectorsPerBlock,
                        lcnCount,
                        string.Join(", ", LCNs.Select(lcn => string.Format("{0:X16}", lcn))),
                        unknown1
                    ));
                }

                if ((RedoOperation?.Type ?? LogOperationType.NoOp) != LogOperationType.NoOp)
                    result.Append(" redo: " + RedoOperation.ToString());
                else
                    verbose = true;

                if (verbose) {
                    if ((UndoOperation?.Type ?? LogOperationType.NoOp) != LogOperationType.NoOp)
                        result.Append(" undo: " + UndoOperation.ToString());
                    else if ((UndoOperation?.Type ?? LogOperationType.NoOp) == LogOperationType.NoOp)
                        result.Append(" dummy operation");
                }

                return result.ToString();
            }

            public NTFSAttribute GetReferencedAttribute()
            {
                return Client.AttributeList.Get(targetAttribute);
            }

            /// <summary>
            /// This should only be used for debugging
            /// </summary>
            public NTFSAttribute GetActualReferencedAttribute()
            {
                var directAttr = GetReferencedAttribute();
                var fileRef = ((targetVCN * Volume.sectorsPerCluster + targetSector) * Volume.bytesPerSector) / Volume.bytesPerMFTRecord;
                var file = Volume.MFT.GetFile(fileRef, null);
                return file.FileRecord.attributes.First(a => a.FileRecordOffset == recordOffset);
            }

            public Tuple<Cluster[], long> GetReferencedClusters(bool markAsDirty)
            {
                var clusters = new Cluster[LCNs.Count()];
                if (clusters.Count() != 0) {
                    var attr = GetReferencedAttribute();
                    for (int i = 0; i < LCNs.Count(); i++) {
                        clusters[i] = attr.GetCluster(targetVCN + i, LCNs[i], true);
                        if (markAsDirty)
                            Client.DirtyClusterList.Add(Header.thisLSN, targetAttribute, clusters[i]);
                    }
                }
                return new Tuple<Cluster[], long>(clusters, sectorsPerBlock * Volume.bytesPerSector + recordOffset);
            }


            /// <summary>
            /// Returns the remaining bytes in the block.
            /// This is useful for operations that need to shift the remaining data.
            /// </summary>
            public long GetRemainingBytes(long offset)
            {
                return (targetSector + sectorsPerBlock) * Volume.bytesPerSector - offset;
            }
        }
    }
}

