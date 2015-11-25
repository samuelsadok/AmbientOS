using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.ComponentModel;
using AmbientOS.Utils;
using Microsoft.Win32.SafeHandles;

namespace AmbientOS
{
    static partial class PInvoke
    {

        #region "Utils"

        static Win32Exception ToException(this ERROR error)
        {
            return new Win32Exception((int)error);
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
            private SafeWaitHandle handle;

            public SafeWaitHandle Handle { get { return handle; } }

            /// <summary>
            /// Creates a new managed Event instance.
            /// </summary>
            public Win32Event()
            {
                handle = CreateEvent(IntPtr.Zero, true, false, null);
                if (handle.IsInvalid)
                    throw new Win32Exception();
            }

            /// <summary>
            /// Waits for the event to be signalled
            /// </summary>
            /// <param name="timeout">Timeout in ms. -1 means infinity</param>
            public void Wait(int timeout)
            {
                switch (WaitForSingleObject(handle, timeout)) {
                    case 0: return;
                    case 0x80: throw new OperationCanceledException();  // the thread owning the event handle was terminated
                    case 0x102: throw new TimeoutException();
                    default: throw new Win32Exception();
                }
            }

            public void Dispose()
            {
                handle.Dispose();
            }
        }

        #endregion


        #region "Constants"

        public const int ERROR_INSUFFICIENT_BUFFER = 0x7A;

        public static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public enum ERROR
        {
            SUCCESS = 0,
            NO_MORE_FILES = 18,
            ALREADY_EXISTS = 183,
            MORE_DATA = 234,
            IO_PENDING = 997
        }

        public enum Access : uint
        {
            None = 0x00000000,
            All = 0x10000000,
            Read = 0x80000000,
            Write = 0x40000000,
            Execute = 0x20000000
        }

        public enum ShareMode
        {
            Read = 0x1,
            Write = 0x2,
            ReadWrite = 0x3,
            Delete = 0x4
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
            BACKUP_SEMANTICS = 0x02000000,
            OVERLAPPED = 0x40000000
        }

        /// <summary>
        /// Values taken from WinIoCtl.h from Windows 10 SDK
        /// </summary>
        public enum FILE_DEVICE
        {
            BEEP = 0x00000001,
            CD_ROM = 0x00000002,
            CD_ROM_FILE_SYSTEM = 0x00000003,
            CONTROLLER = 0x00000004,
            DATALINK = 0x00000005,
            DFS = 0x00000006,
            DISK = 0x00000007,
            DISK_FILE_SYSTEM = 0x00000008,
            FILE_SYSTEM = 0x00000009,
            INPORT_PORT = 0x0000000a,
            KEYBOARD = 0x0000000b,
            MAILSLOT = 0x0000000c,
            MIDI_IN = 0x0000000d,
            MIDI_OUT = 0x0000000e,
            MOUSE = 0x0000000f,
            MULTI_UNC_PROVIDER = 0x00000010,
            NAMED_PIPE = 0x00000011,
            NETWORK = 0x00000012,
            NETWORK_BROWSER = 0x00000013,
            NETWORK_FILE_SYSTEM = 0x00000014,
            NULL = 0x00000015,
            PARALLEL_PORT = 0x00000016,
            PHYSICAL_NETCARD = 0x00000017,
            PRINTER = 0x00000018,
            SCANNER = 0x00000019,
            SERIAL_MOUSE_PORT = 0x0000001a,
            SERIAL_PORT = 0x0000001b,
            SCREEN = 0x0000001c,
            SOUND = 0x0000001d,
            STREAMS = 0x0000001e,
            TAPE = 0x0000001f,
            TAPE_FILE_SYSTEM = 0x00000020,
            TRANSPORT = 0x00000021,
            UNKNOWN = 0x00000022,
            VIDEO = 0x00000023,
            VIRTUAL_DISK = 0x00000024,
            WAVE_IN = 0x00000025,
            WAVE_OUT = 0x00000026,
            _8042_PORT = 0x00000027,
            NETWORK_REDIRECTOR = 0x00000028,
            BATTERY = 0x00000029,
            BUS_EXTENDER = 0x0000002a,
            MODEM = 0x0000002b,
            VDM = 0x0000002c,
            MASS_STORAGE = 0x0000002d,
            SMB = 0x0000002e,
            KS = 0x0000002f,
            CHANGER = 0x00000030,
            SMARTCARD = 0x00000031,
            ACPI = 0x00000032,
            DVD = 0x00000033,
            FULLSCREEN_VIDEO = 0x00000034,
            DFS_FILE_SYSTEM = 0x00000035,
            DFS_VOLUME = 0x00000036,
            SERENUM = 0x00000037,
            TERMSRV = 0x00000038,
            KSEC = 0x00000039,
            FIPS = 0x0000003A,
            INFINIBAND = 0x0000003B,
            VMBUS = 0x0000003E,
            CRYPT_PROVIDER = 0x0000003F,
            WPD = 0x00000040,
            BLUETOOTH = 0x00000041,
            MT_COMPOSITE = 0x00000042,
            MT_TRANSPORT = 0x00000043,
            BIOMETRIC = 0x00000044,
            PMI = 0x00000045,
            EHSTOR = 0x00000046,
            DEVAPI = 0x00000047,
            GPIO = 0x00000048,
            USBEX = 0x00000049,
            CONSOLE = 0x00000050,
            NFP = 0x00000051,
            SYSENV = 0x00000052,
            VIRTUAL_BLOCK = 0x00000053,
            POINT_OF_SERVICE = 0x00000054,
            STORAGE_REPLICATION = 0x00000055,
            TRUST_ENV = 0x00000056,


        }

        public enum IOCTL_BASE
        {
            DISK = 0x00000007,
            STORAGE = 0x0000002d,
            VOLUME = 0x00000056 // 'V'
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

