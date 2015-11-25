using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace AmbientOS.FileSystemDeprecated
{
/*   
    //public static class Names
    //{
    //    // todo: rework the concept of object appearances: integrate it into the interface definition
    //    public const string AOS_DISK = "disk";
    //    public const string AOS_VOLUME = "volume";
    //    public const string AOS_FILESYSTEM = "fs";
    //    public const string AOS_FSOBJECT = "fsobj";
    //    public const string AOS_FOLDER = "folder";
    //    public const string AOS_FILE = "file";
    //}

    public enum FileSystemFlags
    {
        Hidden = 0x1,
        SoftLink = 0x2,
        Sparse = 0x4,
        Compressed = 0x8,
        Encrypted = 0x10
    }


    /// <summary>
    /// Specifies the mode of file/folder creation.
    /// </summary>
    public enum OpenMode
    {
        /// <summary>
        /// Request the creation of a new file or folder.
        /// If a file or folder with the same name already exists, the create method shall throw an exception.
        /// </summary>
        New = 1,

        /// <summary>
        /// Request an existing file or folder.
        /// If the specified file or folder does not exist, the create method shall throw an exception.
        /// </summary>
        Existing = 2,

        /// <summary>
        /// The file or folder is only created if it doesn't already exist, otherwise the existing one is returned.
        /// </summary>
        NewOrExisting = 3
    }


    /// <summary>
    /// Specifies the mode of file/folder deletion.
    /// If deletion is supported on a given object, at least the Permanent option must be supported.
    /// </summary>
    public enum DeleteMode
    {
        /// <summary>
        /// The file or folder is deleted quickly and the allocated disk space becomes free.
        /// This cannot be undone.
        /// Choose this option for temporary files that contain no user data or if the user explicitly requests it.
        /// </summary>
        Permanent,

        /// <summary>
        /// The file is moved to the trash associated with the containing file system and may automatically be deleted after some time period.
        /// Choose this option if the user requested the deletion.
        /// </summary>
        Trash,

        /// <summary>
        /// The associated disk space (including file system structures and log file content) shall be overwritten with the output of a computationally secure pseudorandom generator.
        /// The Delete function must not return until the operation is committed to disk.
        /// If the filesystem driver does not support this mode, it shall throw an exception.
        /// Choose this option for temporary files that may contain user data or if the user explicitly requests it.
        /// </summary>
        //Secure
    }

    /// <summary>
    /// Specifies the action that is taken if a conflict arises while copying.
    /// </summary>
    public enum MergeMode
    {
        /// <summary>
        /// If a conflict arises, abort the operation
        /// </summary>
        Abort = 0x0,

        /// <summary>
        /// Override the file at the destination
        /// </summary>
        Evict = 0x1,

        /// <summary>
        /// Don't copy conflicting files
        /// </summary>
        Skip = 0x2,

        /// <summary>
        /// Keep both files and rename the file being copied
        /// </summary>
        Both = 0x3,

        /// <summary>
        /// Keep the file with the most recent modified time
        /// </summary>
        Newer = 0x4,

        /// <summary>
        /// Keep the file with the oldest modified time
        /// </summary>
        Older = 0x5,

        /// <summary>
        /// If the conflicting objects are folders, merge them recursively.
        /// This flag can be combined with any of the other options (except for Abort).
        /// </summary>
        Merge = 0x8
    }


    public class DiskInfo
    {
        /// <summary>
        /// The ID reported by the hard disk.
        /// Be careful when using this, it could be modified and non-unique, especially for virtual disks.
        /// </summary>
        public Guid ID;

        /// <summary>
        /// The total number of tracks on the disk.
        /// In most cases (i.e. for normal hard drives, SSDs, and most optical disks), this will be 1.
        /// There are however some CD's that have multiple tracks.
        /// </summary>
        public int Tracks;

        /// <summary>
        /// The total number of sectors on one track of the disk.
        /// </summary>
        public long Sectors;

        /// <summary>
        /// The maximum number of sectors the disk could be expanded to.
        /// For most real disks, this will be the same as the current sector count.
        /// </summary>
        public long MaxSectors;

        /// <summary>
        /// The number of bytes per sector.
        /// Most of the time, this will be 512, and sometimes 4096, but you should not rely on that assumption.
        /// </summary>
        public long BytesPerSector;
    }


    public class VolumeInfo
    {
        /// <summary>
        /// The ID of the volume. Volumes on a disk using the MBR partitioning scheme get an ID derived from the disk ID and volume offet.
        /// Be careful when using this, it could be modified and non-unique, especially for virtual disks.
        /// </summary>
        public Guid ID;

        /// <summary>
        /// The type of the volume, as specified in the partition table.
        /// Zero if unknown. For legacy MBR partitions, only the last byte specifies the type and the rest is zero.
        /// </summary>
        public Guid Type;

        public FileSystemFlags flags;
    }

    public class VolumeExtent
    {
        /// <summary>
        /// The track of the disk on which the extent resides. In most cases this is 0, since there's mostly only one track.
        /// </summary>
        public int Track;

        /// <summary>
        /// The absolute start sector of the extent on the underlying disk.
        /// </summary>
        public long StartSector;

        /// <summary>
        /// The total number of sectors in the extent.
        /// </summary>
        public long Sectors;

        /// <summary>
        /// The maximum number of sectors this extent could be expanded to.
        /// If there is no free space on disk following the extent, this is the same as the current sector count.
        /// </summary>
        public long MaxSectors;

        /// <summary>
        /// The underlying disk.
        /// </summary>
        public IDisk Disk;
    }


    /// <summary>
    /// Contains different kinds of times associated with the file.
    /// All times should be in UTC.
    /// For reading, null values indicate that the particular value is not available.
    /// For writing, set the fields to null that shouldn't be updated.
    /// </summary>
    public struct FileTimes
    {
        /// <summary>
        /// Time when the file or folder was created.
        /// AmbientOS file system drivers shall initialize this field automatically.
        /// </summary>
        public DateTime? CreatedTime;

        /// <summary>
        /// Time when the file was modified.
        /// Semantics not clearly defined for folders.
        /// AmbientOS file system drivers shall update this field automatically.
        /// </summary>
        public DateTime? ModifiedTime;

        /// <summary>
        /// Time when the file was last read.
        /// AmbientOS file system drivers shall NOT update this field automatically, hence it is of limited value.
        /// </summary>
        public DateTime? ReadTime;
    }

    public class NamingConventions
    {
        /// <summary>
        /// All characters that must not occur anywhere of a name.
        /// </summary>
        public char[] ForbiddenChars;

        /// <summary>
        /// All characters that must not occur at the beginning of a name.
        /// </summary>
        public char[] ForbiddenLeadingChars;

        /// <summary>
        /// All characters that must not occur at the end of a name.
        /// </summary>
        public char[] ForbiddenTrailingChars;

        /// <summary>
        /// All strings that must not be used as a file or folder name.
        /// </summary>
        public string[] ForbiddenNames;

        /// <summary>
        /// The maximum number of chars in the name.
        /// Set to -1 to indicate no limit.
        /// </summary>
        public int MaxNameLength;

        /// <summary>
        /// Indicates whether the file system uses case sensitive name comparision.
        /// </summary>
        public bool CaseSensitive;
    }


    /// <summary>
    /// A disk that consists of a fixed or dynamic number of sectors
    /// </summary>
    //[AOSInterface(Names.AOS_DISK)]
    public interface IDisk : IObjectRef
    {
        /// <summary>
        /// Returns information about the disk.
        /// </summary>
        DiskInfo GetInfo();

        /// <summary>
        /// Changes the number of sectors in the disk. This may be possible for virtual disks.
        /// This affects all tracks equally.
        /// </summary>
        void SetSize(long sectorCount);

        /// <summary>
        /// Reads the specified sectors.
        /// The method shall fail if the requested range is out of bounds.
        /// </summary>
        /// <param name="offset">The sector number (starting at 0).</param>
        /// <param name="count">The number of sectors to read.</param>
        void Read(int track, long offset, long count, byte[] buffer, long bufferOffset);

        /// <summary>
        /// Writes to the specified sectors.
        /// The method shall grow the disk if the range is out of bounds (if possible).
        /// </summary>
        /// <param name="offset">The sector number (starting at 0).</param>
        /// <param name="count">The number of sectors to read.</param>
        void Write(int track, long offset, long count, byte[] buffer, long bufferOffset);
    }


    /// <summary>
    /// A volume that consists of a fixed or dynamic number of sectors
    /// </summary>
    //[AOSInterface(Names.AOS_VOLUME)]
    public interface IVolume : IObjectRef
    {
        /// <summary>
        /// Returns information about the volume.
        /// </summary>
        VolumeInfo GetInfo();

        /// <summary>
        /// Returns the extents that make up this volume.
        /// In most cases, the result contains a single element.
        /// The result may be incomplete, for instance if the volume is partially virtual.
        /// </summary>
        VolumeExtent[] GetExtents();

        /// <summary>
        /// Returns size of the volume in bytes.
        /// </summary>
        long GetSize();

        /// <summary>
        /// Changes size of the volume.
        /// This may be possible on a virtual volume or if there is unused disk space following the last volume extent.
        /// Returns the actual size that was set. This may be different from the requested size if it's too large or not sector-aligned.
        /// </summary>
        /// <param name="size">New size in bytes.</param>
        long SetSize(long size);

        /// <summary>
        /// Reads from the volume.
        /// The method shall fail if the requested range is out of bounds.
        /// </summary>
        /// <param name="offset">The offset in bytes, where to start reading.</param>
        /// <param name="count">The number of bytes to read.</param>
        void Read(long offset, long count, byte[] buffer, long bufferOffset);

        /// <summary>
        /// Writes to the volume.
        /// The method shall fail if the requested range is out of bounds.
        /// </summary>
        /// <param name="offset">The offset in bytes, where to start writing.</param>
        /// <param name="count">The number of bytes to write.</param>
        void Write(long offset, long count, byte[] buffer, long bufferOffset);
    }

    /// <summary>
    /// Exposes a file system.
    /// </summary>
    //[AOSInterface(Names.AOS_FILESYSTEM)]
    public interface IFileSystem : IObjectRef
    {
        /// <summary>
        /// Returns the human-readable volume name.
        /// Shall return null if there is no name available.
        /// </summary>
        string GetName();

        /// <summary>
        /// Sets the volume name.
        /// </summary>
        /// <param name="file">The new volume name</param>
        void SetName(string name);

        /// <summary>
        /// Returns the naming conventions for this file system.
        /// </summary>
        NamingConventions GetNamingConventions();

        /// <summary>
        /// Returns the root folder of this file system.
        /// </summary>
        IFolder GetRoot();

        /// <summary>
        /// Returns the total size of the volume in bytes.
        /// Shall return null if this is not applicable.
        /// </summary>
        long? GetTotalSpace();

        /// <summary>
        /// Returns the free space on the volume in bytes.
        /// Shall return null if this is not applicable.
        /// </summary>
        long? GetFreeSpace();
        */
        /// <summary>
        /// Runs a search query on the file system and returns all matching files and folders.
        /// Search query examples (todo: decide on case sensitivity):
        /// abc.txt     returns all files with the name "abc.txt"
        /// *.txt       returns all files with the .txt extension (including the file named ".txt")
        /// *a*c*       returns all files that have "a" and "c" in their name in this order
        /// /abc.txt    returns all files named "abc.txt" in the root folder
        /// /*/abc.txt  returns all files named "abc.txt" in any direct subfolder of the root folder
        /// abc/**/def.txt returns all files named "def.txt" in any subfolder of a folder named "def.txt"
        /// projects/*/build/**/*.exe   returns all exe files that are at some point contained in a "build" folder that have
        /// 
        /// todo: rethink this - implemantation probably too complicated
        /// </summary>
        /// <param name="query">The query string (see remarks).</param>
        /*IEnumerable<string> GetFiles(string query);

        /// <summary>
        /// Moves a file or folder within this file system.
        /// </summary>
        /// <param name="file">The file or folder being moved. If this doesn't belong to this file system, the method shall fail.</param>
        /// <param name="destination">The destination of the move operation. If this doesn't belong to this file system, the method shall fail.</param>
        /// <param name="newName">The new name of the file or folder being moved.</param>
        void Move(IFileSystemObject file, IFolder destination, string newName);

        /// <summary>
        /// Copies a file or folder within this file system.
        /// Returns the object at the destination.
        /// </summary>
        /// <param name="file">The file or folder being copied. If this doesn't belong to this file system, the method shall fail.</param>
        /// <param name="destination">The destination of the copy operation. If this doesn't belong to this file system, the method shall fail.</param>
        /// <param name="newName">The new name of the file or folder being copied.</param>
        /// <param name="mode">The behavior in case of conflicts</param>
        IFileSystemObject Copy(IFileSystemObject file, IFolder destination, string newName, MergeMode mode);
    }


    //[AOSInterface(Names.AOS_FSOBJECT)]
    [AOSAttribute("name", "GetName")]
    public interface IFileSystemObject : IObjectRef
    {
        /// <summary>
        /// Returns the file system that contains this file or folder.
        /// Returns null if the object does not belong to a filesystem (e.g. for a virtual folder).
        /// </summary>
        IFileSystem GetFileSystem();

        /// <summary>
        /// todo: think about what we really want
        /// do we want a path that uniquely identifies the object value?
        /// relative to what? another object reference? the current kernel realm? globally?
        /// or do we just want a path that looks nice to a user?
        /// 
        /// to what else can this be applied? any type of object?
        /// an object reference?
        /// can this be combined with a more complex path class, that
        /// would also allow estimation of (different metrics of) cost?
        /// maybe an object reference can contain multiple paths and the client can select
        /// which one it wants to use.
        /// (e.g. the same file may be reachable via bluetooth but also via USB)
        /// </summary>
        string GetPath();

        /// <summary>
        /// Returns the name of the file or folder.
        /// Returns null if the name is not available.
        /// </summary>
        string GetName();

        /// <summary>
        /// Renames the file or folder.
        /// Caution should be taken when allowing this: it may be possible to misuse this to query the existance of files in the parent folder.
        /// todo: think about what names we allow on what file systems (e.g. should we allow on NTFS names that would be invalid in windows? should this be a setting?)
        /// </summary>
        void SetName(string name);

        /// <summary>
        /// Returns various times about the file.
        /// </summary>
        FileTimes GetTimes();

        /// <summary>
        /// Updates the time fields of this file.
        /// </summary>
        void SetTimes(FileTimes times);

        /// <summary>
        /// Returns the total size of the file or folder in bytes.
        /// For folders the size is determined recursively.
        /// Returns null if the size value is not available.
        /// </summary>
        long? GetSize();

        /// <summary>
        /// Returns the total size of the file or folder (recursive) on disk. This includes the full allocated size including the file system structures that make up this file or folder.
        /// For folders the size is determined recursively.
        /// When querying the size-on-disk of the root folder of a volume, the result should be very close to the occupied disk space.
        /// Returns null if the size cannot be determined.
        /// </summary>
        long? GetSizeOnDisk();

        /// <summary>
        /// Deletes the file or folder.
        /// </summary>
        void Delete(DeleteMode mode);
    }


    //[AOSInterface(Names.AOS_FOLDER)]
    public interface IFolder : IFileSystemObject
    {
        /// <summary>
        /// Returns the list of files and folders that are direct children of this folder.
        /// The caller can check for each item, which interface it implements to distinguish between files and folders.
        /// The list is not required to be in any particular order.
        /// </summary>
        IEnumerable<IFileSystemObject> GetChildren();

        /// <summary>
        /// Returns the file or folder with the specified name.
        /// </summary>
        IFileSystemObject GetChild(string name, bool file, OpenMode mode);

        /// <summary>
        /// Indicates whether this folder has a direct child with the specified name.
        /// </summary>
        bool ChildExists(string name, bool file);
    }


    //[AOSInterface(Names.AOS_FILE)]
    public interface IFile : IObjectRef, IFileSystemObject, ICustomAppearance
    {
        /// <summary>
        /// Reads data from the file.
        /// The method shall fail if the requested range is out of bounds.
        /// </summary>
        void Read(long offset, long count, byte[] buffer, long bufferOffset);

        /// <summary>
        /// Writes data to the file.
        /// The method shall fail if the requested range is out of bounds.
        /// </summary>
        void Write(long offset, long count, byte[] buffer, long bufferOffset);

        /// <summary>
        /// Changes the size of the file.
        /// If the new size is larger than the current size, the slack space should be initialized to 0.
        /// </summary>
        void ChangeSize(long newSize);
    }
*/
}
