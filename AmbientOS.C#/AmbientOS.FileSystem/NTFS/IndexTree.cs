using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem.NTFS
{
    /// <summary>
    /// Specifies how keys in an index tree shall be compared.
    /// If the keys have varying lengths (e.g. binary, unicode), lexicographic ordering is used.
    /// </summary>
    enum CollationRule
    {
        Binary = 0x00, // byte by byte comparision
        Filename = 0x01, // compare by filename attribute (uppercase unicode, as specified by $UpCase)
        Unicode = 0x02, // unicode, except that uppercase letters should come first (case sensitive?)
        ULong = 0x10, // compare by a single 64-bit integers
        SID = 0x11, // security identifier
        SecurityHash = 0x12, // first compare by security hash, then by DID
        ULongs = 0x13 // multiple unsigned 64-bit intergers
    }

    [Endianness(Endianness.LittleEndian)]
    class IndexSequence
    {
        public UInt32 sequenceOffset;
        public UInt32 sequenceEndOffset; // current size of the sequence (incl. header)
        public UInt32 bufferEndOffset; // allocated size of the sequence (incl. header)
        public UInt32 hasChildren;

        /// <summary>
        /// The buffer that contains this sequence.
        /// </summary>
        [FieldSpecs(Ignore = true)]
        public byte[] buffer;

        /// <summary>
        /// The offset in the buffer where the sequence is located.
        /// </summary>
        [FieldSpecs(Ignore = true)]
        public long offset;

        public IEnumerable<IndexEntry> GetEntries()
        {
            var offset = this.offset + sequenceOffset;
            var lastEntry = false;

            while (!lastEntry) {
                var nextEntry = offset + buffer.ReadInt16(offset + 0x8, Endianness.LittleEndian);
                var flags = buffer.ReadInt16(offset + 0xC, Endianness.LittleEndian);
                lastEntry = (flags & 2) != 0;

                if (nextEntry > this.offset + sequenceEndOffset)
                    throw new FormatException("index buffer too long");
                if (nextEntry == offset) // we don't want to end up in an endless loop
                    throw new FormatException("index buffer corrupt");

                yield return new IndexEntry(buffer, offset);

                offset = nextEntry;
            };

            yield break;
        }
    }

    [Endianness(Endianness.LittleEndian)]
    class IndexRoot
    {
        public UInt32 indexedType;
        public CollationRule collationRule;
        public UInt32 bufferSize; // index buffer size in bytes (size of a block in the index allocation)
        public sbyte clustersPerBuffer; // clusters per index buffer (if less than 1 cluster, this field is the -(log2) of the size)
        public byte reserved1;
        public Int16 reserved2;
        public IndexSequence sequence;
    }

    [Endianness(Endianness.LittleEndian)]
    class IndexBuffer
    {
        public FixupHeader header;
        public UInt64 indexBufferNumber;
        public IndexSequence sequence;
    }

    class IndexEntry
    {
        /// <summary>
        /// The MFT file reference
        /// </summary>
        public long fileReference;

        /// <summary>
        /// The location of the stream in the buffer. Null means that this entry has no stream (only the case for the last entry of an index buffer)
        /// </summary>
        public long? streamOffset;

        /// <summary>
        /// The length of the stream
        /// </summary>
        public ushort streamLength;

        /// <summary>
        /// The buffer number of the child of this entry. Null means that the entry is a leaf.
        /// </summary>
        public long? childBuffer;

        public IndexEntry(byte[] buffer, long offset)
        {
            var nextEntry = offset + buffer.ReadInt16(offset + 0x8, Endianness.LittleEndian);
            var flags = buffer.ReadInt16(offset + 0xC, Endianness.LittleEndian);

            fileReference = buffer.ReadInt64(offset, Endianness.LittleEndian);
            streamOffset = (flags & 2) != 0 ? (long?)null : offset + 0x10;
            streamLength = buffer.ReadUInt16(offset + 0xA, Endianness.LittleEndian);
            childBuffer = (flags & 1) != 0 ? buffer.ReadInt64(nextEntry - 8, Endianness.LittleEndian) : (long?)null;
        }
    }


    class IndexTree
    {

        /// <summary>
        /// Contains the contents of the index root attribute
        /// </summary>
        private IndexRoot root;

        /// <summary>
        /// The value of this attribute is an array of index buffers
        /// </summary>
        private NTFSAttribute allocation;

        /// <summary>
        /// Bitmap that specifies for each index buffer if it is in use
        /// </summary>
        private byte[] bitmap;

        /// <summary>
        /// Specifies how many index buffers (used and unused) are in the index allocation.
        /// </summary>
        private long bufferCount;

        /// <summary>
        /// The file that contains this index tree.
        /// </summary>
        private NTFSFileSystemObject file;

        /// <summary>
        /// Opens the index tree with the specified name on the specified file.
        /// For directories, the index that contains the file names is called "$I30"
        /// </summary>
        /// <param name="file">the file from which to load the index tree</param>
        /// <param name="name">null to accept any name</param>
        public IndexTree(NTFSFileSystemObject file, string name)
        {
            this.file = file;

            // load index root
            var indexRootBuffer = file.FileRecord.ReadAttribute(NTFSAttributeType.IndexRoot, name);
            root = indexRootBuffer.ReadObject<IndexRoot>(0);
            root.sequence.buffer = indexRootBuffer;
            root.sequence.offset = 0x10;
            if (root.bufferSize != (root.clustersPerBuffer < 0 ? (1 << -root.clustersPerBuffer) : root.clustersPerBuffer * file.Volume.bytesPerCluster))
                throw new Exception("index buffer size mismatch");

            if (root.sequence.hasChildren != 0) {
                // load index allocation
                allocation = file.FileRecord.GetAttributes(NTFSAttributeType.IndexAllocation, name).First();

                // load bitmap
                bitmap = file.FileRecord.ReadAttribute(NTFSAttributeType.Bitmap, name);
            }
        }

        /// <summary>
        /// Loads the specified index buffer from the index allocation.
        /// This does not check if the buffer is allocated.
        /// </summary>
        /// <param name="number">0 is the first index buffer</param>
        public IndexSequence LoadIndexBuffer(long number)
        {
            var buffer = new byte[root.bufferSize];
            allocation.Read(number * root.bufferSize, root.bufferSize, buffer, 0);
            file.Volume.ReadFixup(buffer, 0, root.bufferSize, 0x58444E49);

            if (buffer.ReadInt64(0x10, Endianness.LittleEndian) != number)
                throw new FormatException("index buffer number mismatch");

            var sequence = buffer.ReadObject<IndexSequence>(0x18);
            sequence.buffer = buffer;
            sequence.offset = 0x18;
            return sequence;
        }

        /// <summary>
        /// Compares two keys, using the collation rule of this tree.
        /// Returns null if they are equal, else returns true if key1 is smaller than key2 and false otherwise.
        /// </summary>
        public bool? Compare(byte[] buffer1, long offset1, long length1, object key2)
        {
            switch (root.collationRule) {
                case CollationRule.Binary:

                    var buffer2b = (byte[])key2;
                    long length2b = buffer2b.Length;

                    for (long i = 0; i < Math.Min(length1, length2b); i++) {
                        if (buffer1[offset1 + i] == buffer2b[i])
                            continue;
                        return buffer1[offset1 + i] < buffer2b[offset1];
                    }

                    if (length1 == length2b)
                        return null;
                    return length1 < length2b;

                case CollationRule.Filename:

                    var upcase = file.Volume.UpCase;
                    if (upcase == null)
                        throw new NotSupportedException("The index is sorted by (case insensitive) filename, but the volume doesn't have the $UpCase file.");

                    long length1s = buffer1[(offset1 += 66) - 2];
                    var buffer1s = buffer1.ReadUInt16Arr(ref offset1, length1s, Endianness.LittleEndian);
                    var buffer2s = (ushort[])key2;
                    long length2s = buffer2s.Length;

                    for (long i = 0; i < Math.Min(length1s, length2s); i++) {
                        ushort val1 = upcase[buffer1s[i]], val2 = upcase[buffer2s[i]];
                        if (val1 == val2)
                            continue;
                        return val1 < val2;
                    }

                    if (length1s == length2s)
                        return null;
                    return length1s < length2s;

                default:
                    throw new NotSupportedException("The index uses an unsupported collation rule");
            }
        }


        /// <summary>
        /// Returns the MFT file reference of all index entries that match the specified key.
        /// The key must have a type suitable for the collation rule used by this index tree:
        /// CollationRule.Binary: byte[]
        /// CollationRule.Filename: string, ushort[] (unicode char array) or byte[] (little endian unicode char array)
        /// </summary>
        public IEnumerable<long> GetValues(object key)
        {
            var sequence = root.sequence;

            switch (root.collationRule) {
                case CollationRule.Binary:
                    if (key.GetType() != typeof(byte[]))
                        throw new Exception("invalid key type");
                    break;
                case CollationRule.Filename:
                    long temp = 0;
                    if (key.GetType() == typeof(string))
                        key = Encoding.Unicode.GetBytes((string)key);
                    if (key.GetType() == typeof(byte[]))
                        key = ((byte[])key).ReadUInt16Arr(ref temp, (((byte[])key).Length + 1) / 2, Endianness.LittleEndian);
                    if (key.GetType() != typeof(ushort[]))
                        throw new Exception("invalid key type");
                    break;

                default:
                    throw new NotSupportedException("The index uses an unsupported collation rule");
            }

            while (true) {
                IndexEntry lastEntry = null;

                foreach (var entry in sequence.GetEntries()) {
                    var relation = entry.streamOffset.HasValue ? Compare(sequence.buffer, entry.streamOffset.Value, entry.streamLength, key) : false;

                    lastEntry = entry;

                    if (!relation.HasValue) // if the key matches, return successfully
                        yield return entry.fileReference;
                    else if (!relation.Value) // if the this entry is larger than the sought key, look into child buffer of this entry
                        break;
                }

                if (lastEntry?.childBuffer == null)
                    yield break;

                sequence = LoadIndexBuffer(lastEntry.childBuffer.Value);
            };
        }

        private IEnumerable<KeyValuePair<Tuple<byte[], long, long>, long>> GetAllValues(IndexSequence sequence)
        {
            foreach (var entry in sequence.GetEntries()) {
                if (entry.childBuffer.HasValue) {
                    var subSequence = LoadIndexBuffer(entry.childBuffer.Value);
                    foreach (var subentry in GetAllValues(subSequence))
                        yield return subentry;
                }
                if (entry.streamOffset.HasValue) {
                    yield return new KeyValuePair<Tuple<byte[], long, long>, long>(new Tuple<byte[], long, long>(sequence.buffer, entry.streamOffset.Value, entry.streamLength), entry.fileReference);
                }
            }
        }

        /// <summary>
        /// Returns all of the key-value pairs in the index tree.
        /// The key consists of the buffer that contains it, its offset and its length.
        /// </summary>
        public IEnumerable<KeyValuePair<Tuple<byte[], long, long>, long>> GetAllValues()
        {
            return GetAllValues(root.sequence);
        }
    }
}
