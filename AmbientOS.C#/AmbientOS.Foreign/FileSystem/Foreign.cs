﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AmbientOS.Environment;

namespace AmbientOS.FileSystem.Foreign
{
    public class InteropFileSystem : IFileSystemImpl
    {
        public IFileSystem FileSystemRef { get; }

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
            FileSystemRef = new FileSystemRef(this);

            this.root = root;
            this.folderConstructor = folderConstructor;

            var colon = root.ToLower().IndexOf(':');
            if (colon == 1)
                info = new DriveInfo(root.Substring(0, 1));
        }

        public InteropFileSystem(string root)
            : this(root, (fileSystem, path) => new InteropFolder(fileSystem, path).FolderRef.Retain())
        {
        }

        public NamingConventions GetNamingConventions()
        {
            return namingConventions;
        }

        public string GetName()
        {
            if (info != null)
                return info.VolumeLabel;

            return root;
        }

        public void SetName(string name)
        {
            if (info == null)
                throw new NotSupportedException("You can't rename this volume.");
            info.VolumeLabel = name;
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
            return new InteropFolder(null, path).FolderRef.Retain();
        }

        public static IFile GetFileFromPath(string path)
        {
            return new InteropFile(null, path).FileRef.Retain();
        }
    }

    public abstract class InteropFileSystemObject : IFileSystemObjectImpl
    {
        public IFileSystemObject FileSystemObjectRef { get; }

        public readonly InteropFileSystem fileSystem;
        public readonly string path;

        public InteropFileSystemObject(InteropFileSystem fileSystem, string path)
        {
            FileSystemObjectRef = new FileSystemObjectRef(this);
            this.fileSystem = fileSystem;
            this.path = path;
        }

        public IFileSystem GetFileSystem()
        {
            return fileSystem?.FileSystemRef.Retain();
        }

        public string GetPath()
        {
            return path;
        }

        public string GetName()
        {
            return Path.GetFileName(path);
        }

        public abstract void SetName(string name);
        public abstract FileTimes GetTimes();
        public abstract void SetTimes(FileTimes times);
        public abstract long? GetSize();
        public abstract long? GetSizeOnDisk();
        public abstract void Delete(DeleteMode mode);

        public void SecureDelete(int passes)
        {
            using (var fsObj = FileSystemObjectRef.Retain())
                fsObj.SecureDelete(passes, true);
        }
    }

    public class InteropFolder : InteropFileSystemObject, IFolderImpl
    {
        public IFolder FolderRef { get; }

        public InteropFolder(InteropFileSystem fileSystem, string path)
            : base(fileSystem, path)
        {
            FolderRef = new FolderRef(this);
        }

        public override void SetName(string name)
        {
            var dirPath = Path.GetDirectoryName(path);
            if (dirPath == null)
                throw new Exception("This is a root directory and hence cannot be renamed.");

            var newPath = dirPath + "\\" + name;
            if (Directory.Exists(newPath))
                throw new Exception("A folder with the same name already exists");
            Directory.Move(path, newPath);
        }

        public override FileTimes GetTimes()
        {
            var info = new DirectoryInfo(path);
            return new FileTimes() {
                CreatedTime = Directory.GetCreationTimeUtc(path),
                ReadTime = Directory.GetLastAccessTimeUtc(path),
                ModifiedTime = Directory.GetLastWriteTimeUtc(path)
            };
        }

        public override void SetTimes(FileTimes times)
        {
            Directory.SetCreationTimeUtc(path, times.CreatedTime.Value);
            Directory.SetLastAccessTimeUtc(path, times.ReadTime.Value);
            Directory.SetLastWriteTimeUtc(path, times.ModifiedTime.Value);
        }

        private long GetSize(DirectoryInfo dir)
        {
            return dir.EnumerateFiles("*", SearchOption.AllDirectories).Select(f => f.Length).Sum();
        }

        public override long? GetSize()
        {
            return GetSize(new DirectoryInfo(path));
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

        public IEnumerable<IFileSystemObject> GetChildren()
        {
            var files = Directory.GetFiles(path).Select(p => new InteropFolder(fileSystem, path + "\\" + p).FileSystemObjectRef.Retain());
            var folders = Directory.GetDirectories(path).Select(p => new InteropFolder(fileSystem, path + "\\" + p).FileSystemObjectRef.Retain());
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
                return new InteropFile(fileSystem, path + "\\" + name).FileSystemObjectRef.Retain();
            else
                return new InteropFolder(fileSystem, path + "\\" + name).FileSystemObjectRef.Retain();
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
        public IFile FileRef { get; }


        //private FileStream file = null;

        public InteropFile(InteropFileSystem fileSystem, string path)
            : base(fileSystem, path)
        {
            FileRef = new FileRef(this);
        }

        //private void OpenFile()
        //{
        //    lock (path)
        //        if (file == null)
        //            file = File.Open(path, FileMode.Open);
        //}
        //
        //private void CloseFile

        public override void SetName(string name)
        {
            var newPath = Path.GetDirectoryName(path) + "\\" + name;
            if (File.Exists(newPath))
                throw new Exception("A file with the same name already exists");
            File.Move(path, newPath);
        }

        public override FileTimes GetTimes()
        {
            var info = new FileInfo(path);
            return new FileTimes() {
                CreatedTime = File.GetCreationTimeUtc(path),
                ReadTime = File.GetLastAccessTimeUtc(path),
                ModifiedTime = File.GetLastWriteTimeUtc(path)
            };
        }

        public override void SetTimes(FileTimes times)
        {
            File.SetCreationTimeUtc(path, times.CreatedTime.Value);
            File.SetLastAccessTimeUtc(path, times.ReadTime.Value);
            File.SetLastWriteTimeUtc(path, times.ModifiedTime.Value);
        }

        public override long? GetSize()
        {
            return new FileInfo(path).Length;
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

        public void ChangeSize(long newSize)
        {
            using (var file = File.Open(path, FileMode.Open))
                file.SetLength(newSize);
        }

        public void Flush()
        {
            // the file is closed after each write, which flushes the cache automatically
        }

        public void AddCustomAppearance(Dictionary<string, string> dict, Type type)
        {
            if (type == typeof(IFile))
                ((IFile)this).AddCustomAppearance(dict);
        }
    }




    //[AOSApplication("FileSystem Mount service", Description = "Mounts a filesystem of a volume that is provided by the underlying host OS")]
    //public class InteropFileSystemService
    //{
    //    [AOSAction("info", "volume", "type=00000000-0000-0000-0000-000000000000")]
    //    public IAOSFileSystem Init(IAOSVolume volume, AOSShell shell, bool deliberate, LogContext log)
    //    {
    //        var v = volume;
    //        return new InteropFileSystem()
    //    }
    //}
}
