using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem.NTFS
{
    /// <summary>
    /// The $LogFile logs every transaction on the filesystem.
    /// NTFS is a basically a database, so most operations on it are atomic transactions.
    /// The logfile provides information that allows recovery of incomplete transactions after a power failure.
    /// 
    /// LSN: Linear Sequence Number (this corresponds directly to the record's position in the logfile)
    /// 
    /// The logfile has a restart area that acts as a header (actually this header exists twice for redundancy).
    /// There may be multiple clients, each of which has his own linked lists of log records.
    /// In NTFS however, there is only a single client (which is the OS).
    /// </summary>
    partial class NTFSLogFile
    {
        const long I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST = 0x1000; // todo: how do we know that it's always 4kB, maybe we should check with different cluster sizes?

        /// <summary>
        /// This structure is found at the beginning of the first and second page of the logfile.
        /// </summary>
        [Endianness(Endianness.LittleEndian)]
        class RestartPageHeader
        {
            public FixupHeader fixup;
            public UInt32 systemPageSize;
            public UInt32 logPageSize;
            public UInt16 restartOffset;
            public Int16 minorVersion;
            public Int16 majorVersion;
        }

        /// <summary>
        /// This structure is found on the first and second page of the logfile, directly after the RestartPageHeader.
        /// </summary>
        [Endianness(Endianness.LittleEndian)]
        class RestartAreaHeader
        {
            public Int64 currentLSN;
            public UInt16 logClientCount;
            public Int16 clientFreeList;
            public Int16 clientInUseList;
            public UInt16 flags;
            public Int32 sequenceNumberBits;
            public UInt16 restartAreaLength;

            /// <summary>
            /// Ignored in versions prior to 1.1
            /// </summary>
            public UInt16 clientArrayOffset;

            /// <summary>
            /// "usable log file size. we will stop sharing the value in the page header" => ?
            /// </summary>
            public Int64 fileSize;

            /// <summary>
            /// DataLength of last LSN
            /// </summary>
            public Int32 lastLSNDataLength;

            /// <summary>
            /// Ignored in versions prior to 1.1
            /// record header length in a log page
            /// </summary>
            public UInt16 recordHeaderLength;

            /// <summary>
            /// Ignored in versions prior to 1.1
            /// data offset within a log page
            /// </summary>
            public UInt16 logPageDataOffset;

            /// <summary>
            /// LogFile open count. Used to determine if there has been a change to the disk. This does not increment like a simple counter, but rather random
            /// </summary>
            public UInt32 restartOpenLogCount;

            UInt32 reserved;
        }

        /// <summary>
        /// An array of these records is found on the first and second page of the logfile, directly after the restart area.
        /// In NTFS, there is actually only one client record.
        /// </summary>
        [Endianness(Endianness.LittleEndian)]
        class ClientRecord
        {
            /// <summary>
            /// The oldest LSN that this client requires to be in the log file
            /// </summary>
            public Int64 oldestLSN;

            /// <summary>
            /// The LSN of the latest client restart area written to the disk.
            /// A reserved LSN indicates that no restart record exists for this client
            /// </summary>
            public Int64 clientRestartLSN;

            /// <summary>
            /// Index of the previous client
            /// </summary>
            public Int16 prevClient;

            /// <summary>
            /// Index of the next client
            /// </summary>
            public Int16 nextClient;

            /// <summary>
            /// Sequence number of this record. Increment when this client record is reused - this will happen whenever a client opens the log file and has no current restart area.
            /// </summary>
            public UInt16 sequenceNumber;

            UInt16 reserved1;
            UInt32 reserved2;

            [FieldSpecs(LengthOf = "clientName")]
            UInt32 clientNameLength;

            /// <summary>
            /// Should always be "NTFS" and have length 8
            /// </summary>
            [FieldSpecs(StringFormat = StringFormat.Unicode)]
            public string clientName;

            [FieldSpecs(Ignore = true)]
            public Int16 index;

            [FieldSpecs(Ignore = true)]
            public Client client;
        }

        public struct ClientID
        {
            /// <summary>
            /// Sequence number of the client.
            /// </summary>
            public UInt16 sequenceNumber;

            /// <summary>
            /// The index of the client in the client array.
            /// </summary>
            public Int16 index;

            public static bool operator ==(ClientID x, ClientID y)
            {
                return x.sequenceNumber == y.sequenceNumber && x.index == y.index;
            }
            public static bool operator !=(ClientID x, ClientID y)
            {
                return !(x == y);
            }
            public override bool Equals(object obj)
            {
                if (!(obj is ClientID))
                    return false;
                return (ClientID)obj == this;
            }
            public override int GetHashCode()
            {
                return (index << 16 + sequenceNumber);
            }
        }

        /// <summary>
        /// This is the page header of every valid log record buffer.
        /// All invalid pages are filled with 0xFF.
        /// </summary>
        [Endianness(Endianness.LittleEndian)]
        class RecordPage
        {
            public FixupHeader fixup;

            /// <summary>
            /// values 1 and 3 have been obeserved
            /// </summary>
            public UInt32 flags;

            /// <summary>
            /// Number of pages in this page sequence.
            /// A page sequence is a bunch of consecutive pages that somehow belong together.
            /// It's not entirely clear what defines when multiple pages should be coalesced into a sequence.
            /// </summary>
            public UInt16 pageCount;

            /// <summary>
            /// Position of this page within the sequence (starting at 1!).
            /// </summary>
            public UInt16 pagePosition;

            /// <summary>
            /// Points to the location after the last record in this page (e.g. 0x0E60).
            /// If the last record perfectly fills the page, this is 0x1000.
            /// If the last record overflows to the next page, this is the address of the last record.
            /// </summary>
            public Int64 nextRecordOffset;

            /// <summary>
            /// The LSN of the last log record on this page
            /// </summary>
            public Int64 lastEndLSN;
        }


        /// <summary>
        /// Represents a client of the database.
        /// For NTFS, there is only one client.
        /// </summary>
        public class Client
        {
            public ClientID ID { get; }
            public NTFSLogFile LogFile { get; }

            public AttributeList AttributeList { get; set; }
            public AttributeNameList AttributeNameList { get; set; }
            public DirtyClusterList DirtyClusterList { get; set; }
            public TransactionList TransactionList { get; set; }

            public Client(NTFSLogFile logFile, ClientID id)
            {
                LogFile = logFile;
                ID = id;
                DirtyClusterList = new DirtyClusterList(this);
            }

            /// <summary>
            /// Returns the list of log entries that belong to this client, starting at the specified LSN.
            /// The function returns log entries until it encounters an invalid entry.
            /// </summary>
            private IEnumerable<LogFileEntry> GetEntries(long startLSN)
            {
                LogFileEntry entry;
                for (var lsn = startLSN; (entry = LogFileEntry.FromBuffer(this, lsn)) != null; lsn = entry.NextLSN)
                    if (entry.Header.clientID == ID)
                        yield return entry;
            }

            /// <summary>
            /// Starts a new transaction in the name of this client.
            /// The transaction must be disposed eventually.
            /// </summary>
            public Transaction StartTransaction()
            {
                return new Transaction(this, 0);
            }

            /// <summary>
            /// Does a redo pass by applying all logged redo operations that follow the specified restart LSN.
            /// Returns the number of the last LSN that was read. The returned LSN can be used as a starting point for the undo pass.
            /// </summary>
            /// <param name="restartLSN">This will typically be the client's last known restart LSN. For testing purposes, this can also be any other LSN that points to a restart entry.</param>
            /// <param name="debugFile">Null in normal operation</param>
            public long Redo(long restartLSN, string debugFile, TaskController controller)
            {
                StringBuilder log = new StringBuilder();

                long lastLSN = 0;
                foreach (var entry in GetEntries(restartLSN)) {
                    try {
                        entry.Redo(false);
                        log.AppendLine(string.Format("LSN {0:X16}: successful", entry.LSN));
                    } catch (Exception ex) {
                        if (debugFile == null)
                            throw;
                        log.AppendLine(string.Format("LSN {0:X16}: {1}", entry.LSN, ex.Message));
                    }
                    lastLSN = entry.LSN;
                }

                if (debugFile != null)
                    using (var file = System.IO.File.Open(debugFile, System.IO.FileMode.Create, System.IO.FileAccess.ReadWrite, System.IO.FileShare.Read))
                        file.Write(log.ToString(), controller).Wait();

                return lastLSN;
            }


            public void DumpRecords(byte[] rawFile, long firstLSN, string target, TaskController controller)
            {
                //AttributeNameList = new LogFileEntry.AttributeNameList(this);
                //AttributeList = new LogFileEntry.AttributeList(this);
                //DirtyClusterList = new LogFileEntry.DirtyClusterList(this);
                //TransactionList = new LogFileEntry.TransactionList(this);

                using (var log = System.IO.File.Open(target, System.IO.FileMode.Create, System.IO.FileAccess.ReadWrite, System.IO.FileShare.Read)) {
                    foreach (var entry in GetEntries(firstLSN)) {
                        if (entry.LSN == 0x005848C1)
                            Console.WriteLine("found lsn");
                        entry.Redo(true);
                        log.Write(string.Format("{0}\r\n", entry.ToString()), controller).Wait();
                    }
                }
            }
        }


        /// <summary>
        /// Represents a transaction.
        /// If the transaction is disposed without being committed, all changes done by this transaction will be rolled back.
        /// The public members of this class are NOT thread-safe, i.e. the log operations must be executed sequentially.
        /// However, multiple transactions are thread-safe with respect to each other.
        /// </summary>
        public class Transaction : IDisposable
        {
            bool disposed = false;

            public Client Client { get; }
            public int Index { get; set; }

            long previousLSN;
            long currentLSN;

            public Transaction(Client client, int index)
            {
                Client = client;
                Index = index;
            }

            /// <summary>
            /// 
            /// </summary>
            public int GetAttributeID(NTFSAttribute attribute)
            {
                if (attribute.LogFileAttributeID == 0) {
                    new LoadAttributeOperation(this, attribute, currentLSN).Log(0, null, 0, attribute.SectorsPerBlock, 0, 0);
                }

                return attribute.LogFileAttributeID;
            }

            /// <summary>
            /// Logs and executes the specified operation.
            /// This must not be used by the filesystem directly, but only by the log operation itself or the transaction.
            /// </summary>
            public void Log(LogOperation operation)
            {
                if (disposed)
                    throw new ObjectDisposedException("The transaction has already been committed or disposed.");

                bool preliminaryExecution = false;

                lock (Client.LogFile) {
                    operation.LogRecord.Header.thisLSN = currentLSN;
                    operation.LogRecord.Header.clientPreviousLSN = previousLSN;
                    if (!(operation is CommitOperation))
                        operation.LogRecord.Header.clientUndoNextLSN = previousLSN;

                    preliminaryExecution = operation is LoadAttributeOperation;
                    if (preliminaryExecution)
                        operation.LogRecord.Redo(false);

                    operation.LogRecord.Write();

                    previousLSN = currentLSN;
                    currentLSN = operation.LogRecord.Header.nextLSN;
                }

                if (!preliminaryExecution)
                    operation.LogRecord.Redo(false);
            }

            /// <summary>
            /// Commits the changes done by this transaction.
            /// After this function returns, the changes are guaranteed to be permanent (i.e. in the log file on disk).
            /// This applies even for virtual disks.
            /// </summary>
            public void Commit()
            {
                //Log(new CommitOperation(this, true));
                new CommitOperation(this, true).Log(0x18, null, 0, 0, 0, 0); // todo: verify that these defaults are correct
                                                                             // todo: flush log pages to disk
                disposed = true;
            }

            /// <summary>
            /// Rolls back all changes performed by this transaction.
            /// </summary>
            public void Dispose()
            {
                if (disposed)
                    return;

                throw new NotImplementedException();
            }
        }


        private IEnumerable<ClientRecord> GetClientRecords()
        {
            for (var i = RestartArea.clientInUseList; i >= 0 && i < ClientRecords.Count(); i = ClientRecords[i].nextClient) {
                ClientRecords[i].index = i;
                yield return ClientRecords[i];
            }
        }


        /// <summary>
        /// Returns the client with the specified name.
        /// This is not thread-safe.
        /// </summary>
        private Client GetClient(string name)
        {
            for (var i = 0; i >= 0 && i < ClientRecords.Count(); i = ClientRecords[i].nextClient)
                if (ClientRecords[i].clientName.TrimEnd('\0') == name)
                    return ClientRecords[i].client;

            // let's leave it at that - the NTFS client should be present anyway
            throw new Exception(string.Format("The client \"{0}\" was not found in the logfile. Currently I can't add new clients.", name));
        }

        /// <summary>
        /// Reads the specified page from the logfile and applies the update sequence.
        /// </summary>
        private byte[] ReadRecordBuffer(long offset)
        {
            // todo: choose the correct page (some may be corrupt)
            var buffer = File.Read(offset * I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST, I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST);
            Volume.ReadFixup(buffer, 0, I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST, 0x44524352);
            return buffer;
        }




        private void DumpPages(byte[] rawFile, long openCount, string target, TaskController controller)
        {
            using (var log = System.IO.File.Open(target, System.IO.FileMode.Create, System.IO.FileAccess.ReadWrite, System.IO.FileShare.Read)) {
                log.Write(string.Format("open count: {0:X8}\r\n", openCount), controller).Wait();

                for (var offset = 0; offset < rawFile.Length; offset += 0x1000) {
                    if (rawFile.ReadInt32(offset, Endianness.LittleEndian) != 0x44524352) {
                        var block = new byte[0x1000];
                        Array.Copy(rawFile, offset, block, 0, 0x1000);
                        if (!Array.TrueForAll(block, b => b == 0xFF))
                            log.Write(string.Format("page {0:X8} is not a page header but not all 0xFF\r\n", (offset >> 12)), controller).Wait();
                        continue;
                    }

                    Volume.ReadFixup(rawFile, offset, 0x1000, 0x44524352); // 'RCRD'
                    var checksum = rawFile.GetInt32Checksum(offset, 0x1000);
                    var pageHeader = rawFile.ReadObject<RecordPage>(offset);

                    var str = string.Format("page {0:X8} fixup {1:X4} LSN {2:X16} flags {3:X8} count {4:X4} pos {5:X4} last at {6:X16} is {7:X16}, 0x3C: {8:X8} checksum: {9:X16}\r\n",
                        (offset >> 12),
                        rawFile.ReadInt16(offset + 0x28, Endianness.LittleEndian),
                        pageHeader.fixup.logFileSequenceNumber,
                        pageHeader.flags,
                        pageHeader.pageCount,
                        pageHeader.pagePosition,
                        pageHeader.nextRecordOffset,
                        pageHeader.lastEndLSN,
                        rawFile.ReadInt32(offset + 0x3C, Endianness.LittleEndian),
                        checksum);
                    log.Write(str, controller).Wait();
                }
            }
        }

        public void Dump(string folder, TaskController controller)
        {
            var restartLsn = ClientRecords[0].clientRestartLSN;
            var firstLsn = restartLsn - (restartLsn % (RestartArea.fileSize >> 3)) + 0x4408;

            var rawFile = File.Read();
            DumpPages(rawFile, RestartArea.restartOpenLogCount, folder + @"\logpages.txt", controller);
            NTFSClient.DumpRecords(rawFile, firstLsn, folder + @"\logrecords.txt", controller);
        }


        public void Recover(string debugFile, bool veryDebug, TaskController controller)
        {
            foreach (var clientRecord in GetClientRecords()) {
                var client = clientRecord.client;

                // starting at the last checkpoint, we do the following:
                // 1. analysis pass: open files and pages used in the recovery process
                // 2. redo pass: apply the redo operation of all log records that follow the checkpoint - after this, the cache reflects the state of the volume when the system crashed
                // 3. undo pass: apply the undo operation of all log records, starting at the last valid record - after this, the volume is in a sane state

                var lsn = clientRecord.clientRestartLSN;
                if (veryDebug)
                    lsn = lsn - (lsn % (RestartArea.fileSize >> 3)) + 0x4408;
                client.Redo(lsn, debugFile, controller);
            }


        }



        NTFSVolume Volume { get; }
        IFile File { get; }
        RestartPageHeader RestartHeader { get; }
        RestartAreaHeader RestartArea { get; }
        ClientRecord[] ClientRecords { get; }
        public Client NTFSClient { get; }


        public NTFSLogFile(NTFSFile file)
        {
            File = file.FileRef.Retain();
            Volume = file.Volume;
            var restartAreaRaw = File.Read(0, I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST);

            // if the disk was checked by chkdsk, the restart area has a magic number of "CHKD" and the update sequence may be missing
            if (restartAreaRaw.ReadInt32(0, Endianness.LittleEndian) == 0x444B4843) { // 'CHKD'
                restartAreaRaw.WriteVal(0, 0x444B4843);
                throw new Exception("chkdsk checked disk not supported (just not tested, actually). mount filesystem in windows first");
            }

            Volume.ReadFixup(restartAreaRaw, 0, restartAreaRaw.Length, 0x52545352); //' RSTR'

            RestartHeader = restartAreaRaw.ReadObject<RestartPageHeader>(0);

            if (RestartHeader.logPageSize != I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST
                || RestartHeader.systemPageSize != I_DONT_KNOW_WHERE_TO_FIND_THIS_VALUE_SO_LETS_USE_A_CONST) {
                throw new Exception("Some fields don't have the values I expected - well, actually I don't really know what I'm talking about, bottom line: there's something wrong.");
            }

            // load client records
            RestartArea = restartAreaRaw.ReadObject<RestartAreaHeader>(RestartHeader.restartOffset);
            long clientRecordOffset = RestartHeader.restartOffset + RestartArea.clientArrayOffset;
            ClientRecords = (ClientRecord[])restartAreaRaw.ReadObjectArr(ref clientRecordOffset, RestartArea.logClientCount, 0, typeof(ClientRecord), Endianness.LittleEndian);

            foreach (var clientRecord in GetClientRecords()) {
                if (clientRecord.clientRestartLSN == 0)
                    clientRecord.sequenceNumber++;
                clientRecord.client = new Client(this, new ClientID() { sequenceNumber = clientRecord.sequenceNumber, index = clientRecord.index });
            }

            NTFSClient = GetClient("NTFS");


            Console.WriteLine("loaded logfile");
        }
    }
}
