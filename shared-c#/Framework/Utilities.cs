
// external references:
// System.XML

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AppInstall.Framework
{
    public static class Utilities
    {
        /// <summary>
        /// Does work on a dataset by partitioning it into several parts of equal size (except the last). Each partition is guaranteed to be of non-zero size.
        /// </summary>
        /// <param name="action">The action that accepts the offset into the data and the partition size as arguments</param>
        public static async Task PartitionWork(int start, int count, int partitionSize, Func<int, int, Task> action)
        {
            if (count == 0) return;
            int i;
            for (i = start; i < start + count - partitionSize; i += partitionSize)
                await action(i, partitionSize);
            await action(i, count - i);
        }

        /// <summary>
        /// If required, creates a directory and its missing parent directories.
        /// </summary>
        public static void CreateDirectory(string path)
        {
            if (Directory.Exists(path)) return;
            CreateDirectory(Directory.GetParent(path).FullName);
            Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Deletes the specified directory and its parent directories recursively as long as they are empty
        /// </summary>
        public static void DeleteEmptyDirectory(string path)
        {
            while (true) {
                if (Directory.GetFiles(path).Count() > 0) return;
                if (Directory.GetDirectories(path).Count() > 0) return;
                Directory.Delete(path);
                path = Directory.GetParent(path).FullName;
            }
        }

        /// <summary>
        /// Logs the top-level contents of a direcory (for debugging).
        /// </summary>
        public static void DumpDir(string directory, LogContext logContext)
        {
            try {
                logContext.Log(directory + ":");
                foreach (var dir in Directory.EnumerateDirectories(directory)) {
                    logContext.Log("dir " + dir);
                }
                foreach (var file in Directory.EnumerateFiles(directory)) {
                    logContext.Log("file " + file);
                }
            } catch (Exception ex) {
                logContext.Log(ex.ToString());
            }
        }

        /// <summary>
        /// Determines if the two paths point to the same location. Does not work for different root naming schemes.
        /// </summary>
        public static bool PathsEqual(string path1, string path2)
        {
            return string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2));
        }

        /// <summary>
        /// Merges two string columns to one column while ensuring that the second one is justified on the left side
        /// </summary>
        public static IEnumerable<string> MergeColumns(IEnumerable<Tuple<string, string>> columns, string seperator)
        {
            int c1Width = (from c in columns select c.Item1.Length).Max();
            return (from c in columns select c.Item1 + seperator + new string(' ', c1Width - c.Item1.Length) + c.Item2);
        }


        /// <summary>
        /// Deserializes the object of type T that is stored in an XML document described by the buffer. Returns default(T) if buffer is null or empty.
        /// </summary>
        public static T XMLDeserialize<T>(byte[] buffer)
        {
            if (buffer == null) return default(T);
            if (buffer.Count() == 0) return default(T);
            using (MemoryStream stream = new MemoryStream(buffer))
                return XMLDeserialize<T>(stream);
        }

        /// <summary>
        /// Deserializes the object of type T by reading an XML document from the stream
        /// </summary>
        public static T XMLDeserialize<T>(Stream stream)
        {
            XmlSerializer ser = new XmlSerializer(typeof(T));
            return (T)ser.Deserialize(stream);
        }

        /// <summary>
        /// Serializes an object of type T into an XML document and writes it to the stream.
        /// </summary>
        public static void XMLSerialize<T>(T obj, Stream stream)
        {
            XmlSerializer ser = new XmlSerializer(typeof(T));
            ser.Serialize(stream, obj);
        }

        /// <summary>
        /// Serializes an object of type T into an XML document and returns the resulting buffer.
        /// </summary>
        public static byte[] XMLSerialize<T>(T obj)
        {
            using (MemoryStream stream = new MemoryStream()) {
                XMLSerialize(obj, stream);
                return stream.GetBuffer();
            }
        }

        /// <summary>
        /// Returns the int value associated with an enum value
        /// </summary>
        public static int EnumToInt<T>(T value) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum) throw new InvalidCastException("T must be of an enum type, have " + typeof(T));
            return (int)(object)value;
        }

        /// <summary>
        /// Returns the human readable name (with inserted spaces) of the enum value
        /// </summary>
        public static string EnumToString<T>(T value) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum) throw new InvalidCastException("T must be of an enum type, have " + typeof(T));
            return Enum.GetName(typeof(T), value).CamelCaseToNormal();
        }

        /// <summary>
        /// Converts the name or number of an integer into the integer value.
        /// </summary>
        public static T StringToEnum<T>(string value) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum) throw new InvalidCastException("T must be of an enum type, have " + typeof(T));
            return (T)Enum.Parse(typeof(T), value);
        }

        /// <summary>
        /// Tries to return a string that represents the devices MAC address.
        /// Prefers operational interfaces and returns six zero-bytes if no interface is installed.
        /// </summary>
        public static byte[] GetMachineID()
        {
            var secondary = new byte[] { 0, 0, 0, 0, 0, 0 };
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    return nic.GetPhysicalAddress().GetAddressBytes();
                else
                    secondary = nic.GetPhysicalAddress().GetAddressBytes();
            return secondary;
        }

        /// <summary>
        /// Tries to parse a guid and returns null in case of an error.
        /// </summary>
        public static Guid? TryParseGuid(string guid)
        {
            Guid result;
            if (Guid.TryParse(guid, out result)) return result;
            return null;
        }


        private static Random random = new Random();
        public static Random Random { get { return random; } }

        /// <summary>
        /// Generates a sequence of random length of elements provided by a constructor
        /// </summary>
        public static T[] RandomSequence<T>(int maxLength, Func<T> constructor)
        {
            int length = Random.Next(maxLength);
            var result = new T[length];
            for (int i = 0; i < length; i++)
                result[i] = constructor();
            return result;
        }

        public static T RandomElement<T>(IList<T> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");
            var count = list.Count();
            if (count <= 0)
                throw new ArgumentException("the list must not be empty", "list");
            return list.ElementAt(Random.Next(list.Count() - 1));
        }
    }
}