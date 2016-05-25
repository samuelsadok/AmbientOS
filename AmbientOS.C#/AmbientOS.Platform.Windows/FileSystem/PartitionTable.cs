using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.FileSystem
{
    [AOSObjectProvider()]
    class WindowsPartitionTable : IPartitionTableImpl
    {
        DynamicSet<IVolume> volumes;

        public WindowsPartitionTable(WindowsDisk disk)
        {
            var volumes = WindowsVolume.EnumerateVolumes()
                .Where(vol => vol.GetExtents()?.Any(extent => extent.Parent.AsImplementation<WindowsDisk>().Number == disk.Number) ?? false)
                .Select(vol => vol.AsReference<IVolume>()).ToArray();
            this.volumes = new DynamicSet<IVolume>(volumes).Retain();
        }

        public DynamicSet<IVolume> GetPartitions()
        {
            return volumes;
        }

        [AOSObjectProvider()]
        public static IPartitionTable Mount([AOSObjectConstraint("Type", "disk:windows")] IDisk disk)
        {
            var diskImpl = disk.AsImplementation<WindowsDisk>();
            if (diskImpl == null)
                throw new ArgumentException("This service can only mount volumes of a native Windows disk");
            return new WindowsPartitionTable(diskImpl).AsReference<IPartitionTable>();
        }
    }
}
