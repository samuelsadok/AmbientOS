using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AmbientOS.FileSystem
{
    abstract class WindowsFileSystemObject : IFileSystemObjectImpl, IDisposable
    {
        public IFileSystemObject FileSystemObjectRef { get; }

        private readonly PInvoke.ByHandleFileInformation info;
        private readonly WindowsFolder parent;
        protected readonly IFileSystem fs;

        /// <summary>
        /// Returns the path of the file
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// If not null, this handle will be closed when this disk is disposed.
        /// </summary>
        public SafeHandle Handle { get; set; } = null;


        protected abstract SafeFileHandle OpenFile(PInvoke.Access access);

        /// <summary>
        /// Generates a Windows file object from the provided name.
        /// </summary>
        public WindowsFileSystemObject(IFileSystem fs, string path)
        {
            FileSystemObjectRef = new FileSystemObjectRef(this);

            this.fs = fs;
            Path = path;

            // get file info
            using (var file = OpenFile(PInvoke.Access.None)) {
                info = PInvoke.GetFileInformationByHandle(file);
            }
        }

        public void Dispose()
        {
            Handle?.Dispose();
        }

        public IFileSystem GetFileSystem()
        {
            return fs;
        }

        public string GetPath()
        {
            return Path;
        }

        public string GetName()
        {
            var delimiter = Path.LastIndexOfAny(new char[] { '\\', '/' });
            return Path.Substring(delimiter + 1);
        }

        public void SetName(string name)
        {
            using (var root = parent.OpenFile(0))
            using (var file = OpenFile(0))
                PInvoke.SetFileInformationByHandle(file, new PInvoke.FileRenameInfo() {
                    ReplaceIfExists = 0,
                    RootDirectory = root.DangerousGetHandle(),
                    FileName = name
                });
        }

        public FileTimes GetTimes()
        {
            return new FileTimes() {
                CreatedTime = info.ftCreationTime,
                ReadTime = info.ftLastAccessTime,
                ModifiedTime = info.ftLastWriteTime
            };
        }

        public void SetTimes(FileTimes times)
        {
            using (var file = OpenFile(0))
                PInvoke.SetFileInformationByHandle(file, new PInvoke.FileBasicInfo() {
                    CreationTime = info.ftCreationTime,
                    LastAccessTime = info.ftLastAccessTime,
                    LastWriteTime = info.ftLastWriteTime,
                    ChangeTime = info.ftLastWriteTime,
                    FileAttributes = info.dwFileAttributes
                });
        }

        public long? GetSize()
        {
            return ((long)info.nFileSizeHigh << 32) | (info.nFileSizeLow & 0xFFFFFFFF);
        }

        public long? GetSizeOnDisk()
        {
            return null; // todo: determine size on disk using Windows fragmentation API
        }

        public abstract void Delete(DeleteMode mode);

        public void SecureDelete(int passes)
        {
            using (var fsObj = FileSystemObjectRef.Retain())
                fsObj.SecureDelete(passes, true);
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Path == (obj as WindowsFileSystemObject)?.Path;
        }
    }


    class WindowsFile : WindowsFileSystemObject, IFileImpl
    {
        public IFile FileRef { get; }


        public WindowsFile(IFileSystem fs, string path)
            : base(fs, path)
        {
            FileRef = new FileRef(this);
        }

        protected override SafeFileHandle OpenFile(PInvoke.Access access)
        {
            return PInvoke.CreateFile(Path, access, PInvoke.ShareMode.ReadWrite, IntPtr.Zero, PInvoke.CreationDisposition.OPEN_EXISTING, PInvoke.FileFlags.OVERLAPPED, IntPtr.Zero);
        }

        public override void Delete(DeleteMode mode)
        {
            if (mode != DeleteMode.Permanent)
                throw new NotImplementedException();
            PInvoke.DeleteFile(Path);
        }

        public void Read(long offset, long count, byte[] buffer, long bufferOffset)
        {
            using (var file = OpenFile(PInvoke.Access.Read))
                PInvoke.ReadFile(file, offset, buffer, (int)bufferOffset, (int)count);
        }
        
        public void Write(long offset, long count, byte[] buffer, long bufferOffset)
        {
            using (var file = OpenFile(PInvoke.Access.Write))
                PInvoke.WriteFile(file, offset, buffer, (int)bufferOffset, (int)count);
        }

        public void Flush()
        {
            // the handle is closed after each write, which presumably flushes the cache
        }

        public void ChangeSize(long newSize)
        {
            throw new NotImplementedException();
        }

        public void AddCustomAppearance(Dictionary<string, string> dict, Type type)
        {
            throw new NotImplementedException();
        }
    }

    class WindowsFolder : WindowsFileSystemObject, IFolderImpl
    {
        public IFolder FolderRef { get; }

        protected override SafeFileHandle OpenFile(PInvoke.Access access)
        {
            return PInvoke.CreateDirectory(Path + "\\", access, PInvoke.ShareMode.ReadWrite, IntPtr.Zero, PInvoke.CreationDisposition.OPEN_EXISTING, PInvoke.FileFlags.OVERLAPPED | PInvoke.FileFlags.BACKUP_SEMANTICS, IntPtr.Zero);
        }

        public WindowsFolder(IFileSystem fs, string path)
            : base(fs, path)
        {
            FolderRef = new FolderRef(this);
        }

        public override void Delete(DeleteMode mode)
        {
            if (mode != DeleteMode.Permanent)
                throw new NotImplementedException();
            PInvoke.RemoveDirectory(Path + "\\");
        }

        public IEnumerable<IFileSystemObject> GetChildren()
        {
            return PInvoke.FindFiles(Path + "\\*").Where(result => !result.cFileName.StartsWith(".\0") && !result.cFileName.StartsWith("..\0")).Select(result =>
                (result.dwFileAttributes & 0x10) == 0 ?
                (IFileSystemObject)new WindowsFile(fs, Path + "\\" + result.cFileName.TrimEnd('\0')) : // todo: include zero termination to byte converter
                (IFileSystemObject)new WindowsFolder(fs, Path + "\\" + result.cFileName.TrimEnd('\0'))
            );
        }

        public IFileSystemObject GetChild(string name, bool file, OpenMode mode)
        {
            if (!fs.GetNamingConventions().Complies(name))
                throw new ArgumentException(string.Format("forbidden name: \"{0}\"", name), $"{name}");

            var newName = Path + "\\" + name;

            if (ChildExists(name, file)) {
                if ((mode & OpenMode.Existing) == 0)
                    throw new Exception(string.Format("The {0} {1} already exists.", file ? "file" : "folder", name));
            } else {
                if ((mode & OpenMode.New) == 0)
                    throw new Exception(string.Format("The {0} {1} does not exist.", file ? "file" : "folder", name));

                if (file)
                    PInvoke.CreateFile(newName, PInvoke.Access.None, PInvoke.ShareMode.ReadWrite, IntPtr.Zero, PInvoke.CreationDisposition.CREATE_NEW, 0, IntPtr.Zero).Close();
                else
                    PInvoke.CreateDirectory(newName + "\\", PInvoke.Access.None, PInvoke.ShareMode.ReadWrite, IntPtr.Zero, PInvoke.CreationDisposition.CREATE_NEW, PInvoke.FileFlags.BACKUP_SEMANTICS, IntPtr.Zero).Close();
            }

            if (file)
                return new WindowsFile(fs, newName).FileSystemObjectRef.Retain();
            else
                return new WindowsFolder(fs, newName).FileSystemObjectRef.Retain();
        }

        public bool ChildExists(string name, bool file)
        {
            return PInvoke.FindFiles(Path + "\\" + name).Any(result => ((result.dwFileAttributes & 0x10) == 0) == file);
        }
    }

}
