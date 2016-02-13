using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem
{
    public static class Extensions
    {
        /// <summary>
        /// Adds the "ext" appearance attribute to this file.
        /// todo: this method and IExtensionProperties are hacky. Think about a more general way of supporting extension functions
        /// </summary>
        public static void AddCustomAppearance(this IFile file, Dictionary<string, string> dict)
        {
            var name = file.Name.GetValue();
            var lastPoint = name.LastIndexOf('.');
            if (lastPoint >= 0)
                dict["ext"] = name.Substring(lastPoint + 1);
        }

        /// <summary>
        /// Reads part of the file into a buffer
        /// </summary>
        public static byte[] Read(this IFile file, long offset, long length)
        {
            var buffer = new byte[length];
            file.Read(offset, length, buffer, 0);
            return buffer;
        }

        /// <summary>
        /// Reads the entire file into a buffer
        /// </summary>
        public static byte[] Read(this IFile file)
        {
            // todo: lock file

            var size = file.Size.GetValue();
            if (!size.HasValue)
                throw new InvalidOperationException("unknown file size");

            return file.Read(0, size.Value);
        }

        /// <summary>
        /// Writes the specified content to the file (starting at the beginning).
        /// The file size is changed to the buffer size.
        /// </summary>
        public static void Write(this IFile file, byte[] buffer)
        {
            file.Size.SetValue(buffer.Length);
            file.Write(0, buffer.Length, buffer, 0);
        }

        /// <summary>
        /// Appends the provided data to the end of the file.
        /// </summary>
        public static void Append(this IFile file, byte[] value)
        {
            // todo: lock file
            var offset = file.Size.GetValue().Value;
            file.Size.SetValue(offset + value.Count());
            file.Write(offset, value.Count(), value, 0);
        }

        /// <summary>
        /// Returns the specified file contained by this folder.
        /// </summary>
        public static IFile GetFile(this IFolder folder, string name, OpenMode mode)
        {
            return folder.GetChild(name, true, mode).Cast<IFile>();
        }

        /// <summary>
        /// Returns the specified subfolder contained by this folder.
        /// </summary>
        public static IFolder GetFolder(this IFolder folder, string name, OpenMode mode)
        {
            return folder.GetChild(name, false, mode).Cast<IFolder>();
        }

        /// <summary>
        /// Navigates to the file with the specified path.
        /// </summary>
        /// <param name="path">Must be separated by slashes ('/') and path elements must be escaped like a URL. The path is interpreted in a very exact way, e.g. "//" is interpreted as a folder with an empty name.</param>
        public static IFile NavigateToFile(this IFolder folder, string path, OpenMode mode)
        {
            var names = path.Split('/').ToArray();
            foreach (var name in names.Take(names.Count() - 1))
                folder = folder.GetFolder(name, mode);
            return folder.GetFile(names.Last(), mode);
        }

        /// <summary>
        /// Navigates to the folder with the specified path.
        /// </summary>
        /// <param name="path">Must be separated by slashes ('/') and path elements must be escaped like a URL. The path is interpreted in a very exact way, e.g. "//" is interpreted as a folder with an empty name.</param>
        public static IFolder NavigateToFolder(this IFolder folder, string path, OpenMode mode)
        {
            var names = path.Split('/').ToArray();
            foreach (var name in names)
                folder = folder.GetFolder(name, mode);
            return folder;
        }

        /// <summary>
        /// Indicates whether the specified file exists as a direct child of this folder.
        /// </summary>
        public static bool FileExists(this IFolder folder, string name)
        {
            return folder.ChildExists(name, true);
        }

        /// <summary>
        /// Indicates whether the specified subfolder exists as a direct child of this folder.
        /// </summary>
        public static bool FolderExists(this IFolder folder, string name)
        {
            return folder.ChildExists(name, false);
        }

        /// <summary>
        /// Moves the specified file or folder to the new location.
        /// If source and destination folder are on the same file system, this operation is usually very fast (for any size).
        /// If the operation is cancelled, the file or folder will remain unaltered in the old location.
        /// </summary>
        /// <param name="destination">The destination folder.</param>
        /// <param name="newName">The new name of the file or folder being moved.</param>
        /// <param name="local">Normally, if both source and destination are on the same file system, the operation is delegated to the file system. This parameter can be set to true to prevent this delegation.</param>
        public static void Move(this IFileSystemObject file, IFolder destination, string newName, bool local = false)
        {
            var fs1 = file.GetFileSystem();
            var fs2 = destination.GetFileSystem();

            if (fs1 == fs2 && fs1 != null && !local) {
                fs1.Move(file, destination, newName);
            } else {
                var newFile = file.Copy(destination, newName, MergeMode.Abort);
                file.Delete(DeleteMode.Permanent); // todo: delete securely
                file.Substitute(newFile);
            }
        }

        public static T Copy<T>(this T obj, IFolder destination, string newName, MergeMode mode)
            where T : IFileSystemObject
        {
            return obj.Copy(destination, newName, mode, false).Cast<T>();
        }

        /// <summary>
        /// Copies the specified file or folder to the new location.
        /// </summary>
        /// <param name="destination">The destination folder.</param>
        /// <param name="newName">The new name of the file or folder being copied.</param>
        /// <param name="local">Normally, if both source and destination are on the same file system, the operation is delegated to the file system. This parameter can be set to true to prevent this delegation.</param>
        public static IFileSystemObject Copy(this IFileSystemObject obj, IFolder destination, string newName, MergeMode mode, bool local = false)
        {
            var fs1 = obj.GetFileSystem();
            var fs2 = destination.GetFileSystem();

            if (fs1 == fs2 && fs1 != null && !local)
                return fs1.Copy(obj, destination, newName, mode);

            // todo: lock destination

            var isFile = obj is IFile;
            IFileSystemObject newObj = null;

            // apply merge decision
            if (destination.ChildExists(newName, isFile)) {
                var otherObj = destination.GetChild(newName, isFile, OpenMode.Existing);
                if (!isFile && mode.HasFlag(MergeMode.Merge)) {
                    newObj = otherObj;
                } else {
                    string alternativeName;
                    switch (mode & ~MergeMode.Merge) {
                        case MergeMode.Abort: throw new Exception("the file already exists in the destination");
                        case MergeMode.Evict: otherObj.Delete(DeleteMode.Permanent); break; // todo: delete securely
                        case MergeMode.Skip: return null;
                        case MergeMode.Both:
                            for (long i = 0; destination.ChildExists(alternativeName = string.Format("{0} ({1})", newName, i), isFile); i++) ;
                            newName = alternativeName;
                            break;
                        case MergeMode.Newer:
                        case MergeMode.Older:
                            var myTime = obj.Times.GetValue().ModifiedTime.Value;
                            var otherTime2 = otherObj.Times.GetValue().ModifiedTime.Value;
                            if (myTime == otherTime2)
                                throw new Exception("cant decide on the right file, times are equal");
                            if ((myTime < otherTime2) != (mode == MergeMode.Older))
                                return null;
                            break;
                    }
                }
            }

            if (newObj == null)
                newObj = destination.GetChild(newName, isFile, OpenMode.New);

            // todo: improve parallelism

            if (isFile) {
                var file = obj.Cast<IFile>();
                var newFile = newObj.Cast<IFile>();

                var length = file.Size.GetValue().Value;
                newFile.Size.SetValue(length);

                // do a 16MB block copy
                var block = new byte[16777216];
                long offset = 0;
                while (length > 0) {
                    var blockSize = Math.Min(block.Length, length);
                    file.Read(offset, blockSize, block, 0);
                    newFile.Write(offset, blockSize, block, 0);
                    offset += blockSize;
                    length -= blockSize;
                }
            } else {
                var folder = obj.Cast<IFolder>();
                var newFolder = newObj.Cast<IFolder>();
                
                foreach (var child in folder.GetChildren())
                    child.Copy(newFolder, child.Name.GetValue(), mode);
            }

            return newObj;
        }

        public static void SecureDelete(this IFileSystemObject obj, int passes, bool local = false)
        {
            if (!local) {
                obj.SecureDelete(passes);
                return;
            }

            if (obj is IFile)
                ((IFile)obj).SecureDeleteEx(passes);
            else if (obj is IFolder)
                ((IFolder)obj).SecureDeleteEx(passes);
            else
                throw new Exception("cannot delete file system object of type " + obj.GetType().ToString());
        }

        public static void SecureDeleteEx(this IFolder folder, int passes)
        {
            foreach (var child in folder.GetChildren())
                child.SecureDelete(passes);

            folder.Delete(DeleteMode.Permanent);
        }

        /// <summary>
        /// Deletes a file by overwriting it's data with data from a random number generator conjectured to be computationally secure.
        /// This also scrables file times. Currently, the file name is not scrabled.
        /// This does not delete any data in caches or in log files.
        /// </summary>
        public static void SecureDeleteEx(this IFile file, int passes)
        {
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] randomData = new byte[4096];
            var fileSize = file.Size.GetValue().Value;

            for (int i = 0; i < passes; i++) {
                // scramble data
                long offset = 0;
                while (offset < fileSize) {
                    var blockSize = Math.Min(randomData.Length, fileSize - offset);
                    rng.GetBytes(randomData);
                    file.Write(offset, blockSize, randomData, 0);
                    offset += blockSize;
                }

                // scramble times
                file.Times.SetValue(new FileTimes() {
                    CreatedTime = new DateTime(rng.GetLong()),
                    ModifiedTime = new DateTime(rng.GetLong()),
                    ReadTime = new DateTime(rng.GetLong()),
                });

                file.Flush();
            }

            // scramble file size and name
            for (int i = 0; i < passes; i++) {
                // todo: scramble file name without violating naming conventions
                file.Size.SetValue(rng.GetLong(0, fileSize));
            }

            file.Delete(DeleteMode.Permanent);
        }

        /// <summary>
        /// This is absolute garbage. When you use min and max, security goes out the window.
        /// </summary>
        public static long GetLong(this RandomNumberGenerator rng, long min = 0, long max = long.MaxValue)
        {
            var val = new byte[8];
            rng.GetBytes(val);
            var result = val.ReadInt64(0, Endianness.Current);
            if (max < long.MaxValue || min > 0)
                result = (result % ((max - min) + 1)) + min; // todo: find a secure way to do this, this unbalances probabilities for some output ranges
            return result;
        }


        /// <summary>
        /// Verifies if the specified name complies to these conventions.
        /// </summary>
        public static bool Complies(this NamingConventions c, string name)
        {
            // todo: throw exceptions instead of returning result (=> more detail)
            if (c.MaxNameLength >= 0 && name.Length > c.MaxNameLength)
                return false;
            if (name.IndexOfAny(c.ForbiddenChars) >= 0)
                return false;
            if (name.Length > 0) {
                if (c.ForbiddenLeadingChars.Contains(name[0]))
                    return false;
                if (c.ForbiddenTrailingChars.Contains(name[name.Length - 1]))
                    return false;
            }
            if (c.ForbiddenNames.Any(n => n == name))
                return false;
            return true;
        }



        // public static TOut Mount<TOut>(this IAOSFile file, IShell shell, LogContext logContext)
        //     where TOut : IAOSObject
        // {
        //     return ObjectStore.Action<IAOSFile, TOut>(file, "mount", shell, logContext);
        // }



        /// <summary>
        /// Substitutes the value of the specified object reference with the new specified value.
        /// This allows various tricks such as moving files across file system boundaries while they are in use.
        /// </summary>
        public static void Substitute(this IObjectRef obj, IObjectRef newValue)
        {
            // todo: think about how this can be done
            // maybe we need to automatically generate a wrapper class for each interface:
            //   class AOSFileRef : IAOSObjectRef, IAOSFile { IAOSFile value; ... }
            // and then provide an extension function like this:
            //   AOSFileRef Wrap(this IAOSFile object) { ... }
            //   void Substitute(this IAOSFile object, IAOSFile value) { 
            //       ((IAOSFileRef)object).SetValue(value);
            //   }

            throw new NotImplementedException();
        }
    }
}
