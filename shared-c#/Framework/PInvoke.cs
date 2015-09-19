using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace AppInstall.Framework
{
    public class UnmanagedMemory : IDisposable
    {
        /// <summary>
        /// A pointer to the block of unmanaged memory
        /// </summary>
        public IntPtr Handle { get; private set; }
        /// <summary>
        /// The size of this unmanaged memory block
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Allocates a block of unmanaged memory
        /// </summary>
        public UnmanagedMemory(int count)
        {
            Handle = Marshal.AllocHGlobal(count);
        }
        /// <summary>
        /// Copies a byte array into a new block of unmanaged memory
        /// </summary>
        public UnmanagedMemory(byte[] data)
            : this(data.Count())
        {
            Marshal.Copy(data, 0, Handle, data.Count());
        }
        /// <summary>
        /// Generates a byte array from the block of unmanaged memory
        /// </summary>
        public byte[] ToArray()
        {
            byte[] result = new byte[Count];
            Marshal.Copy(Handle, result, 0, Count);
            return result;
        }
        /// <summary>
        /// Frees the block of unmanaged memory
        /// </summary>
        public void Dispose()
        {
            Marshal.FreeHGlobal(Handle);
            Handle = IntPtr.Zero;
        }
    }
}