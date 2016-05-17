﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmbientOS;
using AmbientOS.Environment;
using AmbientOS.FileSystem;
using AmbientOS.UI;
using AmbientOS.Utils;
using static AmbientOS.LogContext;

namespace AmbientOS
{
    [AOSMainApplication()]
    public class Tester
    {
        static void Main(string[] args)
        {
            AmbientOS.Platform.Platform.Initialize(args);


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

            // your code goes here

            var answer = Context.CurrentContext.Shell.PresentDialog(new Text() {
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

            Log("selected option: " + answer);

            AmbientOS.Platform.Platform.Exit();
        }
    }
}
