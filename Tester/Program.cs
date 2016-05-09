using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmbientOS;
using AmbientOS.Environment;
using AmbientOS.FileSystem;
using AmbientOS.UI;
using AmbientOS.Utils;

namespace AmbientOS
{
    [AOSMainApplication()]
    public class Tester : IApplicationImpl
    {
        static void Main(string[] args)
        {
            Platform.Platform.Init(args);
        }

        public IApplication ApplicationRef { get; }

        public DynamicEndpoint<string> Name { get; } = new DynamicEndpoint<string>("AmbientOS Testing Tool", PropertyAccess.ReadOnly);
        public DynamicEndpoint<string> Description { get; } = new DynamicEndpoint<string>("Facility to test AmbientOS core components and different kinds of services.", PropertyAccess.ReadOnly);

        public Tester()
        {
            ApplicationRef = new ApplicationRef(this);
        }

        public void Run(Context context)
        {
            //ApplicationRegistry.InstallApp<VHDService>();
            //ApplicationRegistry.InstallApp<PartitionService>();
            //ApplicationRegistry.InstallApp<AmbientOS.FileSystem.NTFS.NTFSService>();
            //ApplicationRegistry.InstallApp<WindowsDiskService>();
            //ApplicationRegistry.InstallApp<WindowsVolumeService>();
            //
            //Application.Init(args, "AmbientOS Installer", "Tool for installing AmbientOS", context => {
            //    var log = context.Log;
            //    var file = AmbientOS.FileSystem.Foreign.InteropFileSystem.GetFileFromPath(@"C:\Developer\vhd\test.vhd");
            //
            //
            //    foreach (var disk in WindowsDiskService.EnumerateDisks(log))
            //        log.Debug("found disk: {0}", disk.Name);
            //
            //    foreach (var disk in WindowsVolumeService.EnumerateVolumes(log))
            //        log.Debug("found volume: {0}", disk.Name);
            //
            //    log.Debug("continue");
            //
            //
            //});

            //var answer = 0;
            var answer = context.Shell.PresentDialog(new Text() {
                Summary = "This is a summary",
                Details = "and these are the details"
            }, new Option[] {
            new Option() {
                Text = new Text() {
                    Summary = "This is an option",
                    Details = "And here are some details"
                },
                Level = Level.Easy
            },
            new Option() {
                Text = new Text() {
                    Summary = "Another option",
                    Details = "And more details"
                },
                Level = Level.Recommended
            },
            new Option() {
                Text = new Text() {
                    Summary = "And a third option",
                    Details = "And even more details"
                },
                Level = Level.Escape
            }
            }
            );

            context.Log.Log("selected option: " + answer);

            //context.Controller.Cancel();
        }
    }
}
