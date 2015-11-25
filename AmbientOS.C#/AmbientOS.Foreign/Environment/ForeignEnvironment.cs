using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using AmbientOS.FileSystem;

namespace AmbientOS.Environment
{
    public class ForeignEnvironment : IEnvironmentImpl
    {
        public IEnvironment EnvironmentRef { get; }

        public ForeignEnvironment()
        {
            EnvironmentRef = new EnvironmentRef(this);
        }

        public IFolder GetTempFolder()
        {
            using (var temp = FileSystem.Foreign.InteropFileSystem.GetFolderFromPath(Path.GetTempPath()))
                return temp.GetFolder("AmbientOS", OpenMode.NewOrExisting);
        }
    }
}
