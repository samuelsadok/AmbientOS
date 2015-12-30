using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using AmbientOS.Environment;
using AmbientOS.Utils;

namespace AmbientOS.FileSystem.NTFS
{
    [AOSService(
        "NTFS driver",
        Description = "Mounts the file system contained on an NTFS-formatted volume."
        )]
    public partial class NTFSService
    {

        /// <summary>
        /// Shows information about an NTFS formatted volume.
        /// </summary>
        [AOSAction("mount", "type=00000000-0000-0000-0000-000000000007")] // NTFS (also used for IFS, exFAT, HPFS)
        [AOSAction("mount", "type=00000000-0000-0000-0000-000000000017")] // hidden NTFS (also used for hidden IFS, exFAT, HPFS)
        [AOSAction("mount", "type=00000000-0000-0000-0000-000000000027")] // NTFS rescue partition (may also be FAT32)
        [AOSAction("mount", "type=E3C9E316-0B5C-4DB8-817D-F92DF00215AE")] // Microsoft Reserved Partition
        [AOSAction("mount", "type=EBD0A0A2-B9E5-4433-87C0-68B6B72699C7")] // Basic Data Partition
        [AOSAction("mount", "type=DE94BBA4-06D1-4D40-A16A-BFD50179D6AC")] // Windows Recovery Environment
        public DynamicSet<IFileSystem> Mount(IVolume volume, Context context)
        {
            List<string> issues;
            var vol = new NTFSVolume(volume, "info", context, out issues);

            if (issues.Count == 0)
                context.Log.Log("The VHD image seems to be healthy.", LogType.Success);
            else
                context.Log.Break();

            if (issues.Count > 1)
                context.Log.Log("Multiple issues were found with the VHD image:", LogType.Warning);

            foreach (var issue in issues)
                context.Log.Log(issue, LogType.Warning);

            return new DynamicSet<IFileSystem>(vol.FileSystemRef).Retain();
        }
    }
}

