using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace AmbientOS
{

    public enum Endianness
    {
        /// <summary>
        /// Used to indicate that the endianness is not specified.
        /// This is the default.
        /// </summary>
        Unspecified,

        LittleEndian,

        BigEndian,
        NetworkByteOrder = BigEndian,

        /// <summary>
        /// Used to specify that the byteconverter should use whatever the endianness of the machine is.
        /// It is not yet clear how this maps to architectures like ARM where the kernel can select endianness for memory pages.
        /// </summary>
        Current
    }

    /// <summary>
    /// Use this attribute for a structure that has a well-known layout.
    /// This can then be used to read the structure from a byte array or generate a byte array from the struct.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class EndiannessAttribute : Attribute
    {
        public Endianness Endianness { get; }

        public EndiannessAttribute(Endianness endianness)
        {
            Endianness = endianness;
        }
    }

    /// <summary>
    /// Use this attribute to specify that the ByteConverter should also read or write the fields of the basetype of this type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class TypeSpecs : Attribute
    {
        public bool WalkBaseType { get; set; }
        public TypeSpecs()
        {
        }
    }

    public class EnumType : Attribute
    {
        public Type Type { get; }

        public EnumType(string name)
        {
            Type = Type.GetType(name, true);
        }
    }

    public enum DateFormat
    {
        Unspecified = 0,

        /// <summary>
        /// Number of ticks (0.1us) since 01/01/1601 (UTC)
        /// </summary>
        NTFS,

        /// <summary>
        /// Windows FILETIME type, equal to NTFS file time
        /// </summary>
        Windows = NTFS
    }

    public enum StringFormat
    {
        Unspecified = 0,
        ASCII,
        Unicode
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class FieldSpecsAttribute : Attribute
    {
        /// <summary>
        /// If true, the field is ignored and the byte offset is not altered.
        /// </summary>
        public bool Ignore { get; set; }

        /// <summary>
        /// If true, the field is ignored when the end of the buffer is reached (reading) or the value is null (writing)
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// Must be used on DateTime fields
        /// </summary>
        public DateFormat DateFormat { get; set; }

        /// <summary>
        /// Must be used on string fields
        /// </summary>
        public StringFormat StringFormat { get; set; }

        /// <summary>
        /// Used to specify that the field belongs to a union.
        /// Fields of a union must be consecutive.
        /// </summary>
        public bool Union { get; set; }

        /// <summary>
        /// This must be set to true on the last field of a new union
        /// </summary>
        public bool EndOfUnion { get; set; }

        /// <summary>
        /// Specifies the type in which an enum is stored.
        /// This will typically be one of: (System. ?) byte, System.Int16, System.Int32.
        /// The default is System.Int32
        /// </summary>
        public string EnumType { get; set; }

        /// <summary>
        /// Indicates that the field specifies the length of an array or string field.
        /// The value of this property should be the name of that field or null.
        /// </summary>
        public string LengthOf { get; set; }

        /// <summary>
        /// Can be used on array fields to indicate the size of an element.
        /// This is useful if the intrinsic size of the array element type does not match the intended size.
        /// For simple element types (e.g. Int16, UInt16, Int32, UInt32, Int64, UInt64), this field is ignored.
        /// The value of this property should either be the name of another field or null to determine the size automatically.
        /// </summary>
        public string ElementSize { get; set; }

        /// <summary>
        /// Specifies the length of an array or string.
        /// If this number is not constant, the length must be specified using another field as length specifier.
        /// If another field specifies the length, it must occur before this field.
        /// </summary>
        public long Length { get; set; }

        public FieldSpecsAttribute()
        {
        }
    }



    /// <summary>
    /// Provides functions for converting raw data (little or big endian) into number values.
    /// These functions work on both little and big endian machines.
    /// </summary>
    public static class ByteConverter
    {
        const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static Endianness Resolve<T>(this Endianness endianness)
        {
            switch (endianness) {
                case Endianness.Unspecified:
                    var attr = typeof(T).GetCustomAttribute<EndiannessAttribute>();
                    if (attr == null)
                        throw new NotSupportedException("The type " + typeof(T) + " must specify an endianness");
                    return attr.Endianness;

                case Endianness.Current:
                    return BitConverter.IsLittleEndian ? Endianness.LittleEndian : Endianness.BigEndian;

                case Endianness.LittleEndian:
                case Endianness.BigEndian:
                    return endianness;

                default:
                    throw new Exception("Unknown endianness value.");
            }
        }


        #region Reading

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int16 ReadInt16(this byte[] buffer, ref long offset, Endianness endianness)
        {
            Int16 result = 0;
            if (endianness == Endianness.LittleEndian) {
                result += (Int16)(buffer[offset++] << 0);
                result += (Int16)(buffer[offset++] << 8);
            } else {
                result += (Int16)(buffer[offset++] << 8);
                result += (Int16)(buffer[offset++] << 0);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt16 ReadUInt16(this byte[] buffer, ref long offset, Endianness endianness)
        {
            UInt16 result = 0;
            if (endianness == Endianness.LittleEndian) {
                result += (UInt16)(buffer[offset++] << 0);
                result += (UInt16)(buffer[offset++] << 8);
            } else {
                result += (UInt16)(buffer[offset++] << 8);
                result += (UInt16)(buffer[offset++] << 0);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int32 ReadInt32(this byte[] buffer, ref long offset, Endianness endianness)
        {
            Int32 result = 0;

            if (endianness == Endianness.LittleEndian) {
                result += (Int32)buffer[offset++] << 0;
                result += (Int32)buffer[offset++] << 8;
                result += (Int32)buffer[offset++] << 16;
                result += (Int32)buffer[offset++] << 24;
            } else {
                result += (Int32)buffer[offset++] << 24;
                result += (Int32)buffer[offset++] << 16;
                result += (Int32)buffer[offset++] << 8;
                result += (Int32)buffer[offset++] << 0;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 ReadUInt32(this byte[] buffer, ref long offset, Endianness endianness)
        {
            UInt32 result = 0;
            if (endianness == Endianness.LittleEndian) {
                result += (UInt32)buffer[offset++] << 0;
                result += (UInt32)buffer[offset++] << 8;
                result += (UInt32)buffer[offset++] << 16;
                result += (UInt32)buffer[offset++] << 24;
            } else {
                result += (UInt32)buffer[offset++] << 24;
                result += (UInt32)buffer[offset++] << 16;
                result += (UInt32)buffer[offset++] << 8;
                result += (UInt32)buffer[offset++] << 0;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int64 ReadInt64(this byte[] buffer, ref long offset, Endianness endianness)
        {
            Int64 result = 0;
            if (endianness == Endianness.LittleEndian) {
                result += (Int64)buffer[offset++] << 0;
                result += (Int64)buffer[offset++] << 8;
                result += (Int64)buffer[offset++] << 16;
                result += (Int64)buffer[offset++] << 24;
                result += (Int64)buffer[offset++] << 32;
                result += (Int64)buffer[offset++] << 40;
                result += (Int64)buffer[offset++] << 48;
                result += (Int64)buffer[offset++] << 56;
            } else {
                result += (Int64)buffer[offset++] << 56;
                result += (Int64)buffer[offset++] << 48;
                result += (Int64)buffer[offset++] << 40;
                result += (Int64)buffer[offset++] << 32;
                result += (Int64)buffer[offset++] << 24;
                result += (Int64)buffer[offset++] << 16;
                result += (Int64)buffer[offset++] << 8;
                result += (Int64)buffer[offset++] << 0;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 ReadUInt64(this byte[] buffer, ref long offset, Endianness endianness)
        {
            UInt64 result = 0;
            if (endianness == Endianness.LittleEndian) {
                result += (UInt64)buffer[offset++] << 0;
                result += (UInt64)buffer[offset++] << 8;
                result += (UInt64)buffer[offset++] << 16;
                result += (UInt64)buffer[offset++] << 24;
                result += (UInt64)buffer[offset++] << 32;
                result += (UInt64)buffer[offset++] << 40;
                result += (UInt64)buffer[offset++] << 48;
                result += (UInt64)buffer[offset++] << 56;
            } else {
                result += (UInt64)buffer[offset++] << 56;
                result += (UInt64)buffer[offset++] << 48;
                result += (UInt64)buffer[offset++] << 40;
                result += (UInt64)buffer[offset++] << 32;
                result += (UInt64)buffer[offset++] << 24;
                result += (UInt64)buffer[offset++] << 16;
                result += (UInt64)buffer[offset++] << 8;
                result += (UInt64)buffer[offset++] << 0;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadSingle(this byte[] buffer, ref long offset, Endianness endianness)
        {
            float result;
            if ((endianness == Endianness.LittleEndian) == BitConverter.IsLittleEndian)
                result = BitConverter.ToSingle(buffer, (int)offset);
            else
                result = BitConverter.ToSingle(buffer.Skip((int)offset).Take(8).Reverse().ToArray(), 0);
            offset += 4;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReadDouble(this byte[] buffer, ref long offset, Endianness endianness)
        {
            double result;
            if ((endianness == Endianness.LittleEndian) == BitConverter.IsLittleEndian)
                result = BitConverter.ToDouble(buffer, (int)offset);
            else
                result = BitConverter.ToDouble(buffer.Skip((int)offset).Take(8).Reverse().ToArray(), 0);
            offset += 8;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime ReadDateTime(this byte[] buffer, ref long offset, DateFormat format, Endianness endianness)
        {
            switch (format) {
                case DateFormat.NTFS: return new DateTime(1601, 1, 1).AddTicks(buffer.ReadInt64(ref offset, endianness));
                default: throw new NotSupportedException();
            }
        }

        /// <param name="length">length in chars</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadString(this byte[] buffer, ref long offset, long length, StringFormat format, Endianness endianness)
        {
            string result;
            switch (format) {
                case StringFormat.Unicode:
                    length *= 2;
                    var encoding = endianness == Endianness.LittleEndian ? Encoding.Unicode : Encoding.BigEndianUnicode;
                    result = encoding.GetString(buffer, (int)offset, (int)length);
                    break;
                case StringFormat.ASCII:
                    result = Encoding.ASCII.GetString(buffer, (int)offset, (int)length);
                    break;
                default:
                    throw new NotSupportedException();
            }
            offset += length;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Guid ReadGuid(this byte[] buffer, ref long offset)
        {
            var guid = new byte[16];
            Array.Copy(buffer, offset, guid, 0, 16);
            return new Guid(guid);
        }

        /// <param name="length">number of elements to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int16[] ReadInt16Arr(this byte[] buffer, ref long offset, long length, Endianness endianness)
        {
            var result = new Int16[length];
            for (long i = 0; i < length; i++)
                result[i] = buffer.ReadInt16(ref offset, endianness);
            return result;
        }

        /// <param name="length">number of elements to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt16[] ReadUInt16Arr(this byte[] buffer, ref long offset, long length, Endianness endianness)
        {
            var result = new UInt16[length];
            for (long i = 0; i < length; i++)
                result[i] = buffer.ReadUInt16(ref offset, endianness);
            return result;
        }

        /// <param name="length">number of elements to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int32[] ReadInt32Arr(this byte[] buffer, ref long offset, long length, Endianness endianness)
        {
            var result = new Int32[length];
            for (long i = 0; i < length; i++)
                result[i] = buffer.ReadInt32(ref offset, endianness);
            return result;
        }

        /// <param name="length">number of elements to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32[] ReadUInt32Arr(this byte[] buffer, ref long offset, long length, Endianness endianness)
        {
            var result = new UInt32[length];
            for (long i = 0; i < length; i++)
                result[i] = buffer.ReadUInt32(ref offset, endianness);
            return result;
        }

        /// <param name="length">number of elements to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int64[] ReadInt64Arr(this byte[] buffer, ref long offset, long length, Endianness endianness)
        {
            var result = new Int64[length];
            for (long i = 0; i < length; i++)
                result[i] = buffer.ReadInt64(ref offset, endianness);
            return result;
        }

        /// <param name="length">number of elements to read</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64[] ReadUInt64Arr(this byte[] buffer, ref long offset, long length, Endianness endianness)
        {
            var result = new UInt64[length];
            for (long i = 0; i < length; i++)
                result[i] = buffer.ReadUInt64(ref offset, endianness);
            return result;
        }

        /// <param name="length">number of elements to read</param>
        /// <param name="elementSize">if not 0, overrides the size of an element (overlapping elements are allowed)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object ReadObjectArr(this byte[] buffer, ref long offset, long length, long elementSize, Type elementType, Endianness endianness)
        {
            var result = Array.CreateInstance(elementType, length);
            for (long i = 0; i < length; i++) {
                var oldOffset = offset;
                result.SetValue(buffer.ReadObject(ref offset, elementType, null, null, null, endianness), i);
                if (elementSize != 0)
                    offset = oldOffset + elementSize;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int16 ReadInt16(this byte[] buffer, long offset, Endianness endianness)
        {
            return buffer.ReadInt16(ref offset, endianness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt16 ReadUInt16(this byte[] buffer, long offset, Endianness endianness)
        {
            return buffer.ReadUInt16(ref offset, endianness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int32 ReadInt32(this byte[] buffer, long offset, Endianness endianness)
        {
            return buffer.ReadInt32(ref offset, endianness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 ReadUInt32(this byte[] buffer, long offset, Endianness endianness)
        {
            return buffer.ReadUInt32(ref offset, endianness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int64 ReadInt64(this byte[] buffer, long offset, Endianness endianness)
        {
            return buffer.ReadInt64(ref offset, endianness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 ReadUInt64(this byte[] buffer, long offset, Endianness endianness)
        {
            return buffer.ReadUInt64(ref offset, endianness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadString(this byte[] buffer, long offset, long length, StringFormat format, Endianness endianness)
        {
            return buffer.ReadString(ref offset, length, format, endianness);
        }


        /// <summary>
        /// Reads the fields of struct or class from a byte array.
        /// The endianness must be specified by an EndiannessAttribute of the type.
        /// Other than that, the semantics are equivalent to the overload taking a (runtime) type argument.
        /// </summary>
        public static T ReadObject<T>(this byte[] buffer, long offset, Endianness endianness = Endianness.Unspecified)
        {
            return (T)buffer.ReadObject(ref offset, typeof(T), null, null, null, endianness.Resolve<T>());
        }

        /// <summary>
        /// This is equivalent to the more general overload, except that it automatically determines the type of the target object.
        /// </summary>
        public static void ReadObject(this byte[] buffer, ref long offset, object obj, Endianness endianness)
        {
            buffer.ReadObject(ref offset, obj, obj.GetType(), endianness);
        }

        /// <summary>
        /// Reads the fields of struct or class from a byte array.
        /// This version is in semantics identical to the other overloads, except that it sets the
        /// fields on an already existing object, lifting the requirement of a parameterless constructor.
        /// </summary>
        public static void ReadObject(this byte[] buffer, ref long offset, object obj, Type type, Endianness endianness)
        {
            if (type.GetCustomAttribute<TypeSpecs>()?.WalkBaseType ?? false)
                buffer.ReadObject(ref offset, obj, type.BaseType, endianness);

            var lengthSpecifiers = new Dictionary<string, long?>();
            long? elementCount;
            long unionSize = 0;

            foreach (var field in type.GetFields(bindingFlags | BindingFlags.DeclaredOnly)) {
                var fieldAttr = field.GetCustomAttribute<FieldSpecsAttribute>();
                if (fieldAttr?.Ignore ?? false)
                    continue;
                if (field.Name.Contains("<")) // such names don't occur normally (to be verified) - they are compiler generated
                    continue;
                if (fieldAttr?.Optional ?? false && offset >= buffer.Count())
                    continue;

                elementCount = null;
                if (lengthSpecifiers.Any())
                    if (lengthSpecifiers.TryGetValue(field.Name, out elementCount))
                        lengthSpecifiers.Remove(field.Name);

                var newOffset = offset;
                field.SetValue(obj, buffer.ReadObject(ref newOffset, field.FieldType, fieldAttr, elementCount, obj, endianness));

                if (fieldAttr?.LengthOf != null)
                    lengthSpecifiers[fieldAttr.LengthOf] = Convert.ToInt64(field.GetValue(obj));

                if (fieldAttr?.Union ?? false) {
                    unionSize = Math.Max(unionSize, newOffset - offset);
                    if (fieldAttr.EndOfUnion) {
                        offset += unionSize;
                        unionSize = 0;
                    }
                } else {
                    offset = newOffset;
                }
            }
        }


        /// <summary>
        /// Reads an object from a byte array.
        /// The object can be of any of the supported basic types (for which a strongly typed version exist) or a class or struct.
        /// If the type is a class or struct, it's fields are read recursively.
        /// In this case, the fields are assumed to be tightly packed (pack=1).
        /// </summary>
        /// <param name="offset">Location in the buffer, where the value should be read.</param>
        /// <param name="type">The type of the desired object.</param>
        /// <param name="fieldSpecs">Can be null for any type other than: DateTime, string</param>
        /// <param name="endianness">The endianness in which the value should be interpreted.</param>
        /// <exception cref="NotSupportedException">An unsupported type was encountered.</exception>
        public static object ReadObject(this byte[] buffer, ref long offset, Type type, FieldSpecsAttribute fieldSpecs, long? elementCount, object parent, Endianness endianness)
        {
            if (type == typeof(byte)) {
                return buffer[offset++];
            } else if (type == typeof(sbyte)) {
                return (sbyte)buffer[offset++];
            } else if (type == typeof(Int16)) {
                return buffer.ReadInt16(ref offset, endianness);
            } else if (type == typeof(UInt16)) {
                return buffer.ReadUInt16(ref offset, endianness);
            } else if (type == typeof(Int32)) {
                return buffer.ReadInt32(ref offset, endianness);
            } else if (type == typeof(UInt32)) {
                return buffer.ReadUInt32(ref offset, endianness);
            } else if (type == typeof(Int64)) {
                return buffer.ReadInt64(ref offset, endianness);
            } else if (type == typeof(UInt64)) {
                return buffer.ReadUInt64(ref offset, endianness);
            } else if (type == typeof(double)) {
                return buffer.ReadInt64(ref offset, endianness);
            } else if (type == typeof(float)) {
                return buffer.ReadUInt64(ref offset, endianness);
            } else if (type == typeof(DateTime)) {
                return buffer.ReadDateTime(ref offset, fieldSpecs.DateFormat, endianness);
            } else if (type == typeof(string)) {
                return buffer.ReadString(ref offset, elementCount ?? fieldSpecs.Length, fieldSpecs.StringFormat, endianness);
            } else if (type == typeof(Guid)) {
                return buffer.ReadGuid(ref offset);
            } else if (type.IsEnum) {
                return buffer.ReadObject(ref offset, type.GetCustomAttribute<EnumType>()?.Type ?? typeof(Int32), fieldSpecs, elementCount, parent, endianness);
            } else if (type.IsArray) {
                type = type.GetElementType();
                var length = elementCount ?? fieldSpecs.Length;
                if (type == typeof(Int16)) {
                    return buffer.ReadInt16Arr(ref offset, length, endianness);
                } else if (type == typeof(UInt16)) {
                    return buffer.ReadUInt16Arr(ref offset, length, endianness);
                } else if (type == typeof(Int32)) {
                    return buffer.ReadInt32Arr(ref offset, length, endianness);
                } else if (type == typeof(UInt32)) {
                    return buffer.ReadUInt32Arr(ref offset, length, endianness);
                } else if (type == typeof(Int64)) {
                    return buffer.ReadInt64Arr(ref offset, length, endianness);
                } else if (type == typeof(UInt64)) {
                    return buffer.ReadUInt64Arr(ref offset, length, endianness);
                }
                long elementSize = 0;
                if (fieldSpecs?.ElementSize != null)
                    elementSize = Convert.ToInt64(parent.GetType().GetField(fieldSpecs.ElementSize, bindingFlags).GetValue(parent));
                return buffer.ReadObjectArr(ref offset, length, elementSize, type, endianness);
            }

            // Since none of the above types applied, we assume this is a custom class or struct
            var result = Activator.CreateInstance(type, true);
            buffer.ReadObject(ref offset, result, type, endianness);
            return result;
        }

        #endregion


        #region Writing

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, Int16 value, Endianness endianness)
        {
            if (endianness == Endianness.LittleEndian) {
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
            } else {
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
            }
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, UInt16 value, Endianness endianness)
        {
            if (endianness == Endianness.LittleEndian) {
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
            } else {
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
            }
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, Int32 value, Endianness endianness)
        {
            if (endianness == Endianness.LittleEndian) {
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
                buffer[offset++] = (byte)((value >> 16) & 0xFF);
                buffer[offset++] = (byte)((value >> 24) & 0xFF);
            } else {
                buffer[offset++] = (byte)((value >> 24) & 0xFF);
                buffer[offset++] = (byte)((value >> 16) & 0xFF);
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
            }
            return 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, UInt32 value, Endianness endianness)
        {
            if (endianness == Endianness.LittleEndian) {
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
                buffer[offset++] = (byte)((value >> 16) & 0xFF);
                buffer[offset++] = (byte)((value >> 24) & 0xFF);
            } else {
                buffer[offset++] = (byte)((value >> 24) & 0xFF);
                buffer[offset++] = (byte)((value >> 16) & 0xFF);
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
            }
            return 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, Int64 value, Endianness endianness)
        {
            if (endianness == Endianness.LittleEndian) {
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
                buffer[offset++] = (byte)((value >> 16) & 0xFF);
                buffer[offset++] = (byte)((value >> 24) & 0xFF);
                buffer[offset++] = (byte)((value >> 32) & 0xFF);
                buffer[offset++] = (byte)((value >> 40) & 0xFF);
                buffer[offset++] = (byte)((value >> 48) & 0xFF);
                buffer[offset++] = (byte)((value >> 56) & 0xFF);
            } else {
                buffer[offset++] = (byte)((value >> 56) & 0xFF);
                buffer[offset++] = (byte)((value >> 48) & 0xFF);
                buffer[offset++] = (byte)((value >> 40) & 0xFF);
                buffer[offset++] = (byte)((value >> 32) & 0xFF);
                buffer[offset++] = (byte)((value >> 24) & 0xFF);
                buffer[offset++] = (byte)((value >> 16) & 0xFF);
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
            }
            return 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, UInt64 value, Endianness endianness)
        {
            if (endianness == Endianness.LittleEndian) {
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
                buffer[offset++] = (byte)((value >> 16) & 0xFF);
                buffer[offset++] = (byte)((value >> 24) & 0xFF);
                buffer[offset++] = (byte)((value >> 32) & 0xFF);
                buffer[offset++] = (byte)((value >> 40) & 0xFF);
                buffer[offset++] = (byte)((value >> 48) & 0xFF);
                buffer[offset++] = (byte)((value >> 56) & 0xFF);
            } else {
                buffer[offset++] = (byte)((value >> 56) & 0xFF);
                buffer[offset++] = (byte)((value >> 48) & 0xFF);
                buffer[offset++] = (byte)((value >> 40) & 0xFF);
                buffer[offset++] = (byte)((value >> 32) & 0xFF);
                buffer[offset++] = (byte)((value >> 24) & 0xFF);
                buffer[offset++] = (byte)((value >> 16) & 0xFF);
                buffer[offset++] = (byte)((value >> 8) & 0xFF);
                buffer[offset++] = (byte)((value >> 0) & 0xFF);
            }
            return 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, float value, Endianness endianness)
        {
            var result = BitConverter.GetBytes(value);
            if ((endianness == Endianness.LittleEndian) != BitConverter.IsLittleEndian)
                result = result.Reverse().ToArray();
            Array.Copy(result, 0, buffer, offset, 4);
            return 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, double value, Endianness endianness)
        {
            var result = BitConverter.GetBytes(value);
            if ((endianness == Endianness.LittleEndian) != BitConverter.IsLittleEndian)
                result = result.Reverse().ToArray();
            Array.Copy(result, 0, buffer, offset, 8);
            return 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, DateTime value, DateFormat format, Endianness endianness)
        {
            switch (format) {
                case DateFormat.NTFS: return buffer.WriteVal(offset, (value).Subtract(new DateTime(1601, 1, 1)).Ticks, endianness);
                default: throw new NotSupportedException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, string value, StringFormat format, Endianness endianness)
        {
            switch (format) {
                case StringFormat.Unicode: return Encoding.Unicode.GetBytes(value, 0, value.Length, buffer, (int)offset);
                case StringFormat.ASCII: return Encoding.ASCII.GetBytes(value, 0, value.Length, buffer, (int)offset);
                default: throw new NotSupportedException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, Guid value)
        {
            var guid = value.ToByteArray();
            Array.Copy(guid, 0, buffer, offset, 16);
            return 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, Int16[] value, Endianness endianness)
        {
            long size = 0;
            foreach (var val in value)
                size += buffer.WriteVal(offset + size, val, endianness);
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, UInt16[] value, Endianness endianness)
        {
            long size = 0;
            foreach (var val in value)
                size += buffer.WriteVal(offset + size, val, endianness);
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, Int32[] value, Endianness endianness)
        {
            long size = 0;
            foreach (var val in value)
                size += buffer.WriteVal(offset + size, val, endianness);
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, UInt32[] value, Endianness endianness)
        {
            long size = 0;
            foreach (var val in value)
                size += buffer.WriteVal(offset + size, val, endianness);
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, Int64[] value, Endianness endianness)
        {
            long size = 0;
            foreach (var val in value)
                size += buffer.WriteVal(offset + size, val, endianness);
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, UInt64[] value, Endianness endianness)
        {
            long size = 0;
            foreach (var val in value)
                size += buffer.WriteVal(offset + size, val, endianness);
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long WriteVal(this byte[] buffer, long offset, Array value, long elementSize, Endianness endianness)
        {
            long size = 0;
            var elementType = value.GetType().GetElementType();
            foreach (var val in value) {
                var currentSize = buffer.WriteVal(offset + size, val, elementType, null, null, endianness);
                size += elementSize == 0 ? currentSize : elementSize;
            }
            return size;
        }


        /// <summary>
        /// Writes the fields of a struct or class into a byte array.
        /// The endianness must be specified by an EndiannessAttribute of the type.
        /// Other than that, the semantics are equivalent to the overload taking an object argument.
        /// </summary>
        public static long WriteVal<T>(this byte[] buffer, long offset, T value, Endianness endianness = Endianness.Unspecified)
        {
            return buffer.WriteVal(offset, value, typeof(T), null, null, endianness.Resolve<T>());
        }


        /// <summary>
        /// Writes the object into a byte array.
        /// This can be any of the supported basic types (for which a strongly typed overload exist) or a class or struct.
        /// If the type is a class or struct, it's fields are written recursively.
        /// In this case, the fields are assumed to be tightly packed (pack=1).
        /// </summary>
        /// <param name="offset">Location in the buffer, where the value should be written.</param>
        /// <param name="value">The value to be written.</param>
        /// <param name="fieldSpecs">Can be null for any type other than: DateTime, string</param>
        /// <param name="endianness">The endianness in which the value should be written.</param>
        /// <exception cref="NotSupportedException">An unsupported type was encountered.</exception>
        public static long WriteVal(this byte[] buffer, long offset, object value, Type type, FieldSpecsAttribute fieldSpecs, object parent, Endianness endianness)
        {
            if (type == typeof(byte)) {
                buffer[offset++] = Convert.ToByte(value); return 1;
            } else if (type == typeof(sbyte)) {
                buffer[offset++] = Convert.ToByte(value); return 1;
            } else if (type == typeof(Int16)) {
                return buffer.WriteVal(offset, Convert.ToInt16(value), endianness);
            } else if (type == typeof(UInt16)) {
                return buffer.WriteVal(offset, Convert.ToUInt16(value), endianness);
            } else if (type == typeof(Int32)) {
                return buffer.WriteVal(offset, Convert.ToInt32(value), endianness);
            } else if (type == typeof(UInt32)) {
                return buffer.WriteVal(offset, Convert.ToUInt32(value), endianness);
            } else if (type == typeof(Int64)) {
                return buffer.WriteVal(offset, Convert.ToInt64(value), endianness);
            } else if (type == typeof(UInt64)) {
                return buffer.WriteVal(offset, Convert.ToUInt64(value), endianness);
            } else if (type == typeof(float)) {
                return buffer.WriteVal(offset, Convert.ToSingle(value), endianness);
            } else if (type == typeof(double)) {
                return buffer.WriteVal(offset, Convert.ToDouble(value), endianness);
            } else if (type == typeof(DateTime)) {
                return buffer.WriteVal(offset, Convert.ToDateTime(value), fieldSpecs.DateFormat, endianness);
            } else if (type == typeof(string)) {
                return buffer.WriteVal(offset, (string)value, fieldSpecs.StringFormat, endianness);
            } else if (type == typeof(Guid)) {
                return buffer.WriteVal(offset, (Guid)value);
            } else if (type.IsEnum) {
                var enumType = type.GetCustomAttribute<EnumType>()?.Type ?? typeof(Int32);
                return buffer.WriteVal(offset, Convert.ChangeType(value, enumType), enumType, fieldSpecs, parent, endianness);
            } else if (type.IsArray) {
                type = type.GetElementType();
                if (type == typeof(Int16)) {
                    return buffer.WriteVal(offset, (Int16[])value, endianness);
                } else if (type == typeof(UInt16)) {
                    return buffer.WriteVal(offset, (UInt16[])value, endianness);
                } else if (type == typeof(Int32)) {
                    return buffer.WriteVal(offset, (Int32[])value, endianness);
                } else if (type == typeof(UInt32)) {
                    return buffer.WriteVal(offset, (UInt32[])value, endianness);
                } else if (type == typeof(Int64)) {
                    return buffer.WriteVal(offset, (Int64[])value, endianness);
                } else if (type == typeof(UInt64)) {
                    return buffer.WriteVal(offset, (UInt64[])value, endianness);
                }
                long elementSize = 0;
                if (fieldSpecs?.ElementSize != null)
                    elementSize = Convert.ToInt64(parent.GetType().GetField(fieldSpecs.ElementSize).GetValue(parent));
                return buffer.WriteVal(offset, (Array)value, elementSize, endianness);
            }

            // Since none of the above types applied, we assume this is a custom class or struct

            if (type.GetCustomAttribute<TypeSpecs>()?.WalkBaseType ?? false)
                offset += buffer.WriteVal(offset, value, type.BaseType, fieldSpecs, parent, endianness);

            long size = 0;
            long unionSize = 0;

            foreach (var field in type.GetFields(bindingFlags | BindingFlags.DeclaredOnly)) {
                var fieldAttr = field.GetCustomAttribute<FieldSpecsAttribute>();
                if (fieldAttr?.Ignore ?? false)
                    continue;
                if (field.Name.Contains("<")) // ignore compiler generated values (hacky)
                    continue;

                var fieldVal = (fieldAttr?.LengthOf == null ? field.GetValue(value) : LengthOf(type.GetField(fieldAttr.LengthOf, bindingFlags).GetValue(value)));

                if (fieldAttr?.Optional ?? false && fieldVal == null)
                    continue;

                var length = buffer.WriteVal(offset + size, fieldVal, field.FieldType, fieldAttr, value, endianness);

                if (fieldAttr?.Union ?? false) {
                    unionSize = Math.Max(unionSize, length);
                    if (fieldAttr.EndOfUnion) {
                        size += unionSize;
                        unionSize = 0;
                    }
                } else {
                    size += length;
                }
            }

            return size;
        }

        public static byte[] WriteVal<T>(T value, Endianness endianness = Endianness.Unspecified)
        {
            var buffer = new byte[SizeOf(value)];
            buffer.WriteVal(0, value, endianness);
            return buffer;
        }

        #endregion


        #region Size Calculation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(byte value)
        {
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(sbyte value)
        {
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(Int16 value)
        {
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(UInt16 value)
        {
            return 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(Int32 value)
        {
            return 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(UInt32 value)
        {
            return 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(Int64 value)
        {
            return 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(UInt64 value)
        {
            return 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(float value)
        {
            return 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(double value)
        {
            return 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(DateTime value, DateFormat format)
        {
            switch (format) {
                case DateFormat.NTFS: return 8;
                default: throw new NotSupportedException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(string value, StringFormat format)
        {
            switch (format) {
                case StringFormat.Unicode: return value.Length * 2;
                case StringFormat.ASCII: return value.Length;
                default: throw new NotSupportedException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(Guid value)
        {
            return 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SizeOf(Array value)
        {
            long sum = 0;
            var elementType = value.GetType().GetElementType();
            foreach (var val in value) {
                sum += SizeOf(val, elementType, null, null);
            }
            return sum;
        }

        public static long SizeOf<T>(T value)
        {
            return SizeOf(value, typeof(T), null, null);
        }

        private static int LengthOf(object value)
        {
            if (value == null)
                throw new ArgumentNullException($"{value}");

            var str = value as string;
            if (str != null)
                return str.Length;
            return ((Array)value).Length;
        }

        /// <summary>
        /// Returns the effective number of bytes required by object to be written using WriteVal.
        /// </summary>
        /// <param name="value">The value to be measured.</param>
        /// <param name="fieldSpecs">Can be null for any type other than: DateTime, string</param>
        /// <exception cref="NotSupportedException">An unsupported type was encountered.</exception>
        public static long SizeOf(object value, Type type, FieldSpecsAttribute fieldSpecs, object parent)
        {
            if (type == typeof(byte) || type == typeof(sbyte)) {
                return 1;
            } else if (type == typeof(Int16) || type == typeof(UInt16)) {
                return 2;
            } else if (type == typeof(Int32) || type == typeof(UInt32) || type == typeof(float)) {
                return 4;
            } else if (type == typeof(Int64) || type == typeof(UInt64) || type == typeof(double)) {
                return 8;
            } else if (type == typeof(DateTime)) {
                return SizeOf(Convert.ToDateTime(value), fieldSpecs.DateFormat);
            } else if (type == typeof(string)) {
                return SizeOf((string)value, fieldSpecs.StringFormat);
            } else if (type == typeof(Guid)) {
                return 16;
            } else if (type.IsEnum) {
                var enumType = type.GetCustomAttribute<EnumType>()?.Type ?? typeof(Int32);
                return SizeOf(Convert.ChangeType(value, enumType), enumType, fieldSpecs, parent);
            } else if (type.IsArray) {
                type = type.GetElementType();
                if (type == typeof(byte) || type == typeof(sbyte)) {
                    return ((Array)value).Length;
                } else if (type == typeof(Int16) || type == typeof(UInt16)) {
                    return ((Array)value).Length * 2;
                } else if (type == typeof(Int32) || type == typeof(UInt32) || type == typeof(float)) {
                    return ((Array)value).Length * 4;
                } else if (type == typeof(Int64) || type == typeof(UInt64) || type == typeof(double)) {
                    return ((Array)value).Length * 8;
                }
                if (fieldSpecs?.ElementSize != null)
                    return ((Array)value).Length * Convert.ToInt64(parent.GetType().GetField(fieldSpecs.ElementSize).GetValue(parent));
                return SizeOf((Array)value);
            }

            // Since none of the above types applied, we assume this is a custom class or struct

            long size = 0;
            long unionSize = 0;

            if (type.GetCustomAttribute<TypeSpecs>()?.WalkBaseType ?? false)
                size += SizeOf(value, type.BaseType, fieldSpecs, parent);

            foreach (var field in type.GetFields(bindingFlags | BindingFlags.DeclaredOnly)) {
                var fieldAttr = field.GetCustomAttribute<FieldSpecsAttribute>();
                if (fieldAttr?.Ignore ?? false)
                    continue;
                if (field.Name.Contains("<")) // ignore compiler generated values (hacky)
                    continue;

                var fieldVal = (fieldAttr?.LengthOf == null ? field.GetValue(value) : LengthOf(type.GetField(fieldAttr.LengthOf, bindingFlags).GetValue(value)));

                if (fieldAttr?.Optional ?? false && fieldVal == null)
                    continue;

                var length = SizeOf(fieldVal, field.FieldType, fieldAttr, value);

                if (fieldAttr?.Union ?? false) {
                    unionSize = Math.Max(unionSize, length);
                    if (fieldAttr.EndOfUnion) {
                        size += unionSize;
                        unionSize = 0;
                    }
                } else {
                    size += length;
                }
            }

            return size;
        }

        #endregion


        #region Checksum Calculation

        /// <summary>
        /// Returns the 16-bit sum of a range in a byte array.
        /// </summary>
        public static Int16 GetInt16Checksum(this byte[] buffer, long offset, long length)
        {
            Int16 result = 0;
            for (long i = offset; i < offset + length; i++)
                result = unchecked((Int16)(result + buffer[i]));
            return result;
        }

        /// <summary>
        /// Returns the 32-bit sum of a range in a byte array.
        /// </summary>
        public static Int32 GetInt32Checksum(this byte[] buffer, long offset, long length)
        {
            Int32 result = 0;
            for (long i = offset; i < offset + length; i++)
                result = unchecked((Int32)(result + buffer[i]));
            return result;
        }

        /// <summary>
        /// Validates a 16-bit checksum. The 16-bit number at the specified offset must equal the one's complement of the sum of the rest of the bytes.
        /// </summary>
        public static bool ValidateInt16Checksum(this byte[] buffer, long offset, int length, long checksumOffset, Endianness endianness)
        {
            Int16 sum = buffer.GetInt16Checksum(offset, length);
            for (long i = checksumOffset; i < checksumOffset + 2; i++)
                sum = unchecked((Int16)(sum - buffer[i]));
            return sum == ~buffer.ReadInt16(ref checksumOffset, endianness);
        }

        /// <summary>
        /// Validates a 32-bit checksum. The 32-bit number at the specified offset must equal the one's complement of the sum of the rest of the bytes.
        /// </summary>
        public static bool ValidateInt32Checksum(this byte[] buffer, long offset, int length, long checksumOffset, Endianness endianness)
        {
            Int32 sum = buffer.GetInt32Checksum(offset, length);
            for (long i = checksumOffset; i < checksumOffset + 4; i++)
                sum = unchecked((Int32)(sum - buffer[i]));
            return sum == ~buffer.ReadInt32(ref checksumOffset, endianness);
        }

        #endregion
    }
}

