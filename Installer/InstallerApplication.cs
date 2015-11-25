using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;
using AmbientOS;
using AmbientOS.Environment;
using AmbientOS.FileSystem;
using AmbientOS.UI;
using AmbientOS.Utils;

namespace AppInstall
{
    class Program
    {
        static void Main(string[] args)
        {
            ApplicationRegistry.InstallApp<VHDService>();
            ApplicationRegistry.InstallApp<PartitionService>();
            ApplicationRegistry.InstallApp<AmbientOS.FileSystem.NTFS.NTFSService>();
            ApplicationRegistry.InstallApp<WindowsDiskService>();
            ApplicationRegistry.InstallApp<WindowsVolumeService>();

            Application.Init(args, "AmbientOS Installer", "Tool for installing AmbientOS", context => {
                var log = context.Log;
                var path = (args.Count() < 2 ? @"C:\Developer\vhd\test.vhd" : args[1]);
                var file = AmbientOS.FileSystem.Foreign.InteropFileSystem.GetFileFromPath(path);

                //var result = context.UI.PresentDialog(new Text() {
                //    Summary = "This is a summary",
                //    Details = "and these are the details"
                //}, new Option[] {
                //new Option() {
                //    Text = new Text() {
                //        Summary = "This is an option",
                //        Details = "And here are some details"
                //    },
                //    Level = Level.Easy
                //},
                //new Option() {
                //    Text = new Text() {
                //        Summary = "Another option",
                //        Details = "And more details"
                //    },
                //    Level = Level.Recommended
                //},
                //new Option() {
                //    Text = new Text() {
                //        Summary = "And a third option",
                //        Details = "And even more details"
                //    },
                //    Level = Level.Escape
                //}
                //});


                foreach (var disk in WindowsDiskService.EnumerateDisks(log))
                    log.Debug("found disk: {0}", disk.Name);

                foreach (var disk in WindowsVolumeService.EnumerateVolumes(log))
                    log.Debug("found volume: {0}", disk.Name);

                log.Debug("continue");

                new AmbientOS.FileSystem.NTFS.NTFSService().Test(file, context);

                //var vol = file.Mount<IAOSVolume>(shell, log);
                //ObjectStore.Action(vol, "info", shell, log);

                //file.Publish();
                //Kernel.Dump(log);
                //Kernel.Action("mount", file, shell, log);
                //Kernel.Dump(log);
            });
        }
    }
}
