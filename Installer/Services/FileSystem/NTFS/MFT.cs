using System;
using System.Collections.Generic;
using System.Linq;

namespace AmbientOS.FileSystem.NTFS
{
    /// <summary>
    /// Represents the master file table of an NTFS volume. The MTF is a file by itself.
    /// Normally, a volume has two such files: $MFT and $MFTMirr, though $MFTMirr only contains the first few files.
    /// </summary>
    class MFTFile
    {
        public NTFSFile File { get; }

        public NTFSAttribute Data { get { return File.Data; } }

        private readonly NTFSVolume volume;

        private readonly Dictionary<long, NTFSFile> OpenFiles = new Dictionary<long, NTFSFile>();


        /// <summary>
        /// Loads a master file table directly from volume.
        /// </summary>
        /// <param name="startCluster">The cluster where the MFT is located</param>
        /// <param name="fileReference">The index of this MFT in the MFT (this should be 0 for $MFT and 1 for $MFTMirr)</param>
        public MFTFile(NTFSVolume volume, long startCluster, long fileReference)
        {
            this.volume = volume;

            // to bootstrap the filesystem, we have to load the first cluster(s) of the MFT manually (before the data attribute of the MFT is loaded)
            var mftInitClusterCount = ((4 * volume.bytesPerMFTRecord + volume.bytesPerCluster - 1) / volume.bytesPerCluster);
            var mftInitClusters = new Cluster[mftInitClusterCount];
            for (int i = 0; i < mftInitClusters.Count(); i++) {
                mftInitClusters[i] = new Cluster() {
                    data = new byte[volume.bytesPerCluster],
                    VCN = i,
                    LCN = startCluster + i,
                    dirty = false
                };
                volume.rawVolume.Read(mftInitClusters[i].LCN * volume.bytesPerCluster, volume.bytesPerCluster, mftInitClusters[i].data, 0);
            }

            File = OpenFiles[fileReference] = new NTFSFile(null, volume, mftInitClusters, 0 * volume.bytesPerMFTRecord);

            // give the pre-loaded init clusters back to the MFT
            for (int i = 0; i < mftInitClusters.Count(); i++)
                Data.nonResidentHeader.clusters[i] = mftInitClusters[i];

            Data.SectorsPerBlock = (int)(volume.bytesPerMFTRecord / volume.bytesPerSector);
        }

        /// <summary>
        /// Loads an MFT from a file.
        /// </summary>
        public MFTFile(NTFSFile file)
        {
            this.volume = file.Volume;
            this.File = file;
        }


        /// <summary>
        /// Loads the specified file from this MFT.
        /// For a given file reference, this will always return the same object. (Until it is closed - but when is that? - not implemented yet).
        /// </summary>
        public NTFSFile GetFile(long fileRef, NTFSFile parent)
        {
            var mftIndex = (fileRef & 0x0000FFFFFFFFFFFF);
            var sequenceNumber = (fileRef >> 16) & 0xFFFF;

            NTFSFile result;

            lock (OpenFiles) {
                if (!OpenFiles.TryGetValue(0x0000FFFFFFFFFFFF & fileRef, out result)) {
                    //MFT.Read(mftIndex * bytesPerMFTRecord, bytesPerMFTRecord);
                    var offset = mftIndex * volume.bytesPerMFTRecord;
                    var cluster = offset / volume.bytesPerCluster;
                    var clusterOffset = offset % volume.bytesPerCluster;
                    var clusterCount = (offset + volume.bytesPerMFTRecord + volume.bytesPerCluster - 1) / volume.bytesPerCluster - cluster;

                    var clusters = new Cluster[clusterCount];
                    for (long i = 0; i < clusterCount; i++)
                        clusters[i] = Data.GetCluster(cluster + i, true);

                    OpenFiles[mftIndex] = result = new NTFSFile(parent, volume, clusters, clusterOffset);
                }
            }

            if (sequenceNumber != 0)
                if (result.SequenceNumber != sequenceNumber)
                    throw new Exception(string.Format("unexpected file sequence number (expected {0:X4}, read {1:X4})", sequenceNumber, result.SequenceNumber));

            return result;
        }
    }
}
