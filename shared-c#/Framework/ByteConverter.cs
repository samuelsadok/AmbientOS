using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.Framework
{
    /// <summary>
    /// Provides functions for converting raw data (little or big endian) into number values.
    /// These functions work on both little and big endian machines.
    /// </summary>
    static class ByteConverter
    {

        public static Int16 ToInt16LE(byte[] value, int startIndex)
        {
            Int16 result = 0;
            result += (Int16)(value[startIndex++] << 0);
            result += (Int16)(value[startIndex++] << 8);
            return result;
        }

        public static Int16 ToInt16BE(byte[] value, int startIndex)
        {
            Int16 result = 0;
            result += (Int16)(value[startIndex++] << 8);
            result += (Int16)(value[startIndex++] << 0);
            return result;
        }

        public static UInt16 ToUInt16LE(byte[] value, int startIndex)
        {
            UInt16 result = 0;
            result += (UInt16)(value[startIndex++] << 0);
            result += (UInt16)(value[startIndex++] << 8);
            return result;
        }

        public static UInt16 ToUInt16BE(byte[] value, int startIndex)
        {
            UInt16 result = 0;
            result += (UInt16)(value[startIndex++] << 8);
            result += (UInt16)(value[startIndex++] << 0);
            return result;
        }

        public static Int32 ToInt32LE(byte[] value, int startIndex)
        {
            Int32 result = 0;
            result += (Int32)value[startIndex++] << 0;
            result += (Int32)value[startIndex++] << 8;
            result += (Int32)value[startIndex++] << 16;
            result += (Int32)value[startIndex++] << 24;
            return result;
        }

        public static Int32 ToInt32BE(byte[] value, int startIndex)
        {
            Int32 result = 0;
            result += (Int32)value[startIndex++] << 24;
            result += (Int32)value[startIndex++] << 16;
            result += (Int32)value[startIndex++] << 8;
            result += (Int32)value[startIndex++] << 0;
            return result;
        }

        public static UInt32 ToUInt32LE(byte[] value, int startIndex)
        {
            UInt32 result = 0;
            result += (UInt32)value[startIndex++] << 0;
            result += (UInt32)value[startIndex++] << 8;
            result += (UInt32)value[startIndex++] << 16;
            result += (UInt32)value[startIndex++] << 24;
            return result;
        }

        public static UInt32 ToUInt32BE(byte[] value, int startIndex)
        {
            UInt32 result = 0;
            result += (UInt32)value[startIndex++] << 24;
            result += (UInt32)value[startIndex++] << 16;
            result += (UInt32)value[startIndex++] << 8;
            result += (UInt32)value[startIndex++] << 0;
            return result;
        }

        public static Int64 ToInt64LE(byte[] value, int startIndex)
        {
            Int64 result = 0;
            result += (Int64)value[startIndex++] << 0;
            result += (Int64)value[startIndex++] << 8;
            result += (Int64)value[startIndex++] << 16;
            result += (Int64)value[startIndex++] << 24;
            result += (Int64)value[startIndex++] << 32;
            result += (Int64)value[startIndex++] << 40;
            result += (Int64)value[startIndex++] << 48;
            result += (Int64)value[startIndex++] << 56;
            return result;
        }

        public static Int64 ToInt64BE(byte[] value, int startIndex)
        {
            Int64 result = 0;
            result += (Int64)value[startIndex++] << 56;
            result += (Int64)value[startIndex++] << 48;
            result += (Int64)value[startIndex++] << 40;
            result += (Int64)value[startIndex++] << 32;
            result += (Int64)value[startIndex++] << 24;
            result += (Int64)value[startIndex++] << 16;
            result += (Int64)value[startIndex++] << 8;
            result += (Int64)value[startIndex++] << 0;
            return result;
        }

        public static UInt64 ToUInt64LE(byte[] value, int startIndex)
        {
            UInt64 result = 0;
            result += (UInt64)value[startIndex++] << 0;
            result += (UInt64)value[startIndex++] << 8;
            result += (UInt64)value[startIndex++] << 16;
            result += (UInt64)value[startIndex++] << 24;
            result += (UInt64)value[startIndex++] << 32;
            result += (UInt64)value[startIndex++] << 40;
            result += (UInt64)value[startIndex++] << 48;
            result += (UInt64)value[startIndex++] << 56;
            return result;
        }

        public static UInt64 ToUInt64BE(byte[] value, int startIndex)
        {
            UInt64 result = 0;
            result += (UInt64)value[startIndex++] << 56;
            result += (UInt64)value[startIndex++] << 48;
            result += (UInt64)value[startIndex++] << 40;
            result += (UInt64)value[startIndex++] << 32;
            result += (UInt64)value[startIndex++] << 24;
            result += (UInt64)value[startIndex++] << 16;
            result += (UInt64)value[startIndex++] << 8;
            result += (UInt64)value[startIndex++] << 0;
            return result;
        }

        public static float ToSingleLE(byte[] value, int startIndex)
        {
            return BitConverter.ToSingle(BitConverter.IsLittleEndian ? value : value.Skip(startIndex).Take(4).Reverse().ToArray(), BitConverter.IsLittleEndian ? startIndex : 0);
        }

        public static float ToSingleBE(byte[] value, int startIndex)
        {
            return BitConverter.ToSingle(!BitConverter.IsLittleEndian ? value : value.Skip(startIndex).Take(4).Reverse().ToArray(), !BitConverter.IsLittleEndian ? startIndex : 0);
        }

        public static double ToDoubleLE(byte[] value, int startIndex)
        {
            return BitConverter.ToDouble(BitConverter.IsLittleEndian ? value : value.Skip(startIndex).Take(8).Reverse().ToArray(), BitConverter.IsLittleEndian ? startIndex : 0);
        }

        public static double ToDoubleBE(byte[] value, int startIndex)
        {
            return BitConverter.ToDouble(!BitConverter.IsLittleEndian ? value : value.Skip(startIndex).Take(8).Reverse().ToArray(), !BitConverter.IsLittleEndian ? startIndex : 0);
        }

        public static byte[] GetBytesLE(Int16 value)
        {
            return new byte[] {
                (byte)((value >> 0) & 0xFF),
                (byte)((value >> 8) & 0xFF)
            };
        }

        public static byte[] GetBytesBE(Int16 value)
        {
            return new byte[] {
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 0) & 0xFF)
            };
        }

        public static byte[] GetBytesLE(UInt16 value)
        {
            return new byte[] {
                (byte)((value >> 0) & 0xFF),
                (byte)((value >> 8) & 0xFF)
            };
        }

        public static byte[] GetBytesBE(UInt16 value)
        {
            return new byte[] {
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 0) & 0xFF)
            };
        }

        public static byte[] GetBytesLE(Int32 value)
        {
            return new byte[] {
                (byte)((value >> 0) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF)
            };
        }

        public static byte[] GetBytesBE(Int32 value)
        {
            return new byte[] {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 0) & 0xFF)
            };
        }

        public static byte[] GetBytesLE(UInt32 value)
        {
            return new byte[] {
                (byte)((value >> 0) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF)
            };
        }

        public static byte[] GetBytesBE(UInt32 value)
        {
            return new byte[] {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 0) & 0xFF)
            };
        }

        public static byte[] GetBytesLE(Int64 value)
        {
            return new byte[] {
                (byte)((value >> 0) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 32) & 0xFF),
                (byte)((value >> 40) & 0xFF),
                (byte)((value >> 48) & 0xFF),
                (byte)((value >> 56) & 0xFF)
            };
        }

        public static byte[] GetBytesBE(Int64 value)
        {
            return new byte[] {
                (byte)((value >> 56) & 0xFF),
                (byte)((value >> 48) & 0xFF),
                (byte)((value >> 40) & 0xFF),
                (byte)((value >> 32) & 0xFF),
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 0) & 0xFF)
            };
        }

        public static byte[] GetBytesLE(UInt64 value)
        {
            return new byte[] {
                (byte)((value >> 0) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 32) & 0xFF),
                (byte)((value >> 40) & 0xFF),
                (byte)((value >> 48) & 0xFF),
                (byte)((value >> 56) & 0xFF)
            };
        }

        public static byte[] GetBytesBE(UInt64 value)
        {
            return new byte[] {
                (byte)((value >> 56) & 0xFF),
                (byte)((value >> 48) & 0xFF),
                (byte)((value >> 40) & 0xFF),
                (byte)((value >> 32) & 0xFF),
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 0) & 0xFF)
            };
        }

        public static byte[] GetBytesLE(float value)
        {
            var result = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                result = result.Reverse().ToArray();
            return result;
        }

        public static byte[] GetBytesBE(float value)
        {
            var result = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                result = result.Reverse().ToArray();
            return result;
        }

        public static byte[] GetBytesLE(double value)
        {
            var result = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                result = result.Reverse().ToArray();
            return result;
        }

        public static byte[] GetBytesBE(double value)
        {
            var result = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                result = result.Reverse().ToArray();
            return result;
        }
    }
}
