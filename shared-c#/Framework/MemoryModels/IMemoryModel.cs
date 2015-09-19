using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.Framework.MemoryModels
{
    public interface IMemoryModel<T>
    {
        /// <summary>
        /// Returns the lowest address that contains data
        /// </summary>
        long LowestAddress { get; }
        /// <summary>
        /// Returns the highest address that contains data
        /// </summary>
        long HighestAddress { get; }

        /// <summary>
        /// Reads a block of memory
        /// </summary>
        byte[] Read(long startAddress, long endAddress); // todo: define behaviour when memory outside the range is accessed
    }
}
