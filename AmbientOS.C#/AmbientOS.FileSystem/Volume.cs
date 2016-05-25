using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.FileSystem
{
    class Volume : IVolumeImpl
    {
        public DynamicEndpoint<Guid> ID { get; }
        public DynamicEndpoint<FileSystemFlags> Flags { get; }
        public DynamicEndpoint<string> Type { get; }
        public DynamicEndpoint<long?> Length { get; }

        readonly VolumeExtent[] extents;

        /// <summary>
        /// Bytes-per-sector metric of each extent
        /// </summary>
        readonly long[] blockSizes;

        /// <summary>
        /// Length in bytes of each extent
        /// </summary>
        readonly long[] extentLengths;

        /// <summary>
        /// Generates a new volume that consists of one or multiple extents.
        /// The extents can be on different disks.
        /// The read and write methods automatically operate on the correct extent(s).
        /// </summary>
        public Volume(Guid id, FileSystemFlags flags, string type, params VolumeExtent[] extents)
        {
            this.extents = extents;

            blockSizes = extents.Select(e => e.Parent.BlockSize.GetValue()).ToArray();
            extentLengths = extents.Select((e, i) => e.Blocks * blockSizes[i]).ToArray();

            ID = new DynamicEndpoint<Guid>(id, PropertyAccess.ReadOnly);
            Flags = new DynamicEndpoint<FileSystemFlags>(flags, PropertyAccess.ReadOnly);
            Type = new DynamicEndpoint<string>(type, PropertyAccess.ReadOnly);
            Length = new DynamicEndpoint<long?>(() => extentLengths.Sum(), val => { throw new NotImplementedException(); });
        }

        public VolumeExtent[] GetExtents()
        {
            return extents.RetainAll();
        }

        private void DoOperation(long offset, long count, byte[] buffer, long bufferOffset, bool read)
        {
            for (int i = 0; count > 0;) {
                if (i >= extents.Count())
                    throw new Exception("Attempt to read beyond the volume");

                // skip extent as long as the offset is not within
                if (offset >= extentLengths[i]) {
                    offset -= extentLengths[i++];
                    continue;
                }

                var extent = extents[i];
                long effectiveCount;

                var block = extent.StartBlock + offset / blockSizes[i];
                var offsetInBlock = offset % blockSizes[i];

                if (offsetInBlock != 0 || count < blockSizes[i]) {
                    // case 1: the operation is not sector aligned or less than a full sector
                    // in this case, we also have to read the sector when writing, as not to lose some of the data on the sector
                    effectiveCount = Math.Min(count, blockSizes[i] - offsetInBlock);
                    var temp = new byte[blockSizes[i]];
                    extent.Parent.ReadBlocks(block, 1, temp, 0);

                    if (read) {
                        Array.Copy(temp, offsetInBlock, buffer, bufferOffset, effectiveCount);
                    } else {
                        Array.Copy(buffer, bufferOffset, temp, offsetInBlock, effectiveCount);
                        extent.Parent.WriteBlocks(block, 1, temp, 0);
                    }
                } else {
                    // case 2: the operation is sector-aligned and at least as long as a sector
                    var blockCount = Math.Min(count, extentLengths[i] - offset) / blockSizes[i];
                    if (read)
                        extent.Parent.ReadBlocks(block, blockCount, buffer, bufferOffset);
                    else
                        extent.Parent.WriteBlocks(block, blockCount, buffer, bufferOffset);
                    effectiveCount = blockCount * blockSizes[i];
                }

                count -= effectiveCount;
                offset += effectiveCount;
                bufferOffset += effectiveCount;
            }
        }

        public void Read(long offset, long count, byte[] buffer, long bufferOffset)
        {
            DoOperation(offset, count, buffer, bufferOffset, true);
        }

        public void Write(long offset, long count, byte[] buffer, long bufferOffset)
        {
            DoOperation(offset, count, buffer, bufferOffset, false);
        }

        public void ChangeSize(long sectorCount)
        {
            throw new NotImplementedException();
        }

        public void Flush()
        {
            foreach (var extent in extents)
                extent.Parent.Flush();
        }
    }
}
