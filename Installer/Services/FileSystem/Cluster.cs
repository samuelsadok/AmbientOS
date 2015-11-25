using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.FileSystem
{
    class Cluster
    {
        public long VCN;
        public long LCN;
        public byte[] data;
        public bool valid;
        public bool dirty;

        /// <summary>
        /// Returns a hash code based on the cluster's VCN.
        /// This makes sense as the cluster cache is separate for each non-resident attribute.
        /// </summary>
        public override int GetHashCode()
        {
            return VCN.GetHashCode(); // hash of a Int64 is the upper 32 bits XOR the lower 32 bits
        }

        public static byte[] ReadBytes(Cluster[] clusters, long offset, long length)
        {
            var buffer = new byte[length];
            long bufferOffset = 0;
            for (int i = 0; bufferOffset < length; i++) {
                if (i >= clusters.Count())
                    throw new Exception("attempt to read beyond cluster list");

                if (offset < clusters[i].data.Length) {
                    long bytesToRead = Math.Min(clusters[i].data.Length - offset, length - bufferOffset);
                    Array.Copy(clusters[i].data, offset, buffer, bufferOffset, bytesToRead);
                    bufferOffset += bytesToRead;
                    offset = 0;
                } else {
                    offset -= clusters[i].data.Length;
                }
            }
            return buffer;
        }

        public static void WriteBytes(Cluster[] clusters, long offset, byte[] buffer)
        {
            long bufferOffset = 0;
            for (int i = 0; bufferOffset < buffer.Count(); i++) {
                if (i >= clusters.Count())
                    throw new Exception("attempt to write beyond cluster list");

                if (offset < clusters[i].data.Length) {
                    long bytesToWrite = Math.Min(clusters[i].data.Length - offset, buffer.Count() - bufferOffset);
                    Array.Copy(clusters[i].data, offset, buffer, bufferOffset, bytesToWrite);
                    bufferOffset += bytesToWrite;
                    offset = 0;
                } else {
                    offset -= clusters[i].data.Length;
                }
            }
        }
    }
}
