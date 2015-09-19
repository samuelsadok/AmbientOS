using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace AppInstall.OS
{
    class PInvoke
    {

        #region "Utils"

        public class Win32Handle : IDisposable
        {
            private IntPtr myHandle = INVALID_HANDLE_VALUE;
            public IntPtr h { get { return myHandle; } }

            /// <summary>
            /// Creates a new managed Handle instance. If the handle is invalid, the last Win32 exception is thrown
            /// </summary>
            public Win32Handle(IntPtr h)
            {
                if (h == INVALID_HANDLE_VALUE) Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                myHandle = h;
            }
            public void Dispose()
            {
                CloseHandle(myHandle);
            }
        }

        // taken from Microsoft.Win32.SafeHandles
        public abstract class SafeHandleZeroOrMinusOneIsInvalid : SafeHandle
        {
            
            protected SafeHandleZeroOrMinusOneIsInvalid(bool ownsHandle)
                : base(IntPtr.Zero, ownsHandle)
            {
            }

#if FEATURE_CORECLR
        // A default constructor is needed to satisfy CoreCLR inheritence rules. It should not be called at runtime
        protected SafeHandleZeroOrMinusOneIsInvalid()
        {
            throw new NotImplementedException();
        }
#endif // FEATURE_CORECLR

            public override bool IsInvalid
            {
                [System.Security.SecurityCritical]
                get { return handle == IntPtr.Zero || handle == new IntPtr(-1); }
            }
        }


        // taken from Microsoft.Win32.SafeHandles
        [System.Security.SecurityCritical]
        public sealed class SafeFileHandle : SafeHandleZeroOrMinusOneIsInvalid
        {

            private SafeFileHandle()
                : base(true)
            {
            }

            public SafeFileHandle(IntPtr preexistingHandle, bool ownsHandle)
                : base(ownsHandle)
            {
                SetHandle(preexistingHandle);
            }

            [System.Security.SecurityCritical]
            override protected bool ReleaseHandle()
            {
                return CloseHandle(handle);
            }
        }


        public class Win32Memory : IDisposable
        {
            private IntPtr handle;
            private int size;

            public IntPtr Pointer { get { return handle; } }
            public int Size { get { return size; } }

            /// <summary>
            /// Allocates a new memory block that will be accessible to unmanaged code.
            /// </summary>
            public Win32Memory(int size)
            {
                handle = Marshal.AllocHGlobal(size);
                if (handle == IntPtr.Zero) throw new OutOfMemoryException();
                this.size = size;
            }
            public void Dispose()
            {
                Marshal.FreeHGlobal(handle);
            }
        }

        public class Win32Event : IDisposable
        {
            private IntPtr handle;
            private int size;

            public IntPtr Handle { get { return handle; } }

            /// <summary>
            /// Creates a new managed Event instance.
            /// </summary>
            public Win32Event()
            {
                handle = CreateEvent(IntPtr.Zero, false, false, "RandomEventName");
                if (handle == IntPtr.Zero) Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            /// <summary>
            /// Waits for the event to be signalled
            /// </summary>
            /// <param name="timeout">Timeout in ms. -1 means infinity</param>
            public void Wait(int timeout)
            {
                switch (WaitForSingleObject(handle, timeout))
                {
                    case 0: return;
                    case 0x80: throw new OperationCanceledException();  // the thread owning the event handle terminated
                    case 0x102: throw new TimeoutException();
                    default: Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error()); break;
                }
            }

            public void Dispose()
            {
                CloseHandle(handle);
            }
        }

        #endregion


        #region "Constants"

        public const int ERROR_INSUFFICIENT_BUFFER = 0x7A;

        public static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        enum ERROR
        {
            SUCCESS = 0,
            IO_PENDING = 997
        }

        public enum Access : uint
        {
            GENERIC_ALL         = 0x10000000,
            GENERIC_READ        = 0x80000000,
            GENERIC_WRITE       = 0x40000000,
            GENERIC_EXECUTE     = 0x20000000
        }

        public enum ShareMode
        {
            FILE_SHARE_READ = 0x1,
            FILE_SHARE_WRITE = 0x2,
            FILE_SHARE_DELETE = 0x4
        }

        public enum CreationDisposition
        {
            CREATE_NEW = 1,
            CREATE_ALWAYS = 2,
            OPEN_EXISTING = 3,
            OPEN_ALWAYS = 4,
            TRUNCATE_EXISTING = 5
        }

        public enum FileFlags
        {
            OVERLAPPED = 0x40000000
        }


        public enum IOCTL_BASE
        {
            DISK = 7,
            FILE_SYSTEM = 9,
            MASS_STORAGE = 45
        }

        public enum IOCTL_METHOD
        {
            BUFFERED = 0,
            IN_DIRECT = 1,
            OUT_DIRECT = 2,
            NEITHER = 3
        }

        public enum IOCTL_FILE_ACCESS
        {
            ANY = 0,
            READ = 1,
            WRITE = 2
        }

        private static int CTL_CODE(IOCTL_BASE t, int f, IOCTL_METHOD m, IOCTL_FILE_ACCESS a)
        {
            return ((((int)t) << 16) | (((int)a) << 14) | ((f) << 2) | ((int)m));
        }

        public static int IOCTL_DISK_GET_DRIVE_GEOMETRY = CTL_CODE(IOCTL_BASE.DISK, 0, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static int FSCTL_DISMOUNT_VOLUME = CTL_CODE(IOCTL_BASE.FILE_SYSTEM, 8, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);

        #endregion


        #region "Structures"

        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct Overlapped {
            Int64 Internal;
            public Int64 Offset;
            public IntPtr hEvent;
            public Overlapped(long offset, IntPtr hEvent) {
                Internal = 0;
                Offset = offset;
                this.hEvent = hEvent;
            }
        }

        #endregion


        #region "Function Definitions"
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int QueryDosDevice(string lpDeviceName, IntPtr lpTargetPath, int ucchMax);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Boolean DeviceIoControl(SafeFileHandle hDevice, Int32 dwIoControlCode, IntPtr lpInBuffer, Int32 nInBufferSize, IntPtr lpOutBuffer, Int32 nOutBufferSize, ref Int32 lpBytesReturned, ref Overlapped lpOverlapped);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //static extern IntPtr CreateFile(string lpFileName, Int32 dwDesiredAccess, Int32 dwShareMode, IntPtr lpSecurityAttributes, Int32 dwCreationDisposition, Int32 dwFlagsAndAttributes, IntPtr hTemplateFile);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
        static extern SafeFileHandle CreateFile(string lpFileName, Int32 dwDesiredAccess, Int32 dwShareMode, IntPtr lpSecurityAttributes, Int32 dwCreationDisposition, Int32 dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Boolean ReadFile(SafeFileHandle hFile, byte[] buffer, Int32 nNumberOfBytesToRead, IntPtr lpNumberOfBytesRead, ref Overlapped lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Boolean ReadFile(SafeFileHandle hFile, IntPtr buffer, Int32 nNumberOfBytesToRead, ref int lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Boolean ReadFile(SafeFileHandle hFile, IntPtr buffer, Int32 nNumberOfBytesToRead, IntPtr lpNumberOfBytesRead, ref Overlapped lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Boolean WriteFile(SafeFileHandle hFile, byte[] buffer, Int32 nNumberOfBytesToWrite, IntPtr lpNumberOfBytesWritten, ref Overlapped lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Boolean WriteFile(SafeFileHandle hFile, IntPtr buffer, Int32 nNumberOfBytesToWrite, IntPtr lpNumberOfBytesWritten, ref Overlapped lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateEvent(IntPtr lpEventAttributes, Boolean bManualReset, Boolean bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Int32 WaitForSingleObject(IntPtr hHandle, Int32 dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Boolean GetOverlappedResult(SafeFileHandle hFile, ref Overlapped lpOverlapped, ref Int32 lpNumberOfBytesTransferred, Boolean bWait);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr h);
        #endregion


        #region "Function Wrappers"

        public static string[] QueryDosDevice(string device)
        {
            int returnSize = 0;
            int maxResponseSize = 100; // Arbitrary initial buffer size

            while (true) {
                // Allocate response buffer for native call
                using (Win32Memory response = new Win32Memory(maxResponseSize)) {
                    // List DOS devices
                    returnSize = QueryDosDevice(device, response.Pointer, maxResponseSize);

                    // List success
                    if (returnSize != 0)
                        return Marshal.PtrToStringAnsi(response.Pointer, maxResponseSize).Substring(0, returnSize - 2).Split('\0');
                    else if (Marshal.GetLastWin32Error() == PInvoke.ERROR_INSUFFICIENT_BUFFER)
                        maxResponseSize = (int)(maxResponseSize * 5); // The response buffer is too small, reallocate it exponentially and retry
                    else
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
            }
        }

        public static void DeviceIoControl(SafeFileHandle device, Int32 ioControlCode, Win32Memory inBuffer, Win32Memory outBuffer, ref Int32 bytesReturned, Overlapped overlapped)
        {
            bool success = DeviceIoControl(device, ioControlCode,
                            ((inBuffer == null) ? IntPtr.Zero : inBuffer.Pointer), ((inBuffer == null) ? 0 : inBuffer.Size),
                            ((outBuffer == null) ? IntPtr.Zero : outBuffer.Pointer), ((outBuffer == null) ? 0 : outBuffer.Size),
                            ref bytesReturned,
                            ref overlapped);

            if (!success) Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }


        public static SafeFileHandle CreateFile(string file, Access access, ShareMode shareMode, IntPtr securityAttributes, CreationDisposition creationDisposition, FileFlags flags, IntPtr template)
        {
            //file = "C:\\Developer\\bla.txt";
            //return new Win32Handle(CreateFile(device, (Int32)access, (Int32)shareMode, securityAttributes, (Int32)creationDisposition, (Int32)flags, template));
            var result = CreateFile(file, (Int32)access, (Int32)shareMode, securityAttributes, (Int32)creationDisposition, (Int32)flags, template);
            if (result.IsInvalid)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            //var b = new byte[100];
            //ReadFile(result, 0, b, 0, 5);
            return result;
            //return new Win32Handle();
        }

        /// <summary>
        /// Reads data from a file
        /// </summary>
        /// <param name="file"></param>
        /// <param name="fileOffset">The offset within the file</param>
        /// <param name="buffer">The buffer to read into</param>
        /// <param name="bufferOffset">An offset into the buffer where to place the data</param>
        public static void ReadFile(SafeFileHandle file, long fileOffset, byte[] buffer, int bufferOffset, int count)
        {
            int bytesRead = 0;
            using (Win32Event e = new Win32Event())
            {
                //Console.WriteLine("read " + buffer.Count() + " bytes at " + offset);
                Overlapped overlapped = new Overlapped(fileOffset, e.Handle);
                bool success = ReadFile(file, Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferOffset), count, IntPtr.Zero, ref overlapped);
                if (!success && (Marshal.GetLastWin32Error() != (int)ERROR.IO_PENDING))
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                e.Wait(-1);
                success = GetOverlappedResult(file, ref overlapped, ref bytesRead, false);
                if (!success) Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                if (bytesRead < count) throw new Exception("not all bytes read (should read " + buffer.Count() + ", did read " + bytesRead + ")");
            }
        }

        public static void WriteFile(SafeFileHandle file, byte[] buffer, long offset)
        {
            int bytesWritten = 0;
            using (Win32Event e = new Win32Event())
            {
                Console.WriteLine("write " + buffer.Count() + " bytes at " + offset);
                Overlapped overlapped = new Overlapped(offset, e.Handle);
                bool success = WriteFile(file, buffer, buffer.Count(), IntPtr.Zero, ref overlapped);
                if (!success && (Marshal.GetLastWin32Error() != (int)ERROR.IO_PENDING)) Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                e.Wait(-1);
                success = GetOverlappedResult(file, ref overlapped, ref bytesWritten, false);
                if (!success) Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                if (bytesWritten < (int)buffer.Count()) throw new Exception("not all bytes read (should read " + buffer.Count() + ", did read " + bytesWritten + ")");
            }
        }

        #endregion


    }
}
