using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace AmbientOS
{
    /// <summary>
    /// An unsigned integer of arbitrary size that identifies a node or torrent.
    /// In Mainline DHT, this is always 160-bits long.
    /// A hash is immutable.
    /// </summary>
    public class BigInt
    {
        private static RandomNumberGenerator rng = new RNGCryptoServiceProvider();

        /// <summary>
        /// Returns a random hash within the specified range.
        /// </summary>
        /// <param name="min">The minimum value (inclusive).</param>
        /// <param name="max">The maximum value (exclusive).</param>
        public static BigInt FromRandom(BigInt min, BigInt max)
        {
            var limit = max - min;
            var buffer = new byte[limit.values.Count()];
            rng.GetBytes(buffer);
            // caution: this modulo operation destroys cryptographic randomness of the hash
            buffer[limit.values.Count() - 1] %= limit.values[limit.values.Count() - 1];
            return new BigInt(buffer) + min;
        }

        public static BigInt FromRandom(int bits)
        {
            return FromRandom(Zero, GetMaxVal(bits) + 1);
        }

        public static BigInt FromPattern(byte pattern, int bits)
        {
            var byteCount = bits / 8;
            var bitCount = bits % 8;
            var buffer = new byte[byteCount + (bitCount > 0 ? 1 : 0)];
            for (int i = 0; i < buffer.Count(); i++)
                buffer[i] = pattern;
            if (bitCount > 0)
                buffer[byteCount] = (byte)(buffer[byteCount] & (0xFF >> (8 - bitCount)));
            return new BigInt(buffer);
        }

        public static BigInt GetMaxVal(int bits)
        {
            return FromPattern(0xFF, bits);
        }

        public static readonly BigInt Zero = new BigInt(0);
        public static readonly BigInt MaxValue160 = GetMaxVal(160);

        /// <summary>
        /// Represents a large integer of arbitrary size in little endian (i.e. value[0] is the least significant byte).
        /// It is guaranteed to contain no padding 0 bytes.
        /// </summary>
        private readonly byte[] values;

        public BigInt(byte[] values, Endianness endianness)
        {
            if (endianness == Endianness.BigEndian)
                values = values.Reverse().ToArray();
            else if (endianness != Endianness.LittleEndian)
                throw new ArgumentException("invalid endianness", $"{endianness}");
            this.values = values;

            int padding;
            for (padding = 0; padding < values.Count() - 1; padding++)
                if (values[values.Count() - padding - 1] != 0)
                    break;

            if (padding > 0)
                Array.Resize(ref this.values, values.Count() - padding);
        }

        public BigInt(string value)
            : this((value.Length % 2 == 0 ? value : ("0" + value))
                  .Select((c, i) => new { c = c, i = i >> 1, b = i % 2 == 0 })
                  .GroupBy(item => item.i)
                  .Select(group => (byte)group.Sum(item => Convert.ToInt32(item.c + (item.b ? "0" : ""), 16))).ToArray(),
                  Endianness.BigEndian)
        {
        }

        private BigInt(byte[] values)
            : this(values, Endianness.LittleEndian)
        {
        }

        public BigInt(int value)
            : this(ByteConverter.WriteVal(value, Endianness.LittleEndian))
        {
        }

        /// <summary>
        /// Computes the distance between two hashes by taking their bitwise XOR result.
        /// </summary>
        public BigInt GetDistance(BigInt other)
        {
            return new BigInt(values.Zip(other.values, (int1, int2) => (byte)((int1 ^ int2) & 0xFF)).ToArray());
        }

        public byte[] GetBytes(int numberOfBytes, Endianness endianness)
        {
            IEnumerable<byte> result = values;
            if (result.Count() > numberOfBytes)
                throw new ArgumentException(string.Format("number doesn't fit into {0} bytes", numberOfBytes));
            else if (result.Count() < numberOfBytes)
                result = result.Concat(Enumerable.Repeat((byte)0, numberOfBytes - result.Count()));

            if (endianness == Endianness.BigEndian)
                return result.Reverse().ToArray();
            else if (endianness == Endianness.LittleEndian)
                return result.ToArray();

            throw new ArgumentException("invalid endianness", $"{endianness}");
        }

        /// <summary>
        /// Returns the specified single byte using little endian indexing.
        /// </summary>
        /// <param name="index">The requested index. 0 points to the least significant byte.</param>
        private byte GetByte(int index)
        {
            return index < values.Count() ? values[index] : (byte)0;
        }

        /// <summary>
        /// Returns the specified single byte using big endian indexing.
        /// </summary>
        /// <param name="index">The requested index. 0 points to the most significant byte.</param>
        public byte GetByte(int index, int totalBytes)
        {
            return GetByte(totalBytes - index - 1);
        }

        public override bool Equals(object obj)
        {
            var hash = obj as BigInt;
            if (hash == null)
                return false;
            return this == hash;
        }

        public override int GetHashCode()
        {
            // Note that two equal hashes could have different length (padded with 0). These must evaluate to equal hash codes.
            return values
                .Select((b, i) => new { value = b, index = i })
                .Aggregate(0, (a, b) => unchecked(a = b.index * b.value));
        }

        /// <summary>
        /// Returns the hexadecimal representation of this hash.
        /// </summary>
        public override string ToString()
        {
            return string.Join("", values.Reverse().Select(b => string.Format("{0:x2}", b)));
        }

        public static bool operator ==(BigInt a, BigInt b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
                return false;

            var count = Math.Max(a.values.Count(), b.values.Count());
            while (count-- > 0)
                if (a.GetByte(count) != b.GetByte(count))
                    return false;
            return true;
        }

        public static bool operator !=(BigInt a, BigInt b)
        {
            return !(a == b);
        }

        public static bool operator <(BigInt a, BigInt b)
        {
            if (a == null || b == null)
                return false;

            var count = Math.Max(a.values.Count(), b.values.Count());
            while (count-- > 0) {
                byte val1 = a.GetByte(count), val2 = b.GetByte(count);
                if (val1 < val2)
                    return true;
                else if (val1 > val2)
                    return false;
            }
            return false;
        }

        public static bool operator >(BigInt a, BigInt b)
        {
            if (a == null || b == null)
                return false;

            return !(a < b) && !(a == b);
        }

        public static bool operator <=(BigInt a, BigInt b)
        {
            return (a < b) || (a == b);
        }

        public static bool operator >=(BigInt a, BigInt b)
        {
            return (a > b) || (a == b);
        }

        public static BigInt operator +(BigInt a, BigInt b)
        {
            var count = Math.Max(a.values.Count(), b.values.Count());
            var buffer = new byte[count];
            var carry = 0;

            for (int i = 0; i < count; i++) {
                var sum = a.GetByte(i) + b.GetByte(i) + carry;
                buffer[i] = (byte)(sum & 0xFF);
                carry = sum >> 8;
            }

            if (carry != 0) {
                Array.Resize(ref buffer, count + 1);
                buffer[count] = (byte)(carry & 0xFF);
            }

            return new BigInt(buffer);
        }

        public static BigInt operator +(BigInt a, int b)
        {
            return a + new BigInt(b);
        }

        public static BigInt operator -(BigInt a, BigInt b)
        {
            var count = Math.Max(a.values.Count(), b.values.Count());
            var buffer = new byte[count];
            var carry = 0;

            for (int i = 0; i < count; i++) {
                var sum = a.GetByte(i) - b.GetByte(i) + carry;
                buffer[i] = (byte)(sum & 0xFF);
                carry = sum >> 8;
            }

            if (carry != 0)
                throw new InvalidOperationException(string.Format("{0} is larger than {1}", a.ToString(), b.ToString()));

            return new BigInt(buffer);
        }

        public static BigInt operator -(BigInt a, int b)
        {
            return a - new BigInt(b);
        }

        public static BigInt operator >>(BigInt a, int bits)
        {
            var shiftBytes = bits / 8;
            var shiftBits = bits % 8;
            var buffer = new byte[a.values.Count() - shiftBytes];

            for (int i = 0; i < buffer.Count(); i++) {
                var val = a.values[i + shiftBytes] >> shiftBits;
                if (shiftBits > 0)
                    val += (a.GetByte(i + shiftBytes + 1) << (8 - shiftBits)) & 0xFF;
                buffer[i] = (byte)val;
            }

            return new BigInt(buffer);
        }
    }
}
