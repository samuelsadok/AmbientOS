using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace AmbientOS.FileSystem
{

    // todo: wait for actual language support for contracts (anticipated C# 7)

    /*
    [ContractClassFor(typeof(IDisk))]
    internal abstract class IDiskContract : IDisk
    {
        DiskInfo IDisk.GetInfo()
        {
            Contract.Ensures(Contract.Result<DiskInfo>() != null);
            return default(DiskInfo);
        }

        long IDisk.SetSize(long sectorCount)
        {
            Contract.Requires(sectorCount >= 0);
            return default(long);
        }

        void IDisk.Read(int track, long offset, long count, byte[] buffer, long bufferOffset)
        {
            Contract.Requires(track >= 0);
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(bufferOffset >= 0);
            Contract.Requires(bufferOffset + count <= buffer.Length);
        }

        void IDisk.Write(int track, long offset, long count, byte[] buffer, long bufferOffset)
        {
            Contract.Requires(track >= 0);
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(bufferOffset >= 0);
            Contract.Requires(bufferOffset + count <= buffer.Length);
        }
    }

    [ContractClassFor(typeof(IVolume))]
    internal abstract class IVolumeContract : IVolume
    {
        VolumeInfo IVolume.GetInfo()
        {
            Contract.Ensures(Contract.Result<VolumeInfo>() != null);
            return default(VolumeInfo);
        }

        VolumeExtent[] IVolume.GetExtents()
        {
            Contract.Ensures(Contract.Result<VolumeExtent[]>() != null);
            return default(VolumeExtent[]);
        }

        long IVolume.GetSize()
        {
            Contract.Ensures(Contract.Result<long>() >= 0);
            return default(long);
        }

        long IVolume.SetSize(long size)
        {
            Contract.Requires(size >= 0);
            Contract.Ensures(Contract.Result<long>() >= 0);
            return default(long);
        }

        void IVolume.Read(long offset, long count, byte[] buffer, long bufferOffset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(bufferOffset >= 0);
            Contract.Requires(bufferOffset + count <= buffer.Length);
        }

        void IVolume.Write(long offset, long count, byte[] buffer, long bufferOffset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(bufferOffset >= 0);
            Contract.Requires(bufferOffset + count <= buffer.Length);
        }
    }

    [ContractClassFor(typeof(IFile))]
    internal abstract class IFileContract : IFile
    {
        public abstract void Delete(DeleteMode mode);
        public abstract void SecureDelete(int passes);
        public abstract IFileSystem GetFileSystem();
        public abstract string GetName();
        public abstract string GetPath();
        public abstract long? GetSize();
        public abstract long? GetSizeOnDisk();
        public abstract FileTimes GetTimes();
        public abstract void SetName(string name);
        public abstract void SetTimes(FileTimes times);
        public abstract void AddCustomAppearance(Dictionary<string, string> dict, Type type);

        void IFile.ChangeSize(long newSize)
        {
            Contract.Requires(newSize >= 0);
        }

        void IFile.Read(long offset, long count, byte[] buffer, long bufferOffset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(bufferOffset >= 0);
            Contract.Requires(bufferOffset + count <= buffer.Length);
        }

        void IFile.Write(long offset, long count, byte[] buffer, long bufferOffset)
        {
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(bufferOffset >= 0);
            Contract.Requires(bufferOffset + count <= buffer.Length);
        }
    }
    */
}
