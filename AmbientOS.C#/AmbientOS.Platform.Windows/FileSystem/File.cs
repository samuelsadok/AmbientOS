using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AmbientOS.FileSystem
{
    abstract class WindowsFileSystemObject : IFileSystemObjectImpl, IDisposable
    {
        public DynamicValue<string> Name { get; }
        public DynamicValue<string> Path { get; }
        public DynamicValue<FileTimes> Times { get; }

        private readonly WindowsFolder parent;
        protected readonly PInvoke.ByHandleFileInformation info;
        protected readonly IFileSystem fs;

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
            this.fs = fs;

            // get file info
            using (var file = OpenFile(PInvoke.Access.None)) {
                info = PInvoke.GetFileInformationByHandle(file);
            }


            Path = new LocalValue<string>(path);

            Name = new LambdaValue<string>(
                () => {
                    var currentPath = Path.Get();
                    var delimiter = currentPath.LastIndexOfAny(new char[] { '\\', '/' });
                    return currentPath.Substring(delimiter + 1);
                },
                val => {
                    using (var root = parent.OpenFile(0))
                    using (var file = OpenFile(0))
                        PInvoke.SetFileInformationByHandle(file, new PInvoke.FileRenameInfo() {
                            ReplaceIfExists = 0,
                            RootDirectory = root.DangerousGetHandle(),
                            FileName = val
                        });
                    // todo: update path
                });

            Times = new LambdaValue<FileTimes>(
                () => new FileTimes() {
                    CreatedTime = info.ftCreationTime,
                    ReadTime = info.ftLastAccessTime,
                    ModifiedTime = info.ftLastWriteTime
                },
                val => {
                    if (val.CreatedTime.HasValue)
                        info.ftCreationTime = val.CreatedTime.Value;
                    if (val.ReadTime.HasValue)
                        info.ftLastAccessTime = val.ReadTime.Value;
                    if (val.ModifiedTime.HasValue)
                        info.ftLastWriteTime = val.ModifiedTime.Value;

                    using (var file = OpenFile(0))
                        PInvoke.SetFileInformationByHandle(file, new PInvoke.FileBasicInfo() {
                            CreationTime = info.ftCreationTime,
                            LastAccessTime = info.ftLastAccessTime,
                            LastWriteTime = info.ftLastWriteTime,
                            ChangeTime = info.ftLastWriteTime,
                            FileAttributes = info.dwFileAttributes
                        });
                });
        }

        public void Dispose()
        {
            Handle?.Dispose();
        }

        public IFileSystem GetFileSystem()
        {
            return fs;
        }

        public long? GetSizeOnDisk()
        {
            return null; // todo: determine size on disk using Windows fragmentation API
        }

        public abstract void Delete(DeleteMode mode);

        public void SecureDelete(int passes)
        {
            using (var reference = this.AsReference<IFileSystemObject>())
                reference.SecureDelete(passes, true);
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
        public DynamicValue<string> Type { get; }
        public DynamicValue<long?> Length { get; }

        public WindowsFile(IFileSystem fs, string path)
            : base(fs, path)
        {
            Type = this.GetStreamTypeFromFileName();

            Length = new LambdaValue<long?>(
                () => ((long)info.nFileSizeHigh << 32) | (info.nFileSizeLow & 0xFFFFFFFF),
                val => { throw new NotImplementedException(); });
        }

        protected override SafeFileHandle OpenFile(PInvoke.Access access)
        {
            return PInvoke.CreateFile(Path.Get(), access, PInvoke.ShareMode.ReadWrite, IntPtr.Zero, PInvoke.CreationDisposition.OPEN_EXISTING, PInvoke.FileFlags.OVERLAPPED, IntPtr.Zero);
        }

        public override void Delete(DeleteMode mode)
        {
            if (mode != DeleteMode.Permanent)
                throw new NotImplementedException();
            PInvoke.DeleteFile(Path.Get());
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
    }

    class WindowsFolder : WindowsFileSystemObject, IFolderImpl
    {
        protected override SafeFileHandle OpenFile(PInvoke.Access access)
        {
            return PInvoke.CreateDirectory(Path + "\\", access, PInvoke.ShareMode.ReadWrite, IntPtr.Zero, PInvoke.CreationDisposition.OPEN_EXISTING, PInvoke.FileFlags.OVERLAPPED | PInvoke.FileFlags.BACKUP_SEMANTICS, IntPtr.Zero);
        }

        public WindowsFolder(IFileSystem fs, string path)
            : base(fs, path)
        {
        }

        public override void Delete(DeleteMode mode)
        {
            if (mode != DeleteMode.Permanent)
                throw new NotImplementedException();
            PInvoke.RemoveDirectory(Path + "\\");
        }

        public long? GetContentSize()
        {
            long? size = 0;

            foreach (var child in GetChildren()) {
                var file = child as WindowsFile;
                if (file != null)
                    size += file.Length.Get();

                var folder = child as WindowsFolder;
                if (folder != null)
                    size += folder.GetContentSize();
            }

            return size;
        }

        private IEnumerable<WindowsFileSystemObject> GetChildren()
        {
            return PInvoke.FindFiles(Path + "\\*").Where(result => !result.cFileName.StartsWith(".\0") && !result.cFileName.StartsWith("..\0")).Select(result =>
                (result.dwFileAttributes & 0x10) == 0 ?
                (WindowsFileSystemObject)new WindowsFile(fs, Path + "\\" + result.cFileName.TrimEnd('\0')) : // todo: include zero termination to byte converter
                (WindowsFileSystemObject)new WindowsFolder(fs, Path + "\\" + result.cFileName.TrimEnd('\0'))
            );
        }

        IEnumerable<IFileSystemObject> IFolderImpl.GetChildren()
        {
            return GetChildren().Select(child => child.AsReference<IFileSystemObject>());
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
                return new WindowsFile(fs, newName).AsReference<IFile>();
            else
                return new WindowsFolder(fs, newName).AsReference<IFolder>();
        }

        public bool ChildExists(string name, bool file)
        {
            return PInvoke.FindFiles(Path + "\\" + name).Any(result => ((result.dwFileAttributes & 0x10) == 0) == file);
        }
    }

}
