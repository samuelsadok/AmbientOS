using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;


namespace AmbientOS
{
    /*
    //public class Win32Handle : IDisposable
    //{
    //    private IntPtr myHandle = INVALID_HANDLE_VALUE;
    //    public IntPtr h { get { return myHandle; } }
    //
    //    /// <summary>
    //    /// Creates a new managed Handle instance. If the handle is invalid, the last Win32 exception is thrown.
    //    /// </summary>
    //    public Win32Handle(IntPtr h)
    //    {
    //        if (h == INVALID_HANDLE_VALUE)
    //            throw new Win32Exception();
    //        myHandle = h;
    //    }
    //    public void Dispose()
    //    {
    //        CloseHandle(myHandle);
    //    }
    //}

    // taken from Microsoft.Win32.SafeHandles
    abstract class SafeHandleZeroOrMinusOneIsInvalid : SafeHandle
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
            get
            { return handle == IntPtr.Zero || handle == new IntPtr(-1); }
        }
    }


    // taken from Microsoft.Win32.SafeHandles
    [System.Security.SecurityCritical]
    sealed class SafeFileHandle : SafeHandleZeroOrMinusOneIsInvalid
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
            return PInvoke.CloseHandle(handle);
        }
    }
    */

    static partial class PInvoke
    {

        // modified from Microsoft.Win32.SafeHandles.SafeFileHandle
        [System.Security.SecurityCritical]
        public sealed class SafeDiskHandle : SafeHandleZeroOrMinusOneIsInvalid
        {

            private SafeDiskHandle()
                : base(true)
            {
            }

            public SafeDiskHandle(IntPtr preexistingHandle, bool ownsHandle)
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

        // modified from Microsoft.Win32.SafeHandles.SafeFileHandle
        [System.Security.SecurityCritical]
        sealed class SafeFindVolumeHandle : SafeHandleZeroOrMinusOneIsInvalid
        {

            private SafeFindVolumeHandle()
                : base(true)
            {
            }

            public SafeFindVolumeHandle(IntPtr preexistingHandle, bool ownsHandle)
                : base(ownsHandle)
            {
                SetHandle(preexistingHandle);
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
            }

            [System.Security.SecurityCritical]
            override protected bool ReleaseHandle()
            {
                return FindVolumeClose(handle);
            }
        }

        // modified from Microsoft.Win32.SafeHandles.SafeFileHandle
        [System.Security.SecurityCritical]
        sealed class SafeFindVolumeMountPointHandle : SafeHandleZeroOrMinusOneIsInvalid
        {

            private SafeFindVolumeMountPointHandle()
                : base(true)
            {
            }

            public SafeFindVolumeMountPointHandle(IntPtr preexistingHandle, bool ownsHandle)
                : base(ownsHandle)
            {
                SetHandle(preexistingHandle);
            }

            [System.Security.SecurityCritical]
            override protected bool ReleaseHandle()
            {
                return FindVolumeMountPointClose(handle);
            }
        }

        // modified from Microsoft.Win32.SafeHandles.SafeFileHandle
        [System.Security.SecurityCritical]
        sealed class SafeFindFileHandle : SafeHandleZeroOrMinusOneIsInvalid
        {

            private SafeFindFileHandle()
                : base(true)
            {
            }

            public SafeFindFileHandle(IntPtr preexistingHandle, bool ownsHandle)
                : base(ownsHandle)
            {
                SetHandle(preexistingHandle);
            }

            [System.Security.SecurityCritical]
            override protected bool ReleaseHandle()
            {
                return FindClose(handle);
            }
        }
    }
}
