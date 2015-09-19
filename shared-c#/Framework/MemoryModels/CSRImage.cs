using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace AppInstall.Framework.MemoryModels
{
    /// <summary>
    /// Reads/writes the firmware file format for Cambridge Silicon Radio (CSR) chips
    /// </summary>
    class CSRImage : IMemoryModel<byte>
    {
        private const long MAX_LENGTH = 65536; // memory size of CSR1010

        private long count;
        private byte[] data = new byte[MAX_LENGTH];



        #region "IMemoryModel implementation"
        public long LowestAddress { get { return 0; } }

        public long HighestAddress { get { return count - 1; } }

        public byte[] Read(long startAddress, long endAddress)
        {
            byte[] destination = new byte[endAddress - startAddress + 1];
            Array.Copy(data, startAddress, destination, 0, endAddress - startAddress + 1);
            return destination;
        }
        #endregion




        /// <summary>
        /// Creates an instance of a CSR firmware image by loading a file from disk
        /// </summary>
        /// <param name="maxSize">The mac</param>
        /// <param name="path"></param>
        public CSRImage(string path)
        {
            for (int i = 0; i < data.Count(); i++)
                data[i] = 0xFF; // fill empty space

            Load(path);
        }


        /// <summary>
        /// Loads a CSR image file from disk
        /// </summary>
        /// <exception cref="FormatException">The image file is invalid</exception>
        private void Load(string path)
        {
            count = 0;

            using (StreamReader s = new StreamReader(path)) {
                while (!s.EndOfStream) {
                    string line = s.ReadLine().Trim();
                    try {
                        if (line.StartsWith("@")) {
                            string[] words = GetWords(line).ToArray();
                            if (words.Count() != 2) throw new FormatException("invalid word count");
                            long address = Convert.ToInt64(words[0].Substring(1), 16);
                            int dataByte = Convert.ToInt32(words[1], 16);
                            if ((words[1].Length & 1) != 0) throw new FormatException("invalid data field");
                            int dataSize = words[1].Length / 2;

                                while (dataSize-- != 0) {
                                    //Data[address++] = (byte)((data >> (dataSize * 8)) & 0xFF);
                                    data[address++] = (byte)(dataByte & 0xFF);
                                    dataByte >>= 8;
                                }
                                if (address > count) count = address;
                        } else if (line.StartsWith("//") || line == "") {
                            // comment line
                        } else {
                            throw new FormatException("unknown line");
                        }
                    } catch (Exception ex) {
                        throw new FormatException("failed on line \"" + line + "\": " + ex.Message);
                    }
                }
            }
        }


        public static IEnumerable<string> GetWords(string str)
        {
            return (from s in str.Split(' ').Where((s) => !string.IsNullOrWhiteSpace(s)) select s.Trim());
        }
    }
}