        public enum IOControlCode : int
        {
            // use CTL_CODE to generate actual values
        }

        private static IOControlCode CTL_CODE(FILE_DEVICE t, int f, IOCTL_METHOD m, IOCTL_FILE_ACCESS a)
        {
            return (IOControlCode)((((int)t) << 16) | (((int)a) << 14) | ((f) << 2) | ((int)m));
        }

        private static IOControlCode CTL_CODE(IOCTL_BASE t, int f, IOCTL_METHOD m, IOCTL_FILE_ACCESS a)
        {
            return (IOControlCode)((((int)t) << 16) | (((int)a) << 14) | ((f) << 2) | ((int)m));
        }

        // Values taken from WinIoCtl.h from Windows 10.0.10240.0 SDK
        public static IOControlCode IOCTL_DISK_GET_DRIVE_GEOMETRY = CTL_CODE(FILE_DEVICE.DISK, 0, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = CTL_CODE(IOCTL_BASE.VOLUME, 0, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_VOLUME_ONLINE = CTL_CODE(IOCTL_BASE.VOLUME, 2, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_VOLUME_OFFLINE = CTL_CODE(IOCTL_BASE.VOLUME, 3, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_VOLUME_IS_CLUSTERED = CTL_CODE(IOCTL_BASE.VOLUME, 12, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_VOLUME_GET_GPT_ATTRIBUTES = CTL_CODE(IOCTL_BASE.VOLUME, 14, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_CHECK_VERIFY = CTL_CODE(IOCTL_BASE.STORAGE, 0x0200, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_CHECK_VERIFY2 = CTL_CODE(IOCTL_BASE.STORAGE, 0x0200, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_MEDIA_REMOVAL = CTL_CODE(IOCTL_BASE.STORAGE, 0x0201, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_EJECT_MEDIA = CTL_CODE(IOCTL_BASE.STORAGE, 0x0202, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_LOAD_MEDIA = CTL_CODE(IOCTL_BASE.STORAGE, 0x0203, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_LOAD_MEDIA2 = CTL_CODE(IOCTL_BASE.STORAGE, 0x0203, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_RESERVE = CTL_CODE(IOCTL_BASE.STORAGE, 0x0204, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_RELEASE = CTL_CODE(IOCTL_BASE.STORAGE, 0x0205, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_FIND_NEW_DEVICES = CTL_CODE(IOCTL_BASE.STORAGE, 0x0206, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_EJECTION_CONTROL = CTL_CODE(IOCTL_BASE.STORAGE, 0x0250, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_MCN_CONTROL = CTL_CODE(IOCTL_BASE.STORAGE, 0x0251, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_GET_MEDIA_TYPES = CTL_CODE(IOCTL_BASE.STORAGE, 0x0300, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_GET_MEDIA_TYPES_EX = CTL_CODE(IOCTL_BASE.STORAGE, 0x0301, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_GET_MEDIA_SERIAL_NUMBER = CTL_CODE(IOCTL_BASE.STORAGE, 0x0304, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_GET_HOTPLUG_INFO = CTL_CODE(IOCTL_BASE.STORAGE, 0x0305, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_SET_HOTPLUG_INFO = CTL_CODE(IOCTL_BASE.STORAGE, 0x0306, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_RESET_BUS = CTL_CODE(IOCTL_BASE.STORAGE, 0x0400, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_RESET_DEVICE = CTL_CODE(IOCTL_BASE.STORAGE, 0x0401, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_BREAK_RESERVATION = CTL_CODE(IOCTL_BASE.STORAGE, 0x0405, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_PERSISTENT_RESERVE_IN = CTL_CODE(IOCTL_BASE.STORAGE, 0x0406, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_PERSISTENT_RESERVE_OUT = CTL_CODE(IOCTL_BASE.STORAGE, 0x0407, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_GET_DEVICE_NUMBER = CTL_CODE(IOCTL_BASE.STORAGE, 0x0420, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_PREDICT_FAILURE = CTL_CODE(IOCTL_BASE.STORAGE, 0x0440, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_FAILURE_PREDICTION_CONFIG = CTL_CODE(IOCTL_BASE.STORAGE, 0x0441, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_READ_CAPACITY = CTL_CODE(IOCTL_BASE.STORAGE, 0x0450, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_GET_DEVICE_TELEMETRY = CTL_CODE(IOCTL_BASE.STORAGE, 0x0470, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_DEVICE_TELEMETRY_NOTIFY = CTL_CODE(IOCTL_BASE.STORAGE, 0x0471, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_DEVICE_TELEMETRY_QUERY_CAPS = CTL_CODE(IOCTL_BASE.STORAGE, 0x0472, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_GET_DEVICE_TELEMETRY_RAW = CTL_CODE(IOCTL_BASE.STORAGE, 0x0473, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_SET_TEMPERATURE_THRESHOLD = CTL_CODE(IOCTL_BASE.STORAGE, 0x0480, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_PROTOCOL_COMMAND = CTL_CODE(IOCTL_BASE.STORAGE, 0x04F0, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_QUERY_PROPERTY = CTL_CODE(IOCTL_BASE.STORAGE, 0x0500, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_MANAGE_DATA_SET_ATTRIBUTES = CTL_CODE(IOCTL_BASE.STORAGE, 0x0501, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_GET_LB_PROVISIONING_MAP_RESOURCES = CTL_CODE(IOCTL_BASE.STORAGE, 0x0502, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_GET_BC_PROPERTIES = CTL_CODE(IOCTL_BASE.STORAGE, 0x0600, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_STORAGE_ALLOCATE_BC_STREAM = CTL_CODE(IOCTL_BASE.STORAGE, 0x0601, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_FREE_BC_STREAM = CTL_CODE(IOCTL_BASE.STORAGE, 0x0602, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_CHECK_PRIORITY_HINT_SUPPORT = CTL_CODE(IOCTL_BASE.STORAGE, 0x0620, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_START_DATA_INTEGRITY_CHECK = CTL_CODE(IOCTL_BASE.STORAGE, 0x0621, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_STOP_DATA_INTEGRITY_CHECK = CTL_CODE(IOCTL_BASE.STORAGE, 0x0622, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        [Obsolete()]
        public static IOControlCode OBSOLETE_IOCTL_STORAGE_RESET_BUS = CTL_CODE(IOCTL_BASE.STORAGE, 0x0400, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        [Obsolete()]
        public static IOControlCode OBSOLETE_IOCTL_STORAGE_RESET_DEVICE = CTL_CODE(IOCTL_BASE.STORAGE, 0x0401, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_FIRMWARE_GET_INFO = CTL_CODE(IOCTL_BASE.STORAGE, 0x0700, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_FIRMWARE_DOWNLOAD = CTL_CODE(IOCTL_BASE.STORAGE, 0x0701, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_FIRMWARE_ACTIVATE = CTL_CODE(IOCTL_BASE.STORAGE, 0x0702, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_STORAGE_ENABLE_IDLE_POWER = CTL_CODE(IOCTL_BASE.STORAGE, 0x0720, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_GET_IDLE_POWERUP_REASON = CTL_CODE(IOCTL_BASE.STORAGE, 0x0721, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_POWER_ACTIVE = CTL_CODE(IOCTL_BASE.STORAGE, 0x0722, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_POWER_IDLE = CTL_CODE(IOCTL_BASE.STORAGE, 0x0723, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_EVENT_NOTIFICATION = CTL_CODE(IOCTL_BASE.STORAGE, 0x0724, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_STORAGE_DEVICE_POWER_CAP = CTL_CODE(IOCTL_BASE.STORAGE, 0x0725, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode FSCTL_LOCK_VOLUME = CTL_CODE(FILE_DEVICE.FILE_SYSTEM, 6, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode FSCTL_UNLOCK_VOLUME = CTL_CODE(FILE_DEVICE.FILE_SYSTEM, 7, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode FSCTL_DISMOUNT_VOLUME = CTL_CODE(FILE_DEVICE.FILE_SYSTEM, 8, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_GET_PARTITION_INFO = CTL_CODE(IOCTL_BASE.DISK, 0x0001, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_SET_PARTITION_INFO = CTL_CODE(IOCTL_BASE.DISK, 0x0002, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_GET_DRIVE_LAYOUT = CTL_CODE(IOCTL_BASE.DISK, 0x0003, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_SET_DRIVE_LAYOUT = CTL_CODE(IOCTL_BASE.DISK, 0x0004, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_VERIFY = CTL_CODE(IOCTL_BASE.DISK, 0x0005, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_FORMAT_TRACKS = CTL_CODE(IOCTL_BASE.DISK, 0x0006, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_REASSIGN_BLOCKS = CTL_CODE(IOCTL_BASE.DISK, 0x0007, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_PERFORMANCE = CTL_CODE(IOCTL_BASE.DISK, 0x0008, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_IS_WRITABLE = CTL_CODE(IOCTL_BASE.DISK, 0x0009, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_LOGGING = CTL_CODE(IOCTL_BASE.DISK, 0x000a, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_FORMAT_TRACKS_EX = CTL_CODE(IOCTL_BASE.DISK, 0x000b, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_HISTOGRAM_STRUCTURE = CTL_CODE(IOCTL_BASE.DISK, 0x000c, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_HISTOGRAM_DATA = CTL_CODE(IOCTL_BASE.DISK, 0x000d, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_HISTOGRAM_RESET = CTL_CODE(IOCTL_BASE.DISK, 0x000e, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_REQUEST_STRUCTURE = CTL_CODE(IOCTL_BASE.DISK, 0x000f, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_REQUEST_DATA = CTL_CODE(IOCTL_BASE.DISK, 0x0010, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_PERFORMANCE_OFF = CTL_CODE(IOCTL_BASE.DISK, 0x0018, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_CONTROLLER_NUMBER = CTL_CODE(IOCTL_BASE.DISK, 0x0011, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode SMART_GET_VERSION = CTL_CODE(IOCTL_BASE.DISK, 0x0020, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode SMART_SEND_DRIVE_COMMAND = CTL_CODE(IOCTL_BASE.DISK, 0x0021, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode SMART_RCV_DRIVE_DATA = CTL_CODE(IOCTL_BASE.DISK, 0x0022, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_GET_PARTITION_INFO_EX = CTL_CODE(IOCTL_BASE.DISK, 0x0012, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_SET_PARTITION_INFO_EX = CTL_CODE(IOCTL_BASE.DISK, 0x0013, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_GET_DRIVE_LAYOUT_EX = CTL_CODE(IOCTL_BASE.DISK, 0x0014, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_SET_DRIVE_LAYOUT_EX = CTL_CODE(IOCTL_BASE.DISK, 0x0015, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_CREATE_DISK = CTL_CODE(IOCTL_BASE.DISK, 0x0016, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_GET_LENGTH_INFO = CTL_CODE(IOCTL_BASE.DISK, 0x0017, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = CTL_CODE(IOCTL_BASE.DISK, 0x0028, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_REASSIGN_BLOCKS_EX = CTL_CODE(IOCTL_BASE.DISK, 0x0029, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_UPDATE_DRIVE_SIZE = CTL_CODE(IOCTL_BASE.DISK, 0x0032, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_GROW_PARTITION = CTL_CODE(IOCTL_BASE.DISK, 0x0034, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_GET_CACHE_INFORMATION = CTL_CODE(IOCTL_BASE.DISK, 0x0035, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_SET_CACHE_INFORMATION = CTL_CODE(IOCTL_BASE.DISK, 0x0036, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        [Obsolete()]
        public static IOControlCode OBSOLETE_DISK_GET_WRITE_CACHE_STATE = CTL_CODE(IOCTL_BASE.DISK, 0x0037, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_DELETE_DRIVE_LAYOUT = CTL_CODE(IOCTL_BASE.DISK, 0x0040, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_UPDATE_PROPERTIES = CTL_CODE(IOCTL_BASE.DISK, 0x0050, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_FORMAT_DRIVE = CTL_CODE(IOCTL_BASE.DISK, 0x00f3, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ | IOCTL_FILE_ACCESS.WRITE);
        public static IOControlCode IOCTL_DISK_SENSE_DEVICE = CTL_CODE(IOCTL_BASE.DISK, 0x00f8, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);
        public static IOControlCode IOCTL_DISK_CHECK_VERIFY = CTL_CODE(IOCTL_BASE.DISK, 0x0200, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_MEDIA_REMOVAL = CTL_CODE(IOCTL_BASE.DISK, 0x0201, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_EJECT_MEDIA = CTL_CODE(IOCTL_BASE.DISK, 0x0202, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_LOAD_MEDIA = CTL_CODE(IOCTL_BASE.DISK, 0x0203, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_RESERVE = CTL_CODE(IOCTL_BASE.DISK, 0x0204, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_RELEASE = CTL_CODE(IOCTL_BASE.DISK, 0x0205, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_FIND_NEW_DEVICES = CTL_CODE(IOCTL_BASE.DISK, 0x0206, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.READ);
        public static IOControlCode IOCTL_DISK_GET_MEDIA_TYPES = CTL_CODE(IOCTL_BASE.DISK, 0x0300, IOCTL_METHOD.BUFFERED, IOCTL_FILE_ACCESS.ANY);


        // Values taken from WinNT.h from Windows 10.0.10240.0 SDK
        public const string SE_CREATE_TOKEN_NAME = "SeCreateTokenPrivilege";
        public const string SE_ASSIGNPRIMARYTOKEN_NAME = "SeAssignPrimaryTokenPrivilege";
        public const string SE_LOCK_MEMORY_NAME = "SeLockMemoryPrivilege";
        public const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";
        public const string SE_UNSOLICITED_INPUT_NAME = "SeUnsolicitedInputPrivilege";
        public const string SE_MACHINE_ACCOUNT_NAME = "SeMachineAccountPrivilege";
        public const string SE_TCB_NAME = "SeTcbPrivilege";
        public const string SE_SECURITY_NAME = "SeSecurityPrivilege";
        public const string SE_TAKE_OWNERSHIP_NAME = "SeTakeOwnershipPrivilege";
        public const string SE_LOAD_DRIVER_NAME = "SeLoadDriverPrivilege";
        public const string SE_SYSTEM_PROFILE_NAME = "SeSystemProfilePrivilege";
        public const string SE_SYSTEMTIME_NAME = "SeSystemtimePrivilege";
        public const string SE_PROF_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";
        public const string SE_INC_BASE_PRIORITY_NAME = "SeIncreaseBasePriorityPrivilege";
        public const string SE_CREATE_PAGEFILE_NAME = "SeCreatePagefilePrivilege";
        public const string SE_CREATE_PERMANENT_NAME = "SeCreatePermanentPrivilege";
        public const string SE_BACKUP_NAME = "SeBackupPrivilege";
        public const string SE_RESTORE_NAME = "SeRestorePrivilege";
        public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
        public const string SE_DEBUG_NAME = "SeDebugPrivilege";
        public const string SE_AUDIT_NAME = "SeAuditPrivilege";
        public const string SE_SYSTEM_ENVIRONMENT_NAME = "SeSystemEnvironmentPrivilege";
        public const string SE_CHANGE_NOTIFY_NAME = "SeChangeNotifyPrivilege";
        public const string SE_REMOTE_SHUTDOWN_NAME = "SeRemoteShutdownPrivilege";
        public const string SE_UNDOCK_NAME = "SeUndockPrivilege";
        public const string SE_SYNC_AGENT_NAME = "SeSyncAgentPrivilege";
        public const string SE_ENABLE_DELEGATION_NAME = "SeEnableDelegationPrivilege";
        public const string SE_MANAGE_VOLUME_NAME = "SeManageVolumePrivilege";
        public const string SE_IMPERSONATE_NAME = "SeImpersonatePrivilege";
        public const string SE_CREATE_GLOBAL_NAME = "SeCreateGlobalPrivilege";
        public const string SE_TRUSTED_CREDMAN_ACCESS_NAME = "SeTrustedCredManAccessPrivilege";
        public const string SE_RELABEL_NAME = "SeRelabelPrivilege";
        public const string SE_INC_WORKING_SET_NAME = "SeIncreaseWorkingSetPrivilege";
        public const string SE_TIME_ZONE_NAME = "SeTimeZonePrivilege";
        public const string SE_CREATE_SYMBOLIC_LINK_NAME = "SeCreateSymbolicLinkPrivilege";


        public enum AttachVirtualDiskFlags : int
        {
            None = 0x00000000,
            ReadOnly = 0x00000001,
            NoDriveLetter = 0x00000002,
            PermanentLifetime = 0x00000004,
            NoLocalHost = 0x00000008
        }

        public enum AttachVirtualDiskVersion : int
        {
            Unspecified = 0,
            Version1 = 1
        }

        public enum OpenVirtualDiskFlags : int
        {
            None = 0x00000000,
            NoParents = 0x00000001,
            BlankFile = 0x00000002,
            BootDrive = 0x00000004
        }

        public enum OpenVirtualDiskVersion : int
        {
            Version1 = 1,
            Version2 = 2
        }

        public enum VirtualDiskAccessFlags : int
        {
            AttachRO = 0x00010000,
            AttachRW = 0x00020000,
            Detach = 0x00040000,
            GetInfo = 0x00080000,
            Create = 0x00100000,
            MetaOps = 0x00200000,
            Read = 0x000d0000,
            All = 0x003f0000,
            Writable = 0x00320000
        }

        public enum VirtualStorageTypeDevice : int
        {
            Unknown = 0,
            ISO = 1, // not available prior to Windows 8
            VHD = 2,
            VHDX = 3 // not available prior to Windows 8
        }

        public static readonly Guid VirtualStorageTypeVendorUnknown = new Guid();
        public static readonly Guid VirtualStorageTypeVendorMicrosoft = new Guid("EC984AEC-A0F9-47E9-901F-71415A66345B");


        enum FileInfoByHandleClass : int
        {
            FileBasicInfo = 0,
            FileRenameInfo = 3,
            FileDispositionInfo = 4,
            FileAllocationInfo = 5,
            FileEndOfFileInfo = 6,
            FileIoPriorityHintInfo = 12
        }

        #endregion


        #region "Structures"

        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct Overlapped
        {
            Int64 Internal;
            public Int64 Offset;
            public SafeWaitHandle hEvent;
            public Overlapped(long offset, SafeWaitHandle hEvent)
            {
                Internal = 0;
                Offset = offset;
                this.hEvent = hEvent;
            }
        }

        // we use our own, superior ByteConverter for this struct
        [StructLayout(LayoutKind.Sequential)]
        public struct DiskGeometry
        {
            public Int64 Cylinders;
            public Int32 MediaType;
            public Int32 TracksPerCylinder;
            public Int32 SectorsPerTrack;
            public UInt32 BytesPerSector;
            public Int64 Capacity { get { return Cylinders * TracksPerCylinder * SectorsPerTrack * BytesPerSector; } }
        }

        // we use our own, superior ByteConverter for this struct
        [StructLayout(LayoutKind.Sequential)]
        public struct VolumeDiskExtents
        {
            [FieldSpecs(LengthOf = "Extents")]
            public Int32 NumberOfDiskExtents;
            Int32 reserved;
            public DiskExtent[] Extents;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DiskExtent // size: 0x18
        {
            public Int32 DiskNumber;
            Int32 reserved;
            public Int64 StartingOffset;
            public Int64 ExtentLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct StorageReadCapacity
        {
            public UInt64 Version;
            public UInt64 Size;
            public UInt64 BlockLength;
            public Int64 NumberOfBlocks;
            public Int64 DiskLength;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct AttachVirtualDiskParameters
        {
            public AttachVirtualDiskVersion Version;
            public AttachVirtualDiskParametersVersion1 Version1;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct AttachVirtualDiskParametersVersion1
        {
            public Int32 Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OpenVirtualDiskParameters
        {
            public OpenVirtualDiskVersion Version;
            public OpenVirtualDiskParametersVersion1 Version1;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OpenVirtualDiskParametersVersion1
        {
            public Int32 RWDepth;
        }

        /// <summary>
        /// Not supported prior to Windows 8
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OpenVirtualDiskParametersVersion2
        {
            public bool GetInfoOnly;
            public bool ReadOnly;
            public Guid ResiliencyGuid;
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct VirtualStorageType
        {
            public VirtualStorageTypeDevice DeviceId;
            public Guid VendorId;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class ByHandleFileInformation
        {
            public Int32 dwFileAttributes;
            [FieldSpecs(DateFormat = DateFormat.NTFS)]
            public DateTime ftCreationTime;
            [FieldSpecs(DateFormat = DateFormat.NTFS)]
            public DateTime ftLastAccessTime;
            [FieldSpecs(DateFormat = DateFormat.NTFS)]
            public DateTime ftLastWriteTime;
            public Int32 dwVolumeSerialNumber;
            public Int32 nFileSizeHigh;
            public Int32 nFileSizeLow;
            public Int32 nNumberOfLinks;
            public Int32 nFileIndexHigh;
            public Int32 nFileIndexLow;
        }

        public class FileBasicInfo
        {
            [FieldSpecs(DateFormat = DateFormat.Windows)]
            public DateTime CreationTime;
            [FieldSpecs(DateFormat = DateFormat.Windows)]
            public DateTime LastAccessTime;
            [FieldSpecs(DateFormat = DateFormat.Windows)]
            public DateTime LastWriteTime;
            [FieldSpecs(DateFormat = DateFormat.Windows)]
            public DateTime ChangeTime;
            public Int32 FileAttributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class FileRenameInfo
        {
            public Int32 ReplaceIfExists;
            public IntPtr RootDirectory;
            [FieldSpecs(LengthOf = "FileName")]
            Int32 FileNameLength;
            public string FileName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class Win32FindData
        {
            public Int32 dwFileAttributes;
            [FieldSpecs(DateFormat = DateFormat.Windows)]
            public DateTime ftCreationTime;
            [FieldSpecs(DateFormat = DateFormat.Windows)]
            public DateTime ftLastAccessTime;
            [FieldSpecs(DateFormat = DateFormat.Windows)]
            public DateTime ftLastWriteTime;
            public Int32 nFileSizeHigh;
            public Int32 nFileSizeLow;
            public Int32 dwReserved0;
            public Int32 dwReserved1;
            [FieldSpecs(StringFormat = StringFormat.Unicode, Length = 260)]
            public string cFileName;
            [FieldSpecs(StringFormat = StringFormat.Unicode, Length = 14)]
            public string cAlternateFileName;
        }


        #endregion


        #region "kernel32.dll functions"

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int QueryDosDevice(string lpDeviceName, IntPtr lpTargetPath, int ucchMax);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Boolean DeviceIoControl(SafeFileHandle hDevice, IOControlCode dwIoControlCode, byte[] lpInBuffer, Int32 nInBufferSize, byte[] lpOutBuffer, Int32 nOutBufferSize, ref Int32 lpBytesReturned, ref Overlapped lpOverlapped);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //static extern IntPtr CreateFile(string lpFileName, Int32 dwDesiredAccess, Int32 dwShareMode, IntPtr lpSecurityAttributes, Int32 dwCreationDisposition, Int32 dwFlagsAndAttributes, IntPtr hTemplateFile);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
        static extern SafeFileHandle CreateFile(string lpFileName, Int32 dwDesiredAccess, Int32 dwShareMode, IntPtr lpSecurityAttributes, Int32 dwCreationDisposition, Int32 dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", EntryPoint = "DeleteFile", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
        static extern Boolean DeleteFileEx(string lpPathName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
        static extern Boolean CreateDirectory(string lpPathName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", EntryPoint = "RemoveDirectory", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
        static extern Boolean RemoveDirectoryEx(string lpPathName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Boolean GetFileInformationByHandle(SafeFileHandle hFile, byte[] lpFileInformation);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern Boolean SetFileInformationByHandle(SafeFileHandle hFile, FileInfoByHandleClass FileInformationClass, byte[] lpFileInformation, int bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean ReadFile(SafeFileHandle hFile, IntPtr buffer, Int32 nNumberOfBytesToRead, ref Int32 lpNumberOfBytesRead, ref Overlapped lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean WriteFile(SafeFileHandle hFile, IntPtr buffer, Int32 nNumberOfBytesToWrite, ref Int32 lpNumberOfBytesWritten, ref Overlapped lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern SafeWaitHandle CreateEvent(IntPtr lpEventAttributes, Boolean bManualReset, Boolean bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern Int32 WaitForSingleObject(SafeWaitHandle hHandle, Int32 dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean GetOverlappedResult(SafeFileHandle hFile, ref Overlapped lpOverlapped, ref Int32 lpNumberOfBytesTransferred, Boolean bWait);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean CloseHandle(IntPtr h);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFindVolumeHandle FindFirstVolume(short[] lpszVolumeName, int cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean FindNextVolume(SafeFindVolumeHandle hFindVolume, short[] lpszVolumeName, int cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean FindVolumeClose(IntPtr hFindVolume);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFindVolumeMountPointHandle FindFirstVolumeMountPoint(string lpszRootPathName, short[] lpszVolumeMountPoint, int cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean FindNextVolumeMountPoint(SafeFindVolumeMountPointHandle hFindVolumeMountPoint, short[] lpszVolumeMountPoint, int cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean FindVolumeMountPointClose(IntPtr hFindVolumeMountPoint);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean GetVolumeInformation(string lpRootPathName, short[] lpVolumeNameBuffer, Int32 nVolumeNameSize, ref Int32 lpVolumeSerialNumber, ref Int32 lpMaximumComponentLength, ref Int32 lpFileSystemFlags, short[] lpFileSystemNameBuffer, Int32 nFileSystemNameSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean GetVolumePathNamesForVolumeName(string lpszVolumeName, short[] lpszVolumePathNames, int cchBufferLength, ref int lpcchReturnLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFindFileHandle FindFirstFile(string lpFileName, byte[] lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean FindNextFile(SafeFindFileHandle hFindFile, byte[] lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean FindClose(IntPtr hFindFile);

        #endregion

        #region "virtdisk.dll functions"

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        static extern ERROR OpenVirtualDisk(ref VirtualStorageType virtualStorageType, string path, VirtualDiskAccessFlags virtualDiskAccessMask, OpenVirtualDiskFlags flags, ref OpenVirtualDiskParameters parameters, ref IntPtr hDisk);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        static extern ERROR AttachVirtualDisk(SafeHandle virtualDiskHandle, IntPtr securityDescriptor, AttachVirtualDiskFlags flags, Int32 providerSpecificFlags, ref AttachVirtualDiskParameters parameters, IntPtr lpOverlapped);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        static extern ERROR GetVirtualDiskPhysicalPath(SafeHandle virtualDiskHandle, ref long diskPathSizeInBytes, byte[] diskPath);

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
                        throw new Win32Exception();
                }
            }
        }

        /// <summary>
        /// Returns a list of every single device connected to the PC.
        /// </summary>
        public static string[] GetAllDevices()
        {
            return QueryDosDevice(null);
        }


        public static ERROR DeviceIoControl(SafeFileHandle device, IOControlCode controlCode, byte[] input, byte[] output, ERROR acceptError = ERROR.SUCCESS)
        {
            int bufferSize = 0;
            using (Win32Event e = new Win32Event()) {
                //Console.WriteLine("write " + buffer.Count() + " bytes at " + fileOffset);
                Overlapped overlapped = new Overlapped(0, e.Handle);
                var success = DeviceIoControl(device, controlCode,
                    input, input?.Length ?? 0,
                    output, output?.Length ?? 0,
                    ref bufferSize, ref overlapped);
                if (!success && (ERROR)Marshal.GetLastWin32Error() == ERROR.IO_PENDING) {
                    e.Wait(-1);
                    success = GetOverlappedResult(device, ref overlapped, ref bufferSize, true);
                }
                if (!success && (ERROR)Marshal.GetLastWin32Error() != acceptError)
                    throw new Win32Exception();
                return (ERROR)Marshal.GetLastWin32Error();
            }
        }

        public static TOut DeviceIoControl<TIn, TOut>(SafeFileHandle device, IOControlCode controlCode, TIn input, ERROR acceptError = ERROR.SUCCESS)
            where TIn : struct
            where TOut : struct
        {
            var inBuffer = new byte[Marshal.SizeOf(typeof(TIn))];
            var outBuffer = new byte[Marshal.SizeOf(typeof(TOut))];

            inBuffer.WriteVal(0, input, Endianness.Current);
            DeviceIoControl(device, controlCode, inBuffer, outBuffer, acceptError);
            return outBuffer.ReadObject<TOut>(0, Endianness.Current);
        }

        public static void DeviceIoControl<TIn>(SafeFileHandle device, IOControlCode controlCode, TIn input, ERROR acceptError = ERROR.SUCCESS)
            where TIn : struct
        {
            var inBuffer = new byte[Marshal.SizeOf(typeof(TIn))];
            inBuffer.WriteVal(0, inBuffer, Endianness.Current);
            DeviceIoControl(device, controlCode, inBuffer, null, acceptError);
        }

        public static TOut DeviceIoControl<TOut>(SafeFileHandle device, IOControlCode controlCode, ERROR acceptError = ERROR.SUCCESS)
            where TOut : struct
        {
            var outBuffer = new byte[Marshal.SizeOf(typeof(TOut))];
            DeviceIoControl(device, controlCode, null, outBuffer, acceptError);
            return outBuffer.ReadObject<TOut>(0, Endianness.Current);
        }


        public static SafeFileHandle CreateFile(string file, Access access, ShareMode shareMode, IntPtr securityAttributes, CreationDisposition creationDisposition, FileFlags flags, IntPtr template)
        {
            var result = CreateFile(file, (Int32)access, (Int32)shareMode, securityAttributes, (Int32)creationDisposition, (Int32)flags, template);
            if (result.IsInvalid)
                throw new Win32Exception();
            return result;
        }

        public static SafeFileHandle CreateDirectory(string directory, Access access, ShareMode shareMode, IntPtr securityAttributes, CreationDisposition creationDisposition, FileFlags flags, IntPtr template)
        {
            // directories must be created by a separate API call
            if (creationDisposition == CreationDisposition.CREATE_ALWAYS || creationDisposition == CreationDisposition.CREATE_NEW)
                if (!CreateDirectory(directory, IntPtr.Zero))
                    if (creationDisposition != CreationDisposition.CREATE_ALWAYS || (ERROR)Marshal.GetLastWin32Error() != ERROR.ALREADY_EXISTS)
                        throw new Win32Exception();
            return CreateFile(directory, access, shareMode, securityAttributes, CreationDisposition.OPEN_EXISTING, flags, template);
        }

        public static void DeleteFile(string file)
        {
            if (!DeleteFileEx(file))
                throw new Win32Exception();
        }

        public static void RemoveDirectory(string file)
        {
            if (!RemoveDirectoryEx(file))
                throw new Win32Exception();
        }

        public static ByHandleFileInformation GetFileInformationByHandle(SafeFileHandle file)
        {
            var info = new byte[0x34];
            if (!GetFileInformationByHandle(file, info))
                throw new Win32Exception();
            return info.ReadObject<ByHandleFileInformation>(0, Endianness.Current);
        }

        public static void SetFileInformationByHandle(SafeFileHandle file, FileBasicInfo info)
        {
            var buffer = new byte[0x28];
            buffer.WriteVal(0, info, Endianness.Current);
            if (!SetFileInformationByHandle(file, FileInfoByHandleClass.FileBasicInfo, buffer, buffer.Length))
                throw new Win32Exception();
        }

        public static void SetFileInformationByHandle(SafeFileHandle file, FileRenameInfo info)
        {
            var buffer = new byte[0x12 + info.FileName.Length];
            buffer.WriteVal(0, info, Endianness.Current);
            if (!SetFileInformationByHandle(file, FileInfoByHandleClass.FileRenameInfo, buffer, buffer.Length))
                throw new Win32Exception();
        }

        /// <summary>
        /// Reads data from a file represented by a Win32 handle
        /// </summary>
        /// <param name="file">The file to read from</param>
        /// <param name="fileOffset">The offset within the file</param>
        /// <param name="buffer">The buffer to read into</param>
        /// <param name="bufferOffset">An offset into the buffer where to place the data</param>
        public static void ReadFile(SafeFileHandle file, long fileOffset, byte[] buffer, int bufferOffset, int count)
        {
            int bufferSize = 0;
            using (Win32Event e = new Win32Event()) {
                //Console.WriteLine("read " + buffer.Count() + " bytes at " + fileOffset);
                Overlapped overlapped = new Overlapped(fileOffset, e.Handle);
                bool success = ReadFile(file, Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferOffset), count, ref bufferSize, ref overlapped);
                if (!success && (ERROR)Marshal.GetLastWin32Error() == ERROR.IO_PENDING) {
                    e.Wait(-1);
                    success = GetOverlappedResult(file, ref overlapped, ref bufferSize, true);
                }
                if (!success)
                    throw new Win32Exception();
                if (bufferSize < count) // if this is found to fail, see notes in WriteFile
                    throw new Exception("not all bytes were read (should read " + count + ", did read " + bufferSize + ")");
            }
        }

        /// <summary>
        /// Writes data to a file represented by a Win32 handle
        /// </summary>
        /// <param name="file">The file to write to</param>
        /// <param name="fileOffset">The offset within the file</param>
        /// <param name="buffer">The buffer that contains the data to be written</param>
        /// <param name="bufferOffset">An offset into the buffer where to read the data</param>
        public static void WriteFile(SafeFileHandle file, long fileOffset, byte[] buffer, int bufferOffset, int count)
        {
            int bufferSize = 0;
            using (Win32Event e = new Win32Event()) {
                //Console.WriteLine("write " + buffer.Count() + " bytes at " + fileOffset);
                Overlapped overlapped = new Overlapped(fileOffset, e.Handle);
                bool success = WriteFile(file, Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferOffset), count, ref bufferSize, ref overlapped);
                if (!success && (ERROR)Marshal.GetLastWin32Error() == ERROR.IO_PENDING) {
                    e.Wait(-1);
                    success = GetOverlappedResult(file, ref overlapped, ref bufferSize, true);
                    bufferSize = count; // it appears that in overlapped mode, this value cannot be trusted, so we cheat a little
                }
                if (!success)
                    throw new Win32Exception();
                if (bufferSize < count)
                     throw new Exception("not all bytes were written (should write " + count + ", did write " + bufferSize + ")");
            }
        }

        public static SafeDiskHandle OpenVirtualDisk(VirtualStorageType virtualStorageType, string path, VirtualDiskAccessFlags virtualDiskAccessMask, OpenVirtualDiskFlags flags, OpenVirtualDiskParameters parameters)
        {
            IntPtr handle = IntPtr.Zero;
            var result = OpenVirtualDisk(ref virtualStorageType, path, virtualDiskAccessMask, flags, ref parameters, ref handle);
            if (result != ERROR.SUCCESS)
                throw result.ToException();
            return new SafeDiskHandle(handle, true);
        }

        public static void AttachVirtualDisk(SafeDiskHandle hDisk, IntPtr securityDescriptor, AttachVirtualDiskFlags flags, Int32 providerSpecificFlags, AttachVirtualDiskParameters parameters)
        {
            var result = AttachVirtualDisk(hDisk, securityDescriptor, flags, providerSpecificFlags, ref parameters, IntPtr.Zero);
            if (result != ERROR.SUCCESS)
                throw result.ToException();
        }

        /// <summary>
        /// Returns a name of the format "\\.\PhysicalDriveX", where X is an integer.
        /// </summary>
        public static string GetVirtualDiskPhysicalPath(SafeDiskHandle hDisk)
        {
            var name = new byte[2 * 260];
            long length = name.Length;
            GetVirtualDiskPhysicalPath(hDisk, ref length, name);
            return name.ReadString(0, length / 2, StringFormat.Unicode, Endianness.LittleEndian);
        }

        /// <summary>
        /// Returns a list of all volumes on the system.
        /// The returned strings have the format:
        ///   //?/Volume{GUID}/
        /// </summary>
        public static IEnumerable<string> FindVolumes()
        {
            var buffer = new short[128];

            var handle = FindFirstVolume(buffer, buffer.Length);
            if (handle.IsInvalid)
                yield break;

            using (handle) {
                while (true) {
                    yield return new string(buffer.TakeWhile(c => c != 0).Select(c => Convert.ToChar(c)).ToArray());

                    if (!FindNextVolume(handle, buffer, buffer.Length)) {
                        if ((ERROR)Marshal.GetLastWin32Error() == ERROR.NO_MORE_FILES)
                            break;
                        throw new Win32Exception();
                    }
                }
            }
        }

        /// <summary>
        /// Returns a list of all mount points of a particular volume.
        /// </summary>
        /// <param name="volume">A string of the format //?/Volume{GUID}/</param>
        /// <remarks>Pops up an unexpected message box for some drives.</remarks>
        public static IEnumerable<string> FindVolumeMountPoints(string volume)
        {
            var buffer = new short[260];

            var handle = FindFirstVolumeMountPoint(volume, buffer, buffer.Length);
            if (handle.IsInvalid)
                yield break;

            using (handle) {
                while (true) {
                    yield return new string(buffer.TakeWhile(c => c != 0).Select(c => Convert.ToChar(c)).ToArray());

                    if (!FindNextVolumeMountPoint(handle, buffer, buffer.Length)) {
                        if ((ERROR)Marshal.GetLastWin32Error() == ERROR.NO_MORE_FILES)
                            break;
                        throw new Win32Exception();
                    }
                }
            }
        }

        /// <summary>
        /// Returns the name of the file system on the volume (e.g. "NTFS" or "FAT")
        /// </summary>
        /// <param name="volume">A valid root name, e.g. a string of the format //?/Volume{GUID}/</param>
        public static string GetVolumeInformation(string volume)
        {
            var volumeNameBuffer = volume.Select(c => Convert.ToInt16(c)).ToArray();
            var fsNameBuffer = new short[260];

            // these values are read but not ouput (alter the function signature if you need them)
            int serialNumber = 0;
            int maxComponentLength = 0;
            int fsFlags = 0;

            if (!GetVolumeInformation(volume, volumeNameBuffer, volumeNameBuffer.Length, ref serialNumber, ref maxComponentLength, ref fsFlags, fsNameBuffer, fsNameBuffer.Length))
                throw new Win32Exception();
            return new string(fsNameBuffer.TakeWhile(c => c != 0).Select(c => Convert.ToChar(c)).ToArray());
        }

        /// <summary>
        /// Returns the path names for a particular volume.
        /// </summary>
        /// <param name="volume">A string of the format //?/Volume{GUID}/</param>
        public static string[] GetVolumePathNamesForVolumeName(string volume)
        {
            int outputLength = 0;
            short[] output;

            while (true) {
                output = new short[outputLength];
                var success = GetVolumePathNamesForVolumeName(volume, output, output.Length, ref outputLength);
                if (success)
                    break;
                else if ((ERROR)Marshal.GetLastWin32Error() == ERROR.MORE_DATA)
                    continue;
                else
                    throw new Win32Exception();
            }

            return new string(output.Select(c => Convert.ToChar(c)).ToArray()).Split('\0');
        }


        public static IEnumerable<Win32FindData> FindFiles(string query)
        {
            var buffer = new byte[0x250];

            var handle = FindFirstFile(query, buffer);
            if (handle.IsInvalid)
                yield break;

            using (handle) {
                while (true) {
                    yield return buffer.ReadObject<Win32FindData>(0, Endianness.Current);

                    if (!FindNextFile(handle, buffer)) {
                        if ((ERROR)Marshal.GetLastWin32Error() == ERROR.NO_MORE_FILES)
                            break;
                        throw new Win32Exception();
                    }
                }
            }
        }

        #endregion
    }
}
