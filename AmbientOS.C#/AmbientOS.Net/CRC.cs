using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace AmbientOS.Net
{
    // The code in this file is derived from this post:
    // http://www.codeproject.com/Articles/35134/How-to-calculate-a-CRC-in-Csharp
    // Thanks, Tamir Khason!


    /// <summary>
    /// Implements a 16-bit CRC hashing algorithm.
    /// The output is an array in big endian format.
    /// Do not use this anywhere near applications where security matters.
    /// </summary>
    public class CRC16 : HashAlgorithm
    {
        private static readonly Dictionary<ushort, ushort[]> tableCache = new Dictionary<ushort, ushort[]>();

        private ushort crc;
        private readonly ushort[] table;

        private static ushort[] BuildTable(ushort polynomial)
        {
            var table = new ushort[256];

            for (ushort i = 0; i < 256; i++) {
                var crc = i;

                for (byte j = 8; j > 0; j--) {
                    if ((crc & 1) != 0)
                        crc = (ushort)((crc >> 1) ^ polynomial);
                    else
                        crc >>= 1;
                }

                table[i] = crc;
            }

            return table;
        }

        public CRC16(ushort polynomial)
        {
            lock (tableCache) {
                if (!tableCache.TryGetValue(polynomial, out table))
                    table = tableCache[polynomial] = BuildTable(polynomial);
            }

            HashSizeValue = 16;
            Initialize();
        }

        /// <summary>
        /// Returns the CRC16 hash algorithm defined in IEEE 802.3.
        /// </summary>
        public static CRC16 IEEE8023()
        {
            // polynomial: 0xA001, for some reason we use the inverse here
            return new CRC16(0x8408);
        }

        public override void Initialize()
        {
            crc = 0xFFFF;
        }

        protected override void HashCore(byte[] buffer, int offset, int count)
        {
            for (int i = offset; i < count; i++) {
                var ptr = (byte)((crc ^ buffer[i]) & 0xFF);
                crc >>= 8;
                crc ^= table[ptr];
            }
        }

        protected override byte[] HashFinal()
        {
            var finalCRC = (ushort)(crc ^ 0xFFFF);

            byte[] finalHash = new byte[2];
            finalHash[1] = (byte)((finalCRC >> 0) & 0xFF);
            finalHash[0] = (byte)((finalCRC >> 8) & 0xFF);

            return finalHash;
        }
    }


    /// <summary>
    /// Implements a 32-bit CRC hashing algorithm.
    /// The output is an array in big endian format.
    /// Do not use this anywhere near applications where security matters.
    /// </summary>
    public class CRC32 : HashAlgorithm
    {
        private static readonly Dictionary<uint, uint[]> tableCache = new Dictionary<uint, uint[]>();

        private uint crc;
        private readonly uint[] table;

        private static uint[] BuildTable(uint polynomial)
        {
            var table = new uint[256];

            for (uint i = 0; i < 256; i++) {
                var crc = i;

                for (int j = 8; j > 0; j--) {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ polynomial;
                    else
                        crc >>= 1;
                }

                table[i] = crc;
            }

            return table;
        }

        public CRC32(uint polynomial)
        {
            lock (tableCache) {
                if (!tableCache.TryGetValue(polynomial, out table))
                    table = tableCache[polynomial] = BuildTable(polynomial);
            }

            HashSizeValue = 32;
            Initialize();
        }

        /// <summary>
        /// Returns the CRC32 hash algorithm defined in IEEE 802.3.
        /// </summary>
        public static CRC32 IEEE8023()
        {
            // polynomial: 0x04C11DB7, for some reason we use the inverse here
            return new CRC32(0xEDB88320);
        }

        /// <summary>
        /// Returns the CRC32-C (Castagnoli) hash algorithm.
        /// </summary>
        public static CRC32 Castagnoli()
        {
            // polynomial: 0x1EDC6F41, for some reason we use the inverse here
            return new CRC32(0x82F63B78);
            //return new CRC32(0x1EDC6F41);
        }

        public override void Initialize()
        {
            crc = 0xFFFFFFFF;
        }

        protected override void HashCore(byte[] buffer, int offset, int count)
        {
            for (int i = offset; i < count; i++) {
                var ptr = (byte)((crc ^ buffer[i]) & 0xFF);
                crc >>= 8;
                crc ^= table[ptr];
            }
        }

        protected override byte[] HashFinal()
        {
            var finalCRC = crc ^ 0xFFFFFFFF;

            byte[] finalHash = new byte[4];
            finalHash[3] = (byte)((finalCRC >> 0) & 0xFF);
            finalHash[2] = (byte)((finalCRC >> 8) & 0xFF);
            finalHash[1] = (byte)((finalCRC >> 16) & 0xFF);
            finalHash[0] = (byte)((finalCRC >> 24) & 0xFF);

            return finalHash;
        }
    }
}
