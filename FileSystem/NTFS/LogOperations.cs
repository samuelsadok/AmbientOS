using System;
using System.Collections.Generic;
using System.Linq;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem.NTFS
{
    partial class NTFSLogFile
    {
        [EnumType("System.Int16")]
        public enum LogOperationType
        {
            /// <summary>
            /// Don't do anything.
            /// In some special cases, the associated data (e.g. undo data) belongs to the other operation (i.e. redo)
            /// </summary>
            NoOp = 0x00,

            /// <summary>
            /// This is used as the undo operation of a commit record.
            /// </summary>
            Compensation = 0x01,

            /// <summary>
            /// Initialize a new file record.
            /// This is done by writing the associated data to the specified location (and then setting the in-use flag at offset 0x16? - not neccessary in the observed occasions).
            /// Both attribute offset and record offset are 0.
            /// The update sequence is NOT applied to the data, e.g. before writing it to disk, it must be applied.
            /// </summary>
            InitFileRecord = 0x02,

            /// <summary>
            /// Invalidate a file record by setting the in-use flag to 0.
            /// This is done by writing the associated data to the specified location, and then clearing the in-use flag at offset 0x16.
            /// Both attribute offset and record offset are 0.
            /// </summary>
            InvalidateFileRecord = 0x03,

            /// <summary>
            /// ??
            /// </summary>
            WriteEndOfFileRecord = 0x4,

            /// <summary>
            /// Add a new attribute (resident or non-resident) to a file record.
            /// This shifts all attributes that follow the new one.
            /// Record offset points to the location in the filerecord where to place the attribute.
            /// The associated data contains the complete attribute, including its header.
            /// </summary>
            CreateAttribute = 0x05,

            /// <summary>
            /// Delete an attribute from a file record.
            /// This shifts all attributes that followed the old one.
            /// Record offset points to the location in the filerecord where to remove the attribute.
            /// This operation has no associated data.
            /// </summary>
            DeleteAttribute = 0x06,


            /// <summary>
            /// Write data to the attribute, don't change size.
            /// Write actions typically come in pairs where the undo operation specifies what was at the location before.
            /// </summary>
            /// 
            /// <summary>
            /// usage: redo & undo (always together)
            /// data: yes (both)
            /// lcn valid: yes
            /// attr: 18
            /// 
            /// for each log record:
            /// at the LCN we find file records ('FILE')
            /// attribute offset points to an attribute header in a file record
            /// record offset points to an offset within the attribute (relative to the header start)
            /// 
            /// This record was observed many times. It's almost always followed by a 1B record, but 0B and 14 has also been observed.
            /// One special record has been observed, that has a redo data length of 4C, but there is no data (next record follows immediately). This was close to a sector end
            /// 
            /// The redo data is the new data that was written at the specified location, the undo data is the data that was there before.
            /// aaaand now we know what the unknown1 field stands for: it's the offset (in sectors) [at least the lower 16 bits], from the start of the specified cluster. Only then should we add the attribute offset.
            /// write operations always seem to start somewhere in the attribute and include the entire rest (until end of attribute)
            /// 
            /// Writes to a resident attribute.
            /// </summary>
            UpdateResidentAttribute = 0x07,

            /// <summary>
            /// Write to a non-resident attribute.
            /// </summary>
            UpdateNonResidentAttribute = 0x8,

            /// <summary>
            /// Updates the runlist of a non-resident attribute.
            /// Presumably, if the runlist grows too large, the attribute has to be deleted and re-added.
            /// </summary>
            UpdateRunlist = 0x9,

            /// <summary>
            /// Updates the 3 size fields of a non-resident attribute (allocated, real and initialized size).
            /// This does not do anything to the dataruns.
            /// </summary>
            UpdateAttributeSize = 0xB,

            /// <summary>
            /// Adds an index entry to an existing index root.
            /// The entries that follow the new one are shifted, and the index root attribute grows.
            /// The associated data contains the complete index entry, including its header.
            /// </summary>
            CreateResidentIndexEntry = 0xC,

            /// <summary>
            /// Deletes an index entry from an existing index allocation.
            /// The entries that followed the old one are shifted, and the index root attribute shrinks.
            /// This operation has no associated data.
            /// </summary>
            DeleteResidentIndexEntry = 0xD,

            /// <summary>
            /// Adds an index entry to an existing index allocation.
            /// The entries that follow the new one are shifted.
            /// The associated data contains the complete index entry, including its header.
            /// </summary>
            CreateNonResidentIndexEntry = 0xE,

            /// <summary>
            /// Deletes an index entry from an existing index allocation.
            /// The entries that followed the old one are shifted.
            /// This operation has no associated data.
            /// </summary>
            DeleteNonResidentIndexEntry = 0xF,


            /// <summary>
            /// usage: redo & undo (always together)
            /// data: yes (both)
            /// lcn valid: yes
            /// attr: 18
            /// 
            /// for each log record:
            /// at the LCN we find file records ('FILE')
            /// attribute offset points to an attribute header in a file record
            /// 
            /// this was observed 3 times:
            /// 1. on the data attribute of the $Repair file
            /// 2. and again (shortly after)
            /// 3. on the ObjectID attribute of some TxF file
            /// in all cases, the attributes were non-resident
            /// 
            /// Write to a (resident) index record (i.e. an index record in the index root).
            /// The record points to the index root.
            /// The attribute offset points to an index entry header (after the header, at + 0x18 we find the data).
            /// The associated data is the new data to be written.
            /// </summary>
            UpdateResidentIndexEntry = 0x13,

            /// <summary>
            /// usage: redo & undo (always together)
            /// data: yes (both)
            /// lcn valid: yes (only 2C observed)
            /// attr: only 40 observed
            /// 
            /// for each log record:
            /// at the LCN, we find an index buffer
            /// attribute offset is 0
            /// at the record offset, we find an index record ('INDX')
            /// 
            /// this was observed 3 times:
            /// 1. a stale operation on the index record of $Secure (in .)
            /// 2. adding of the System Volume Information folder (in .)
            /// 3. adding of the root dir (in .) (intrestingly, the root dir entry appears before SysVolInfo but in the logfile after)
            /// 
            /// 
            /// 
            /// 
            /// Write to a (non-resident) index record (i.e. an index record in the index allocation).
            /// Consequently, the record offset is 0.
            /// The attribute offset points to an index entry header (after the header, at + 0x18 we find the data).
            /// The associated data is the new data to be written.
            /// </summary>
            UpdateNonResidentIndexEntry = 0x14,

            /// <summary>
            /// Allocate a bunch of clusters in a non-resident bitmap attribute.
            /// Both attribute and record offsets are zero.
            /// The associated data contains two 32-bit numbers: the block number (i.e. bit number) and the number of bits to set.
            /// </summary>
            AllocateInBitmap = 0x15,

            /// <summary>
            /// Deallocate a bunch of clusters in a non-resident bitmap attribute.
            /// Both attribute and record offsets are zero.
            /// The associated data contains two 32-bit numbers: the block number (i.e. bit number) and the number of bits to clear.
            /// </summary>
            DeallocateInBitmap = 0x16,

            /// <summary>
            /// Flush all operations of the transaction to disk (to the logfile), then forget the transaction.
            /// This does NOT flush other dirty clusters (that don't belong to the logfile).
            /// </summary>
            Commit = 0x1B,

            /// <summary>
            /// usage: redo only
            /// undo: always 0 (sometimes with data, sometimes without)
            /// data: yes
            /// lcn valid: no
            /// attr: 18, 40, 90, B8, E0, 108, 130
            /// 
            /// the redo data is a single Attr-Ref record
            /// the undo data is an attribute name (if applicable)
            /// i.e. for A0 and B0 attributes, there is a name, for 80 there is no name, except for special named data streams
            /// 
            /// Load the specified attribute.
            /// This operation is only used as a redo operation.
            /// The associated redo data holds an AttributeUsage element, the undo data holds the name of the attribute (if any).
            /// </summary>
            LoadNonResidentAttribute = 0x1C,

            /// <summary>
            /// This type of record is always found before a checkpoint.
            /// The associated data contains a dump of the attribute list at the time of the checkpoint.
            /// This is used to re-initialize the list for recovery.
            /// </summary>
            AttributeListDump = 0x1D,

            /// <summary>
            /// This type of record is always found before a checkpoint.
            /// The associated data contains a dump of the attribute name list at the time of the checkpoint.
            /// This is used to re-initialize the list for recovery.
            /// </summary>
            AttributeNameListDump = 0x1E,

            /// <summary>
            /// This type of record is always found before a checkpoint.
            /// The associated data contains a dump of the dirty cluster list at the time of the checkpoint.
            /// This is used to re-initialize the list for recovery.
            /// </summary>
            DirtyClusterListDump = 0x1F,

            /// <summary>
            /// This type of record is always found before a checkpoint.
            /// The associated data contains a dump of the transaction list at the time of the checkpoint.
            /// This is used to re-initialize the list for recovery.
            /// </summary>
            TransactionListDump = 0x20,

            /// <summary>
            /// This was observed only once.
            /// It went like this:
            /// 1. delete resident data attribute
            /// 2. [0x25] (maybe this is update filerecord size field - but there'd have to be more occurrences)
            /// 3. allocate cluster in $Bitmap
            /// 4. create non-resident attribute, allocated size already initialized
            /// 5. update the reals size of the (now non-resident) data attribute
            /// 6. allocate another cluster in $Bitmap
            /// 7. change allocated size to 2 clusters
            /// 8. update runlist (starting at the first byte that actually changes)
            /// 9. update standard attribute (specifically: alteredTime, mftChangedTime and the size fields, the read time is not updated)
            /// 10. update index entry in root dir (file name index) to reflect the changes in time and size change
            /// 11. stale update in the same index entry
            /// 12. increase initialized size (in data attribute header)
            /// 13. increase actual size (in data attribute header)
            /// </summary>
            Misterious = 0x25
        }


        /// <summary>
        /// Represents an operation on the filesystem.
        /// 
        /// During a normal operation, an operation is:
        ///  1. constructed with suitable parameters
        ///  2. queried for associated undo operation
        ///  3. serialized
        ///  4. executed
        /// 
        /// After this, for most operations, the changes are only reflected in cache.
        /// Only after a commit operation is performed, the logfile is flushed to disk (making the transaction permanent).
        /// Only after a checkpoint is inserted, all other dirty clusters are flushed to disk (applying the committed changes).
        /// 
        /// During recovery, an operation is:
        ///  1. deserialized
        ///  2. executed
        /// </summary>
        public abstract class LogOperation
        {
            /// <summary>
            /// The transaction that this operation belongs to.
            /// </summary>
            public Transaction Transaction { get; }

            /// <summary>
            /// The log record that this operation belongs to.
            /// This must be set before calling GetUndoOperation, ToBuffer or Execute.
            /// </summary>
            public LogRecord LogRecord { get; set; }

            public abstract LogOperationType Type { get; }

            protected LogOperation(Transaction transaction)
            {
                Transaction = transaction;
            }

            public static LogOperation FromBuffer(Transaction t, LogOperationType operationType, byte[] buffer, byte[] secondaryBuffer)
            {
                switch (operationType) {
                    case LogOperationType.NoOp: return null;

                    case LogOperationType.InitFileRecord: return new UpdateFileRecordOperation(t, buffer, true);
                    case LogOperationType.InvalidateFileRecord: return new UpdateFileRecordOperation(t, buffer, false);

                    case LogOperationType.CreateAttribute: return new CreateOrDeleteValueOperation(t, buffer, false, true);
                    case LogOperationType.DeleteAttribute: return new CreateOrDeleteValueOperation(t, null, false, true);
                    case LogOperationType.CreateResidentIndexEntry: return new CreateOrDeleteValueOperation(t, buffer, true, true);
                    case LogOperationType.DeleteResidentIndexEntry: return new CreateOrDeleteValueOperation(t, null, true, true);
                    case LogOperationType.CreateNonResidentIndexEntry: return new CreateOrDeleteValueOperation(t, buffer, true, false);
                    case LogOperationType.DeleteNonResidentIndexEntry: return new CreateOrDeleteValueOperation(t, null, true, false);

                    case LogOperationType.UpdateResidentAttribute: return new UpdateValueOperation(t, buffer, true, false);
                    case LogOperationType.UpdateNonResidentAttribute: return new UpdateValueOperation(t, buffer, false, false);
                    case LogOperationType.UpdateResidentIndexEntry: return new UpdateValueOperation(t, buffer, true, true);
                    case LogOperationType.UpdateNonResidentIndexEntry: return new UpdateValueOperation(t, buffer, false, true);

                    case LogOperationType.UpdateAttributeSize: return new UpdateAttributeSizeValue(t, buffer);
                    case LogOperationType.UpdateRunlist: return new UpdateRunlistOperation(t, buffer);

                    case LogOperationType.AllocateInBitmap: return new UpdateBitmapOperation(t, buffer, true);
                    case LogOperationType.DeallocateInBitmap: return new UpdateBitmapOperation(t, buffer, false);

                    case LogOperationType.Commit: return new CommitOperation(t, true);
                    case LogOperationType.Compensation: return new CommitOperation(t, false);
                    case LogOperationType.LoadNonResidentAttribute: return new LoadAttributeOperation(t, buffer, secondaryBuffer);

                    case LogOperationType.AttributeListDump: return new AttributeList(t, buffer);
                    case LogOperationType.AttributeNameListDump: return new AttributeNameList(t, buffer);
                    case LogOperationType.DirtyClusterListDump: return new DirtyClusterList(t, buffer);
                    case LogOperationType.TransactionListDump: return new TransactionList(t, buffer);
                    default: return new UnsupportedOperation(t, buffer, operationType);
                }
            }

            /// <summary>
            /// Generates a log record from this operation, it's corresponding undo operation and the specified attribute, adds it to the log file and executes it.
            /// If the specified attribute was not written to already, a load-attribute-operation is automatically inserted.
            /// </summary>
            /// <param name="attribute">The attribute on which this operation should be executed. This must be non-resident.</param>
            /// <param name="blockNumber">The number of the block on which the operation should be executed.</param>
            /// <param name="blockOffset">The offset (within the block) of the structure (e.g. index entry, attribute) on which the operation should be carried out.</param>
            /// <param name="attributeOffset">The offset (within the attribute / index entry) where the operation should be carried out.</param>
            public void Log(NTFSAttribute attribute, long blockNumber, ushort blockOffset, ushort attributeOffset)
            {
                var startSector = blockNumber / attribute.SectorsPerBlock;
                var sectorCount = attribute.SectorsPerBlock;

                var sectorsPerCluster = Transaction.Client.LogFile.Volume.sectorsPerCluster;
                var vcn = startSector / sectorsPerCluster;
                startSector = startSector % sectorsPerCluster;

                var clusters = new Cluster[(startSector + attribute.SectorsPerBlock + sectorsPerCluster - 1) / sectorsPerCluster];
                for (int i = 0; i < clusters.Count(); i++)
                    clusters[i] = attribute.GetCluster(vcn++, true);

                var attrID = Transaction.GetAttributeID(attribute);
                Log(attrID, clusters, startSector, attribute.SectorsPerBlock, blockOffset, attributeOffset);
            }


            public void Log(int attrID, Cluster[] clusters, long startSector, int sectorsPerBlock, ushort blockOffset, ushort attributeOffset)
            {
                LogRecord = new LogRecord(Transaction.Client, (ushort)attrID, clusters, (ushort)startSector, (ushort)sectorsPerBlock, blockOffset, attributeOffset, this);
                LogRecord.Setup();
                Transaction.Log(this);
            }


            /// <summary>
            /// Shall generate the reverse operation of the operation.
            /// This will never be called after Execute.
            /// The arguments passed are always equvalent to the arguments for Execute.
            /// </summary>
            public virtual LogOperation GetUndoOperation(Cluster[] clusters, long offset)
            {
                return null;
            }

            /// <summary>
            /// Executes the operation on the specified clusters.
            /// The changes can only performed in cache (except for the commit operation) since at the time of execution,
            /// the operation isn't yet logged on disk. An exception is the commit operation.
            /// </summary>
            public abstract void Execute(Cluster[] clusters, long offset, bool dryrun);

            /// <summary>
            /// Shall generate the data for this operation.
            /// For redo operations, this will never be called before Execute.
            /// For undo operations, this guarantee does not hold.
            /// </summary>
            public virtual byte[] ToBuffer()
            {
                return null;
            }
        }


        /// <summary>
        /// Represents an unknown operation found in an existing logfile.
        /// </summary>
        public class UnsupportedOperation : LogOperation
        {
            public override LogOperationType Type { get; }

            readonly byte[] data;

            public UnsupportedOperation(Transaction t, byte[] buffer, LogOperationType type)
                : base(t)
            {
                Type = type;
                data = buffer;
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                if (!dryrun)
                    throw new NotSupportedException(string.Format("unknown operation ({0:X2})", Type.ToInt()));
            }

            public override byte[] ToBuffer()
            {
                return data;
            }

            public override string ToString()
            {
                return string.Format("unknown operation ({0:X2}): data({1:X2}): {2}{3}",
                        Type.ToInt(),
                        data.Length,
                        string.Join(" ", data.Take(0x50).Select(b => string.Format("{0:X2}", b))),
                        data.Length > 0x50 ? " ..." : "");
            }
        }


        /// <summary>
        /// Represents a dummy operation that exists only to hold data for the associated other operation.
        /// </summary>
        public class NoOperation : LogOperation
        {
            public override LogOperationType Type { get { return LogOperationType.NoOp; } }

            readonly byte[] data;

            public NoOperation(Transaction t, byte[] buffer)
                : base(t)
            {
                data = buffer;
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                // no operation
            }

            public override byte[] ToBuffer()
            {
                return data;
            }

            public override string ToString()
            {
                return "[associated data]";
            }
        }


        /// <summary>
        /// Initializes or invalidates a file record.
        /// Corresponds to the 0x02 and 0x03 operations.
        /// </summary>
        public class UpdateFileRecordOperation : LogOperation
        {
            public override LogOperationType Type { get { return inUse ? LogOperationType.InitFileRecord : LogOperationType.InvalidateFileRecord; } }

            readonly bool inUse;
            readonly byte[] data;

            public UpdateFileRecordOperation(Transaction t, byte[] buffer, bool inUse)
                : base(t)
            {
                data = buffer;
                this.inUse = inUse;
            }

            public override LogOperation GetUndoOperation(Cluster[] clusters, long offset)
            {
                var data = Cluster.ReadBytes(clusters, offset, inUse ? 0x8 : 0x18);
                return new UpdateFileRecordOperation(Transaction, data, !inUse);
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                if (dryrun)
                    return;

                Cluster.WriteBytes(clusters, offset, data);

                // modify in-use flag
                var flags = Cluster.ReadBytes(clusters, offset + 0x16, 2).ReadInt16(0, Endianness.LittleEndian);
                if (inUse)
                    flags |= 1; // this may not be required
                else
                    flags &= (Int16)~1;
                var flagsBuffer = new byte[2];
                flagsBuffer.WriteVal(0, flags, Endianness.LittleEndian);
                Cluster.WriteBytes(clusters, offset + 0x16, flagsBuffer);
            }

            public override byte[] ToBuffer()
            {
                return data;
            }

            public override string ToString()
            {
                return string.Format("{0} file record {1:X8}",
                    inUse ? "init" : "invalidate",
                    (LogRecord.targetVCN * LogRecord.Volume.sectorsPerCluster + LogRecord.targetSector) * LogRecord.Volume.bytesPerSector / LogRecord.Volume.bytesPerMFTRecord);
            }
        }


        /// <summary>
        /// Corresponds to the 0x07, 0x08, 0x13 and 0x14 operations
        /// </summary>
        public class UpdateValueOperation : LogOperation
        {
            public override LogOperationType Type { get; }

            readonly byte[] data;
            readonly bool resident;
            readonly bool index;

            public UpdateValueOperation(Transaction t, byte[] buffer, bool resident, bool index)
                : base(t)
            {
                data = buffer;
                this.resident = resident;
                this.index = index;

                if (resident && index)
                    Type = LogOperationType.UpdateResidentIndexEntry;
                else if (resident && !index)
                    Type = LogOperationType.UpdateResidentAttribute;
                else if (!resident && index)
                    Type = LogOperationType.UpdateNonResidentIndexEntry;
                else
                    Type = LogOperationType.UpdateNonResidentAttribute;
            }

            private long GetValueOffset(Cluster[] clusters, long offset)
            {
                if (resident) // if resident, skip attribute header
                    offset += Cluster.ReadBytes(clusters, offset + 0x14, 2).ReadUInt16(0, Endianness.LittleEndian);
                else if (index) // if non-resident index entry, we skip the index buffer header
                    offset += 0x18;
                return offset + LogRecord.attributeOffset;
            }

            public override LogOperation GetUndoOperation(Cluster[] clusters, long offset)
            {
                var clusterOffset = GetValueOffset(clusters, offset);
                var undoData = Cluster.ReadBytes(clusters, clusterOffset, data.Length);
                return new UpdateValueOperation(Transaction, undoData, resident, index);
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                if (dryrun)
                    return;

                var clusterOffset = GetValueOffset(clusters, offset);
                Cluster.WriteBytes(clusters, clusterOffset, data);
            }

            public override byte[] ToBuffer()
            {
                return data;
            }

            public override string ToString()
            {
                try {
                    return string.Format("update {0} {1} in {2}",
                        resident ? "resident" : "non-resident",
                        index ? "index entry" : "value",
                        (resident ? LogRecord.GetActualReferencedAttribute() : LogRecord.GetReferencedAttribute()).ToString());
                } catch {
                    return string.Format("update resident value (attribute not found at specified location - maybe it was moved)");
                }
            }
        }


        /// <summary>
        /// Inserts or deletes an attribute or index entry from a buffer (file record, index root or index allocation)
        /// and shifts the remaining bytes of the block.
        /// Corresponds to the operations 0x05, 0x06, 0x0C, 0x0D, 0x0E and 0x0F.
        /// </summary>
        public class CreateOrDeleteValueOperation : LogOperation
        {
            public override LogOperationType Type
            {
                get
                {
                    if (data != null)
                        return index ? resident ? LogOperationType.CreateResidentIndexEntry : LogOperationType.CreateNonResidentIndexEntry : LogOperationType.CreateAttribute;
                    else
                        return index ? resident ? LogOperationType.DeleteResidentIndexEntry : LogOperationType.DeleteNonResidentIndexEntry : LogOperationType.DeleteAttribute;
                }
            }

            readonly byte[] data; // null for delete
            readonly bool index;
            readonly bool resident;

            public CreateOrDeleteValueOperation(Transaction t, byte[] buffer, bool index, bool resident)
                : base(t)
            {
                data = buffer;
                this.index = index;
                this.resident = resident;
            }

            private long GetValueLength(Cluster[] clusters, long offset)
            {
                if (index) // read length of index entry
                    return Cluster.ReadBytes(clusters, offset + 8, 2).ReadUInt16(0, Endianness.LittleEndian);
                else // read length of attribute
                    return Cluster.ReadBytes(clusters, offset + 4, 4).ReadUInt32(0, Endianness.LittleEndian);
            }

            public override LogOperation GetUndoOperation(Cluster[] clusters, long offset)
            {
                if (data != null)
                    return new CreateOrDeleteValueOperation(Transaction, null, index, resident);
                else
                    return new CreateOrDeleteValueOperation(Transaction, Cluster.ReadBytes(clusters, offset, GetValueLength(clusters, offset)), index, resident);
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                if (dryrun)
                    return;

                long oldLength = data != null ? 0 : GetValueLength(clusters, offset + LogRecord.attributeOffset);
                long newLength = data != null ? data.Count() : 0;

                var remaining = LogRecord.GetRemainingBytes(offset + Math.Max(oldLength, newLength));
                var remainder = Cluster.ReadBytes(clusters, offset + LogRecord.attributeOffset + oldLength, remaining);
                Cluster.WriteBytes(clusters, offset + LogRecord.attributeOffset + newLength, remainder);

                if (data != null)
                    Cluster.WriteBytes(clusters, offset + LogRecord.attributeOffset, data);

                // if this was in the index root, update attribute length
                if (index && resident) {
                    var newLengthBytes = new byte[4];
                    var oldAttrLength = Cluster.ReadBytes(clusters, offset + 4, 4).ReadInt32(0, Endianness.LittleEndian);
                    newLengthBytes.WriteVal(0, oldAttrLength + (newLength - oldLength), Endianness.LittleEndian);
                    Cluster.WriteBytes(clusters, offset + 4, newLengthBytes);
                }
            }

            public override byte[] ToBuffer()
            {
                return data;
            }

            public override string ToString()
            {
                return string.Format("{0} {1}",
                    data != null ? "create" : "delete",
                    index ? resident ? "index entry in index root" : "index entry in " + LogRecord.GetReferencedAttribute().ToString() : "attribute"); ;
            }
        }


        /// <summary>
        /// Updates the allocated, real and initialized size fields of a non-resident attribute header.
        /// This does not alter the runlist or (de)allocate anything.
        /// </summary>
        public class UpdateAttributeSizeValue : LogOperation
        {
            public override LogOperationType Type { get { return LogOperationType.UpdateAttributeSize; } }

            readonly NTFSAttribute attribute; // only valid if operation was created from scratch
            readonly long allocatedSize;
            readonly long realSize;
            readonly long initializedSize;

            public UpdateAttributeSizeValue(Transaction t, NTFSAttribute attribute, long allocatedSize, long realSize, long initializedSize)
                : base(t)
            {
                this.attribute = attribute;
                this.allocatedSize = allocatedSize;
                this.realSize = realSize;
                this.initializedSize = initializedSize;
            }

            public UpdateAttributeSizeValue(Transaction t, byte[] buffer)
                : base(t)
            {
                if (buffer.Count() != 0x18)
                    throw new Exception("data of update-attribute-size operation must have 24 bytes (have " + buffer.Count() + ")");

                allocatedSize = buffer.ReadInt64(0x00, Endianness.LittleEndian);
                realSize = buffer.ReadInt64(0x08, Endianness.LittleEndian);
                initializedSize = buffer.ReadInt64(0x10, Endianness.LittleEndian);
            }

            public override LogOperation GetUndoOperation(Cluster[] clusters, long offset)
            {
                return new UpdateAttributeSizeValue(Transaction, attribute, attribute.nonResidentHeader.allocatedSize, attribute.nonResidentHeader.realSize, attribute.nonResidentHeader.initializedSize);
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                if (!dryrun)
                    Cluster.WriteBytes(clusters, 0x28, ToBuffer()); // the size fields start at offset 0x28 relative to the attribute header
            }

            public override byte[] ToBuffer()
            {
                var buffer = new byte[24];
                buffer.WriteVal(0x00, allocatedSize, Endianness.LittleEndian);
                buffer.WriteVal(0x08, realSize, Endianness.LittleEndian);
                buffer.WriteVal(0x10, initializedSize, Endianness.LittleEndian);
                return base.ToBuffer();
            }

            public override string ToString()
            {
                string attrName;
                try {
                    attrName = LogRecord.GetActualReferencedAttribute().ToString();
                } catch {
                    attrName = "[couldn't load attribute]";
                }
                return string.Format("change sizes of {0}: allocated: {1:X8}, actual: {2:X8}, initialized: {3:X8}", attrName, allocatedSize, realSize, initializedSize);
            }
        }

        /// <summary>
        /// Updates the runlist of a non-resident attribute.
        /// If the length of the runlist changes (such that the attribute length changes),
        /// the remaining bytes of the file record are shifted.
        /// Corresponds to the 0x09 operation.
        /// </summary>
        public class UpdateRunlistOperation : LogOperation
        {
            public override LogOperationType Type { get { return LogOperationType.UpdateRunlist; } }

            readonly byte[] data;
            List<Tuple<long, long>> runlist;

            public UpdateRunlistOperation(Transaction t, List<Tuple<long, long>> newRunlist)
                : base(t)
            {
                runlist = newRunlist;
            }

            public UpdateRunlistOperation(Transaction t, byte[] buffer)
                : base(t)
            {
                data = buffer;
            }

            public override LogOperation GetUndoOperation(Cluster[] clusters, long offset)
            {
                var attrLength = Cluster.ReadBytes(clusters, offset + 4, 4).ReadUInt32(0, Endianness.LittleEndian);
                var oldData = Cluster.ReadBytes(clusters, offset + LogRecord.attributeOffset, attrLength - LogRecord.attributeOffset);
                return new UpdateRunlistOperation(Transaction, oldData);
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                var runlistOffsetInAttribute = Cluster.ReadBytes(clusters, offset + 0x20, 2).ReadInt16(0, Endianness.LittleEndian);

                // this is a bit complicated but the logfile sometimes contains a dirty runlist (that is not padded with 0s)
                // the MS NTFS driver seems to fill in these 0s, so we do the same, but for that we have to parse the full runlist
                if (runlist == null) {
                    var dataOffsetInRunlist = LogRecord.attributeOffset - runlistOffsetInAttribute;
                    var newData = new byte[dataOffsetInRunlist + this.data.Length];
                    Array.Copy(Cluster.ReadBytes(clusters, offset + runlistOffsetInAttribute, dataOffsetInRunlist), 0, newData, 0, dataOffsetInRunlist);
                    Array.Copy(this.data, 0, newData, dataOffsetInRunlist, this.data.Length);
                    runlist = NTFSAttribute.NonResidentHeader.ReadRunlist(newData);
                }

                // this pads to multiple of 8 bytes
                var data = NTFSAttribute.NonResidentHeader.WriteRunlist(runlist);

                if (dryrun)
                    return;

                var attrLength = Cluster.ReadBytes(clusters, offset + 4, 4).ReadUInt32(0, Endianness.LittleEndian);
                long oldLength = attrLength;
                long newLength = runlistOffsetInAttribute + data.Length;

                // shift the rest
                var remaining = LogRecord.GetRemainingBytes(offset + Math.Max(oldLength, newLength));
                var remainder = Cluster.ReadBytes(clusters, offset + oldLength, remaining);
                Cluster.WriteBytes(clusters, offset + newLength, remainder);

                // write new data
                Cluster.WriteBytes(clusters, offset + runlistOffsetInAttribute, data);

                // update attribute length
                var newLengthBytes = new byte[4];
                newLengthBytes.WriteVal(0, (Int32)newLength, Endianness.LittleEndian);
                Cluster.WriteBytes(clusters, offset + 4, newLengthBytes);
            }

            public override byte[] ToBuffer()
            {
                if (runlist == null)
                    return data;
                else
                    return NTFSAttribute.NonResidentHeader.WriteRunlist(runlist);
            }

            public override string ToString()
            {
                return string.Format("update runlist of {0}", LogRecord.GetActualReferencedAttribute().ToString());
            }
        }


        /// <summary>
        /// Corresponds to the 0x15/0x16 operations
        /// </summary>
        public class UpdateBitmapOperation : LogOperation
        {
            public override LogOperationType Type { get { return value ? LogOperationType.AllocateInBitmap : LogOperationType.DeallocateInBitmap; } }

            readonly long index;
            readonly long count;
            readonly bool value;

            public UpdateBitmapOperation(Transaction t, long index, long count, bool value)
                : base(t)
            {
                this.index = index;
                this.count = count;
                this.value = value;
            }

            public UpdateBitmapOperation(Transaction t, byte[] buffer, bool value)
                : base(t)
            {
                if (buffer.Count() != 8)
                    throw new Exception("data of write-to-bitmap operation must have 8 bytes (have " + buffer.Count() + ")");

                index = buffer.ReadUInt32(0, Endianness.LittleEndian);
                count = buffer.ReadUInt32(4, Endianness.LittleEndian);
                this.value = value;
            }

            public override LogOperation GetUndoOperation(Cluster[] clusters, long offset)
            {
                return new UpdateBitmapOperation(Transaction, index, count, !value);
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                offset += count / 8;
                var cluster = offset / LogRecord.Volume.bytesPerCluster;
                offset = offset % LogRecord.Volume.bytesPerCluster;
                var mask = (1 << (byte)(count % 8));
                if (value)
                    clusters[cluster].data[offset] |= (byte)mask;
                else
                    clusters[cluster].data[offset] &= (byte)~mask;
            }

            public override byte[] ToBuffer()
            {
                // seems strange, but if this actually works like written here, the logfile limits a bitmap to 4 billion bits
                var buffer = new byte[8];
                buffer.WriteVal(0, (UInt32)index);
                buffer.WriteVal(4, (UInt32)count);
                return base.ToBuffer();
            }

            public override string ToString()
            {
                return string.Format("{0} {1} {2:X8} in {3}",
                    value ? "allocate" : "free",
                    count == 1 ? "bit" : count + "bits starting at",
                    index,
                    LogRecord.GetReferencedAttribute().ToString()
                    );
            }
        }


        /// <summary>
        /// Corresponds to the 0x1C operation
        /// </summary>
        public class CommitOperation : LogOperation
        {
            public override LogOperationType Type { get { return redo ? LogOperationType.Commit : LogOperationType.Compensation; } }

            readonly bool redo;

            public CommitOperation(Transaction t, bool redo)
                : base(t)
            {
                this.redo = redo;
            }

            public override LogOperation GetUndoOperation(Cluster[] clusters, long offset)
            {
                return new CommitOperation(Transaction, !redo);
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                Transaction.Client.TransactionList.Remove(Transaction);
            }

            public override string ToString()
            {
                return redo ? "commit transaction" : "compensation";
            }
        }


        /// <summary>
        /// Corresponds to the 0x1C operation
        /// </summary>
        public class LoadAttributeOperation : LogOperation
        {
            public override LogOperationType Type { get { return LogOperationType.LoadNonResidentAttribute; } }

            readonly AttributeList.ListItem attr;
            readonly string name;

            public LoadAttributeOperation(Transaction t, byte[] buffer, byte[] secondaryBuffer)
                : base(t)
            {
                attr = buffer.ReadObject<AttributeList.ListItem>(0);
                name = secondaryBuffer.ReadString(0, secondaryBuffer.Length / 2, StringFormat.Unicode, Endianness.LittleEndian);
            }

            public LoadAttributeOperation(Transaction t, NTFSAttribute attribute, long currentLSN)
                : base(t)
            {
                attr = new AttributeList.ListItem(
                    attribute.File.FileReference,
                    attribute.type,
                    attribute.type == NTFSAttributeType.IndexAllocation ? attribute.SectorsPerBlock : 0,
                    currentLSN);
                name = attribute.name;
            }

            public override LogOperation GetUndoOperation(Cluster[] clusters, long offset)
            {
                var buffer = new byte[name.Length];
                buffer.WriteVal(0, name, StringFormat.Unicode, Endianness.LittleEndian);
                return new NoOperation(Transaction, buffer);
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                LogRecord.targetAttribute = (UInt16)Transaction.Client.AttributeList.Add(LogRecord.targetAttribute, attr);
                if (name != "")
                    Transaction.Client.AttributeNameList.Add(LogRecord.targetAttribute, name);
                if (!dryrun)
                    LogRecord.GetReferencedAttribute().LogFileAttributeID = LogRecord.targetAttribute;
            }

            public override byte[] ToBuffer()
            {
                var buffer = new byte[Transaction.Client.AttributeList.ElementSize];
                buffer.WriteVal(0, attr);
                return buffer;
            }

            public override string ToString()
            {
                var file = LogRecord.Volume.MFT.GetFile(attr.fileRef, null);
                return string.Format("load attribute ID {0:X4}: {1} {2} of {3}",
                    LogRecord.targetAttribute,
                    name == "" ? "unnamed" : name,
                    attr.type.ToEnumName(),
                    file.GetName());
            }
        }


        /// <summary>
        /// An item, as found in an AttributeList.
        /// </summary>
        public interface IDumpableListItem
        {
            Int32 NextElement { get; }
            long LSN { get; }
        }

        /// <summary>
        /// This list is found as the payload data in 1D and 1F log records.
        /// The two list types hold slightly different element types, but they are organized in the same way.
        /// It is created on demand (e.g. on the first 1C operation after a commit), and seems to grow by a factor of 4 if it overflows.
        /// Used items start with FFFFFFFF while unused items start with the address of the next item (the last one with 0).
        /// </summary>
        public abstract class DumpableList<T> : LogOperation where T : IDumpableListItem
        {
            /// <summary>
            /// The byte length of a single element in the list
            /// </summary>
            protected UInt16 elementLength;

            /// <summary>
            /// The total number of items in the list.
            /// </summary>
            [FieldSpecs(LengthOf = "elements")]
            UInt16 totalLength;

            /// <summary>
            /// The number of items in the list that are in use.
            /// </summary>
            UInt16 inUse;

            /// <summary>
            /// only 0 observed
            /// </summary>
            UInt16 unknown1;

            /// <summary>
            /// only 0 observed
            /// </summary>
            UInt32 unknown2;

            /// <summary>
            /// Probably specifies the number that is used to mark used items in the list (only FFFFFFFF was observed)
            /// </summary>
            Int32 inUseNumber;

            /// <summary>
            /// Offset (relative to attribute list start) of the first free item (0 if full)
            /// </summary>
            Int32 nextFreeItem;

            /// <summary>
            /// Offset (relative to attribute list start) of the last free item (0 if full)
            /// </summary>
            Int32 lastFreeItem;

            /// <summary>
            /// The list of elements
            /// </summary>
            [FieldSpecs(ElementSize = "elementLength")]
            T[] elements;

            /// <summary>
            /// The logfile that contains this list
            /// </summary>
            [FieldSpecs(Ignore = true)]
            protected Client client;

            /// <summary>
            /// A constructor for empty elements of type T
            /// </summary>
            [FieldSpecs(Ignore = true)]
            Func<int, T> emptyElementConstructor;

            public int ElementSize { get { return elementLength; } }

            /// <summary>
            /// Creates a new empty attribute list
            /// </summary>
            public DumpableList(Client client, Func<int, T> emptyElementConstructor, int initialSize)
                : base(null)
            {
                this.client = client;
                this.emptyElementConstructor = emptyElementConstructor;
                inUse = 0;
                elements = new T[initialSize];
                elementLength = 0x28;
                nextFreeItem = 0x18;
                lastFreeItem = (initialSize - 1) * elementLength + 0x18;
                inUseNumber = -1;
                FillUnusedElements();
            }

            /// <summary>
            /// Reads an attribute list from a buffer
            /// </summary>
            public DumpableList(Transaction t, Func<int, T> emptyElementConstructor, byte[] buffer)
                : base(t)
            {
                client = t.Client;
                this.emptyElementConstructor = emptyElementConstructor;
                long offset = 0;
                buffer.ReadObject(ref offset, this, Endianness.LittleEndian);
            }

            private void FillUnusedElements()
            {
                for (int i = nextFreeItem; i < lastFreeItem; i += elementLength)
                    elements[(i - 0x18) / elementLength] = emptyElementConstructor(i + elementLength);
                elements[(lastFreeItem - 0x18) / elementLength] = emptyElementConstructor(0);
            }

            /// <summary>
            /// Doubles the allocated size of the list
            /// </summary>
            private void Grow()
            {
                nextFreeItem = elements.Count() * elementLength + 0x18;
                lastFreeItem = (elements.Count() * 2 - 1) * elementLength + 0x18;
                Array.Resize(ref elements, elements.Count() * 2);
                FillUnusedElements();
            }

            /// <summary>
            /// Tries to get the element for the specified index.
            /// Returns true if the lookup was successful.
            /// </summary>
            protected bool TryGet(int index, out T result)
            {
                result = elements[(index - 0x18) / elementLength];
                return result.NextElement == inUseNumber;
            }

            /// <summary>
            /// Returns the element for the specified index.
            /// </summary>
            protected T Get(int index)
            {
                T result;
                if (!TryGet(index, out result))
                    throw new Exception("unknown attribute");
                return result;
            }

            /// <summary>
            /// Returns all valid elements in the list.
            /// </summary>
            protected IEnumerable<T> GetAll()
            {
                return (from element in elements where element.NextElement == inUseNumber select element);
            }

            private int UpdatePreviousFreeItem(int index, int newNextFreeItem, bool returnOldValue)
            {
                if (index == nextFreeItem) {
                    // this is the first item - update first item field
                    var oldVal = nextFreeItem;
                    nextFreeItem = newNextFreeItem;
                    return returnOldValue ? oldVal : 0;
                } else {
                    // walk list to find the previous item
                    var previousFreeItem = 0;
                    while (elements[previousFreeItem++].NextElement != index) ;

                    // update the item we found
                    var oldVal = elements[previousFreeItem].NextElement;
                    elements[previousFreeItem] = emptyElementConstructor(newNextFreeItem);
                    return returnOldValue ? oldVal : previousFreeItem * elementLength + 0x18;
                }
            }

            /// <summary>
            /// Puts the specified attribute-usage-element into the list.
            /// Returns the index at which the item was inserted.
            /// </summary>
            /// <param name="index">This can be used to specify the index in the list. If 0, the index is selected automatically.</param>
            public int Add(int index, T item)
            {
                // select index automatically if neccessary, then grow list if neccessary
                if (index == 0)
                    index = (nextFreeItem == 0 ? elements.Count() * elementLength + 0x18 : nextFreeItem);

                var i = (index - 0x18) / elementLength;
                if (elements[i].NextElement == inUseNumber)
                    throw new Exception("the element is already in use");

                while (i >= elements.Count())
                    Grow();

                // update linked list
                var previousFreeItem = UpdatePreviousFreeItem(index, elements[i].NextElement, false);
                if (index == lastFreeItem)
                    lastFreeItem = previousFreeItem;

                // insert new element
                elements[i] = item;
                inUse++;
                return index;
            }

            /// <summary>
            /// Removes the item at the specified position.
            /// This does not check if the item exists.
            /// </summary>
            protected void Remove(int index)
            {
                //elements[(index - 0x18) / elementLength] = emptyElementConstructor(UpdatePreviousFreeItem(index, index, true)); // this is actually stupid

                elements[(index - 0x18) / elementLength] = emptyElementConstructor(nextFreeItem);
                nextFreeItem = index;
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                // this is a dump - not a real operation
            }

            public override byte[] ToBuffer()
            {
                var buffer = new byte[0x18 + elements.Count() * elementLength];
                buffer.WriteVal(0, this);
                return buffer;
            }

            /// <summary>
            /// Shall return a human readable version of a single list element for debugging
            /// </summary>
            protected abstract string ToString(T attr, int attrID);

            /// <summary>
            /// Returns a human readable version of the list for debugging
            /// </summary>
            public override string ToString()
            {
                return string.Format("length: {0:X4}, in use: {1:X4}, next free: {2:X8}, last free: {3:X8}", elements.Count(), inUse, nextFreeItem, lastFreeItem) +
                    string.Join("", elements.Where(e => e.NextElement == inUseNumber)
                    .Select((e, i) => {
                        var attrID = i * elementLength + 0x18;
                        return string.Format("\r\n    index {0:X4} ({1:X8}): {2}",
                            attrID,
                            e.LSN,
                            ToString(e, attrID));
                    }));
            }
        }


        /// <summary>
        /// This list is found as the payload data in 1D log records.
        /// It holds a list of attributes that are currently opened for write access.
        /// When a checkpoint is inserted in the logfile, this table is dumped to a log record,
        /// so the recovery process can initialize it again from the dump.
        /// </summary>
        [TypeSpecs(WalkBaseType = true)]
        public class AttributeList : DumpableList<AttributeList.ListItem>
        {
            public override LogOperationType Type { get { return LogOperationType.AttributeListDump; } }

            [Endianness(Endianness.LittleEndian)]
            public class ListItem : IDumpableListItem
            {
                public Int32 NextElement { get { return nextElement; } }
                public long LSN { get { return lastCleanLSN; } }

                /// <summary>
                /// always FFFFFFFF for used elements, address of next unused element in unused elements (0 if no other unused elements)
                /// </summary>
                Int32 nextElement;

                /// <summary>
                /// Block size of the index allocation?
                /// 0 otherwise?
                /// </summary>
                public Int32 blocksize;

                /// <summary>
                /// the attribute type
                /// </summary>
                public NTFSAttributeType type;

                /// <summary>
                /// Only bit 0 was observed with a value other than 0.
                /// Bit 0 tells us if in the related dirty-cluster-list there is any cluster belonging to this attribute.
                /// </summary>
                public Int32 flags;

                /// <summary>
                /// MFT file reference (if the update sequence number is 1, this may mean ignore)
                /// </summary>
                public Int64 fileRef;

                /// <summary>
                /// the last LSN before the attribute was opened
                /// </summary>
                public Int64 lastCleanLSN;

                /// <summary>
                /// Creates a new element that is in use.
                /// </summary>
                public ListItem(long fileRef, NTFSAttributeType type, int blocksize, long lastCleanLSN)
                {
                    nextElement = -1;
                    this.fileRef = fileRef;
                    this.type = type;
                    this.lastCleanLSN = lastCleanLSN;
                    this.blocksize = blocksize;
                    flags = 0;
                }

                public ListItem(int nextElement)
                {
                    this.nextElement = nextElement;
                }

                private ListItem()
                {
                    // for ByteConverter use only
                }
            }

            /// <summary>
            /// Reads an attribute list from a buffer
            /// </summary>
            public AttributeList(Transaction t, byte[] buffer)
                : base(t, i => new ListItem(i), buffer)
            {
            }

            /// <summary>
            /// Creates a new empty attribute list
            /// </summary>
            public AttributeList(Client client)
                : base(client, i => new ListItem(i), 0x8)
            {
            }

            /// <summary>
            /// Returns the attribute-usage-element for the specified attribute number (as used in log records)
            /// </summary>
            public new NTFSAttribute Get(int index)
            {
                var result = base.Get(index);
                var file = client.LogFile.Volume.MFT.GetFile(result.fileRef, null);
                var attrName = client.AttributeNameList.Get(index);
                return file.FileRecord.GetAttributes(result.type, attrName).SingleOrDefault();
            }

            public void ChangeDirtyState(int attrID, bool dirty)
            {
                if (dirty)
                    base.Get(attrID).flags |= 1;
                else
                    base.Get(attrID).flags &= ~1;
            }

            protected override string ToString(ListItem attr, int index)
            {
                var attrName = client.AttributeNameList.Get(index) ?? "[??]";
                return string.Format("blocksize: {3:X8}, dirty {4:X1}, {0} {1} in {2}",
                    attrName == "" ? "unnamed" : attrName,
                    attr.type.ToEnumName(),
                    client.LogFile.Volume.MFT.GetFile(attr.fileRef, null).GetName(),
                    attr.blocksize,
                    attr.flags
                    );
            }

            public override string ToString()
            {
                var result = "attribute list: " + base.ToString();
                if (!ReferenceEquals(this, client.AttributeList))
                    result = string.Format("\r\nactual version:    {0}\r\ngenerated version: {1}", result, client.AttributeList.ToString());
                return result;
            }
        }


        /// <summary>
        /// This list is found as the payload data in 1D log records.
        /// It holds a list of attributes that were loaded at the point of occurrence.
        /// This is useful for rolling back changes.
        /// </summary>
        [TypeSpecs(WalkBaseType = true)]
        public class DirtyClusterList : DumpableList<DirtyClusterList.ListItem>
        {
            public override LogOperationType Type { get { return LogOperationType.DirtyClusterListDump; } }

            [Endianness(Endianness.LittleEndian)]
            public class ListItem : IDumpableListItem
            {
                public Int32 NextElement { get { return nextElement; } }
                public long LSN { get { return firstUseLSN; } }

                /// <summary>
                /// always FFFFFFFF for used elements, address of next unused element in unused elements (0 if no other unused elements)
                /// </summary>
                Int32 nextElement;

                /// <summary>
                /// The non-resident attribute ID to which the cluster belongs
                /// </summary>
                public Int32 relatedAttributeID;

                /// <summary>
                /// only 0x1000 observed
                /// </summary>
                public Int32 whatever;

                /// <summary>
                /// only 0x01 observed
                /// </summary>
                public Int32 whoknows;

                /// <summary>
                /// VCN of the used cluster
                /// </summary>
                public Int64 VCN;

                /// <summary>
                /// LSN of the first log record that uses this cluster
                /// </summary>
                public Int64 firstUseLSN;

                /// <summary>
                /// LCN of the used cluster
                /// </summary>
                public Int64 LCN;

                /// <summary>
                /// Creates a new element that is in use.
                /// </summary>
                public ListItem(ushort attrID, long vcn, long lcn, long lsn)
                {
                    nextElement = -1;
                    whatever = 0x00001000;
                    whoknows = 0x00000001;
                    relatedAttributeID = attrID;
                    VCN = vcn;
                    firstUseLSN = lsn;
                    LCN = lcn;
                }

                public ListItem(int nextElement)
                {
                    this.nextElement = nextElement;
                }

                private ListItem()
                {
                    // for ByteConverter use only
                }
            }

            /// <summary>
            /// Creates a new empty attribute list
            /// </summary>
            public DirtyClusterList(Client client)
                : base(client, i => new ListItem(i), 0x20)
            {
            }

            /// <summary>
            /// Reads an attribute list from a buffer
            /// </summary>
            public DirtyClusterList(Transaction t, byte[] buffer)
                : base(t, i => new ListItem(i), buffer)
            {
            }

            public new IEnumerable<Cluster> GetAll()
            {
                foreach (var item in base.GetAll())
                    yield return client.AttributeList.Get(item.relatedAttributeID).GetCluster(item.VCN, item.LCN, true);
            }

            /// <summary>
            /// Puts the specified dirty-cluster-element into the list, provided that it isn't already there.
            /// </summary>
            public void Add(long lsn, ushort attrID, Cluster cluster)
            {
                if (!cluster.dirty) {
                    base.Add(0, new ListItem(attrID, cluster.VCN, cluster.LCN, lsn));
                    client.AttributeList.ChangeDirtyState(attrID, true);
                    cluster.dirty = true;
                }
            }

            protected override string ToString(ListItem attr, int index)
            {
                return string.Format("attrID {0:X4}, whatever {1:X8}, whoknows {2:X8}, VCN {3:X16}, LCN {4:X16}",
                    attr.relatedAttributeID,
                    attr.whatever,
                    attr.whoknows,
                    attr.VCN,
                    attr.LCN
                    );
            }

            public override string ToString()
            {
                var result = "dirty cluster list: " + base.ToString();
                if (!ReferenceEquals(this, client.DirtyClusterList))
                    result = string.Format("\r\nactual version:    {0}\r\ngenerated version: {1}", result, client.DirtyClusterList.ToString());
                return result;
            }
        }


        /// <summary>
        /// This list is found as the payload data in 1D log records.
        /// It holds a list of attributes that were loaded at the point of occurrence.
        /// This is useful for rolling back changes.
        /// </summary>
        [TypeSpecs(WalkBaseType = true)]
        public class TransactionList : DumpableList<TransactionList.ListItem>
        {
            public override LogOperationType Type { get { return LogOperationType.TransactionListDump; } }

            [Endianness(Endianness.LittleEndian)]
            public class ListItem : IDumpableListItem
            {
                public Int32 NextElement { get { return nextElement; } }
                public long LSN { get { return someLSN; } }

                /// <summary>
                /// always FFFFFFFF for used elements, address of next unused element in unused elements (0 if no other unused elements)
                /// </summary>
                Int32 nextElement;

                /// <summary>
                /// LSN of the first log record that uses this cluster
                /// </summary>
                public Int64 someLSN;

                [FieldSpecs(Ignore = true)]
                readonly public Transaction transaction;

                public ListItem(long lsn, Transaction transaction)
                {
                    nextElement = -1;
                    someLSN = lsn;
                    this.transaction = transaction;
                }

                public ListItem(int nextElement)
                {
                    this.nextElement = nextElement;
                }

                private ListItem()
                {
                    // for ByteConverter use only
                }
            }

            /// <summary>
            /// Creates a new empty attribute list
            /// </summary>
            public TransactionList(Client client)
                : base(client, i => new ListItem(i), 0x20)
            {
            }

            /// <summary>
            /// Reads an attribute list from a buffer
            /// </summary>
            public TransactionList(Transaction t, byte[] buffer)
                : base(t, i => new ListItem(i), buffer)
            {
            }

            public void Add(Transaction transaction)
            {
                transaction.Index = Add(transaction.Index, new ListItem(0, transaction));
            }

            public new Transaction Get(int index)
            {
                ListItem result;
                if (!TryGet(index, out result))
                    Add(index, result = new ListItem(0, new Transaction(client, index)));
                return result.transaction;
            }

            public void Remove(Transaction transaction)
            {
                base.Remove(transaction.Index);
            }

            protected override string ToString(ListItem attr, int attrID)
            {
                return string.Format("not implemented");
            }

            public override string ToString()
            {
                var result = "transaction list: " + base.ToString();
                if (!ReferenceEquals(this, client.DirtyClusterList))
                    result = string.Format("\r\nactual version:    {0}\r\ngenerated version: {1}", result, client.DirtyClusterList.ToString());
                return result;
            }
        }


        /// <summary>
        /// Corresponds to the 0x1E operation.
        /// This list holds every attribute name (if any) that is referenced by the associated 1D record.
        /// Note that the list omits empty names (e.g. the name of the unnamed data stream).
        /// </summary>
        public class AttributeNameList : LogOperation
        {
            public override LogOperationType Type { get { return LogOperationType.AttributeNameListDump; } }

            readonly List<Tuple<int, string>> names = new List<Tuple<int, string>>();
            readonly Client client;

            /// <summary>
            /// Returns the name for the specified attribute.
            /// Returns an empty string if the attribute is not in this list.
            /// </summary>
            public string Get(int attrID)
            {
                return names.Where(n => n.Item1 == attrID).SingleOrDefault()?.Item2 ?? "";
            }

            /// <summary>
            /// Adds the specified name to the list, if it's not null or empty
            /// </summary>
            public void Add(int attrID, string name)
            {
                if (!string.IsNullOrEmpty(name))
                    names.Add(new Tuple<int, string>(attrID, name));
            }

            /// <summary>
            /// Creates a new empty attribute list
            /// </summary>
            public AttributeNameList(Client client)
                : base(null)
            {
                this.client = client;
            }

            /// <summary>
            /// Reads an attribute name list from a buffer
            /// </summary>
            public AttributeNameList(Transaction t, byte[] buffer)
                : base(t)
            {
                client = t.Client;

                long offset = 0;

                while (true) {
                    var num = buffer.ReadUInt32(ref offset, Endianness.LittleEndian);
                    if (num == 0)
                        break;
                    Add((int)(num & 0xFFFF), buffer.ReadString(ref offset, num >> 17, StringFormat.Unicode, Endianness.LittleEndian)); // string length is specified in bytes, therefore shift by 16+1
                    offset += 2;
                }
            }

            public override void Execute(Cluster[] clusters, long offset, bool dryrun)
            {
                // this is a dump - not a real operation
            }

            /// <summary>
            /// Serializes this list into a buffer
            /// </summary>
            public override byte[] ToBuffer()
            {
                var buffer = new byte[names.Select(n => n.Item2.Length).Sum() * 2 + 6 * names.Count() + 4];
                long offset = 0;

                foreach (var name in names) {
                    offset += buffer.WriteVal(offset, (uint)(name.Item1 + name.Item2.Length << 17), Endianness.LittleEndian);
                    offset += buffer.WriteVal(offset, name.Item2, StringFormat.Unicode, Endianness.LittleEndian);
                    offset += 2;
                }

                return buffer;
            }

            public override string ToString()
            {
                var result = "attribute name list:" + string.Join("", names.Select(n => string.Format("\r\n    attrID: {0:X4}, name: \"{1}\"", n.Item1, n.Item2)));
                if (!ReferenceEquals(this, client.AttributeNameList))
                    result = string.Format("\r\nactual version:    {0}\r\ngenerated version: {1}", result, client.AttributeNameList.ToString());
                return result;
            }
        }
    }
}

