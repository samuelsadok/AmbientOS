using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace AmbientOS.FileSystem
{
    public class InteropFileSystem : IFileSystemImpl
    {
        public DynamicValue<string> Name { get; }

        private readonly string root;
        private readonly Func<InteropFileSystem, string, IFolder> folderConstructor;
        private readonly DriveInfo info; // can be null if this isn't a drive

        private readonly NamingConventions namingConventions = new NamingConventions() {
            MaxNameLength = 260,
            ForbiddenChars = new char[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*', '\uff3c' }.Concat(Enumerable.Range(0, 32).Select(i => Convert.ToChar(i))).ToArray(),
            ForbiddenLeadingChars = new char[0],
            ForbiddenTrailingChars = new char[] { ' ', '.' },
            ForbiddenNames = new string[] { "", ".", ".." },
            CaseSensitive = false
        };

        public InteropFileSystem(string root, Func<InteropFileSystem, string, IFolder> folderConstructor)
        {
            this.root = root;
            this.folderConstructor = folderConstructor;

            var colon = root.ToLower().IndexOf(':');
            if (colon == 1)
                info = new DriveInfo(root.Substring(0, 1));

            Name = new LambdaValue<string>(
                () => info != null ? info.VolumeLabel : root,
                val => {
                    if (info == null)
                        throw new NotSupportedException("You can't rename this volume.");
                    info.VolumeLabel = val;
                });
        }

        public InteropFileSystem(string root)
            : this(root, (fileSystem, path) => new InteropFolder(fileSystem, path).AsReference<IFolder>())
        {
        }

        public NamingConventions GetNamingConventions()
        {
            return namingConventions;
        }

        public IFolder GetRoot()
        {
            return folderConstructor(this, root);
        }

        public long? GetTotalSpace()
        {
            return null;
        }

        public long? GetFreeSpace()
        {
            return null;
        }

        public IEnumerable<string> GetFiles(string query)
        {
            throw new NotImplementedException();
        }

        public void Move(IFileSystemObject file, IFolder destination, string newName)
        {
            var nativeFile = file.AsImplementation<InteropFileSystemObject>();
            var nativeDestination = destination.AsImplementation<InteropFolder>();

            if (nativeFile.fileSystem != this)
                throw new ArgumentException();
            if (nativeDestination.fileSystem != this)
                throw new ArgumentException();

            var newPath = nativeDestination.path + "//" + newName;

            if (nativeFile is InteropFile) {
                if (File.Exists(newPath))
                    throw new Exception("There already exists a file with the same name at the destination");
                File.Move(nativeFile.path, newPath);
            } else {
                if (Directory.Exists(newPath))
                    throw new Exception("There already exists a folder with the same name at the destination");
                Directory.Move(nativeFile.path, newPath);
            }
        }

        public IFileSystemObject Copy(IFileSystemObject file, IFolder destination, string newName, MergeMode mode)
        {
            // todo: how to communicate that the caller should do this?
            throw new NotImplementedException();
        }

        public static IFolder GetFolderFromPath(string path)
        {
            return new InteropFolder(null, path).AsReference<IFolder>();
        }

        public static IFile GetFileFromPath(string path)
        {
            return new InteropFile(null, path).AsReference<IFile>();
        }
    }

    public abstract class InteropFileSystemObject : IFileSystemObjectImpl
    {
        public DynamicValue<string> Name { get; }
        public DynamicValue<string> Path { get; }
        public DynamicValue<FileTimes> Times { get; protected set; }

        public readonly InteropFileSystem fileSystem;
        public readonly string path;

        public InteropFileSystemObject(InteropFileSystem fileSystem, string path)
        {
            this.fileSystem = fileSystem;
            this.path = path;

            Name = new LambdaValue<string>(
                () => System.IO.Path.GetFileName(this.path),
                val => SetName(val)
                );

            Path = new LocalValue<string>(path);
        }

        public IFileSystem GetFileSystem()
        {
            return fileSystem?.AsReference<IFileSystem>();
        }

        protected abstract void SetName(string name);
        public abstract long? GetSizeOnDisk();
        public abstract void Delete(DeleteMode mode);

        public void SecureDelete(int passes)
        {
            using (var reference = this.AsReference<IFileSystemObject>())
                reference.SecureDelete(passes, true);
        }
    }

    public class InteropFolder : InteropFileSystemObject, IFolderImpl
    {
        public InteropFolder(InteropFileSystem fileSystem, string path)
            : base(fileSystem, path)
        {
            Times = new LambdaValue<FileTimes>(
                () => new FileTimes() {
                    CreatedTime = Directory.GetCreationTimeUtc(path),
                    ReadTime = Directory.GetLastAccessTimeUtc(path),
                    ModifiedTime = Directory.GetLastWriteTimeUtc(path)
                },
                val => {
                    Directory.SetCreationTimeUtc(path, val.CreatedTime.Value);
                    Directory.SetLastAccessTimeUtc(path, val.ReadTime.Value);
                    Directory.SetLastWriteTimeUtc(path, val.ModifiedTime.Value);
                });
        }

        protected override void SetName(string name)
        {
            var dirPath = System.IO.Path.GetDirectoryName(path);
            if (dirPath == null)
                throw new Exception("This is a root directory and hence cannot be renamed.");

            var newPath = dirPath + "\\" + name;
            if (Directory.Exists(newPath))
                throw new Exception("A folder with the same name already exists");
            Directory.Move(path, newPath);
        }

        public override long? GetSizeOnDisk()
        {
            return null;
        }

        public override void Delete(DeleteMode mode)
        {
            if (mode != DeleteMode.Permanent) {
                throw new NotImplementedException();
            }
            Directory.Delete(path, true);
        }

        public long? GetContentSize()
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Select(f => f.Length).Sum();
        }

        public IEnumerable<IFileSystemObject> GetChildren()
        {
            var files = Directory.GetFiles(path).Select(p => new InteropFolder(fileSystem, path + "\\" + p).AsReference<IFileSystemObject>());
            var folders = Directory.GetDirectories(path).Select(p => new InteropFolder(fileSystem, path + "\\" + p).AsReference<IFileSystemObject>());
            return files.Concat(folders);
        }

        public IFileSystemObject GetChild(string name, bool file, OpenMode mode)
        {
            if (ChildExists(name, file)) {
                if (!mode.HasFlag(OpenMode.Existing))
                    throw new FileLoadException("the " + (file ? "file" : "folder") + " already exists", name);
            } else {
                if (!mode.HasFlag(OpenMode.New))
                    throw new FileNotFoundException("the " + (file ? "file" : "folder") + " does not exist", name);
                
                if (file)
                    File.Create(path + "\\" + name).Dispose();
                else
                    Directory.CreateDirectory(path + "\\" + name);
            }

            if (file)
                return new InteropFile(fileSystem, path + "\\" + name).AsReference<IFileSystemObject>();
            else
                return new InteropFolder(fileSystem, path + "\\" + name).AsReference<IFileSystemObject>();
        }


        public bool ChildExists(string name, bool file)
        {
            if (file)
                return File.Exists(path + "\\" + name);
            else
                return Directory.Exists(path + "\\" + name);
        }
    }

    public class InteropFile : InteropFileSystemObject, IFileImpl
    {
        public DynamicValue<string> Type { get; }
        public DynamicValue<long?> Length { get; }

        public InteropFile(InteropFileSystem fileSystem, string path)
            : base(fileSystem, path)
        {
            Times = new LambdaValue<FileTimes>(
                () => new FileTimes() {
                    CreatedTime = File.GetCreationTimeUtc(path),
                    ReadTime = File.GetLastAccessTimeUtc(path),
                    ModifiedTime = File.GetLastWriteTimeUtc(path)
                },
                val => {
                    File.SetCreationTimeUtc(path, val.CreatedTime.Value);
                    File.SetLastAccessTimeUtc(path, val.ReadTime.Value);
                    File.SetLastWriteTimeUtc(path, val.ModifiedTime.Value);
                });

            Type = this.GetStreamTypeFromFileName();

            Length = new LambdaValue<long?>(
                () => new FileInfo(path).Length,
                val => {
                    if (val == null)
                        throw new ArgumentNullException($"{val}");
                    using (var file = File.Open(path, FileMode.Open))
                        file.SetLength(val.Value);
                });
        }

        protected override void SetName(string name)
        {
            var newPath = System.IO.Path.GetDirectoryName(path) + "\\" + name;
            if (File.Exists(newPath))
                throw new Exception("A file with the same name already exists");
            File.Move(path, newPath);
        }

        public override long? GetSizeOnDisk()
        {
            return null;
        }

        public override void Delete(DeleteMode mode)
        {
            if (mode != DeleteMode.Permanent) {
                throw new NotImplementedException();
            }
            File.Delete(path);
        }

        public void Read(long offset, long count, byte[] buffer, long bufferOffset)
        {
            using (var file = File.Open(path, FileMode.Open)) {
                if (offset + count > file.Length)
                    throw new ArgumentOutOfRangeException();

                // todo: check access rights, locks
                file.Seek(offset, SeekOrigin.Begin);

                while (count > 0) {
                    var delta = file.Read(buffer, (int)bufferOffset, (int)count);
                    count -= delta;
                    bufferOffset += delta;
                }
            }
        }

        public void Write(long offset, long count, byte[] buffer, long bufferOffset)
        {
            using (var file = File.Open(path, FileMode.Open)) {
                if (offset + count > file.Length)
                    throw new ArgumentOutOfRangeException();

                file.Seek(offset, SeekOrigin.Begin);

                file.Write(buffer, (int)bufferOffset, (int)count);
            }
        }
        
        public void Flush()
        {
            // the file is closed after each write, which flushes the cache automatically
        }
    }
}
