﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceProcess;

namespace AmbientOS
{
    class MainService : ServiceBase
    {

        /*
        /// <summary>
        /// The main entry point for the application (for both console and service modes)
        /// </summary>
        static void Main(string[] args)
        {
            Environment.CurrentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (ApplicationControl.IsSystemService) {
                var service = new MainService();
                ServiceBase.Run(service);
            } else if (ApplicationControl.NeedsAdmin ? !PlatformUtilities.RestartWithAdminPrivileges(args, SystemConsole.Console) : true) {
                var trimmedArgs = args.Select((arg) => arg.TrimStart('-', '/'));

                //try {
                if (trimmedArgs.Contains("install")) {
                    if (PlatformUtilities.IsOnNetworkDrive()) {
                        SystemConsole.Console.WriteLine("The service executable must not be on a network drive", AmbientOS.Environment.ConsoleColor.Yellow);
                    } else {
                        System.Configuration.Install.ManagedInstallerClass.InstallHelper(new string[] { System.Reflection.Assembly.GetExecutingAssembly().Location });
                    }
                } else if (trimmedArgs.Contains("uninstall")) {
                    System.Configuration.Install.ManagedInstallerClass.InstallHelper(new string[] { "/u", System.Reflection.Assembly.GetExecutingAssembly().Location });
                } else {
                    ApplicationControl.Start(args);
                    new Task(() => {
                        SystemConsole.Console.Pause();
                        ApplicationControl.Shutdown();
                    }).Start();
                    ApplicationControl.ShutdownToken.WaitHandle.WaitOne();
                }
                //} catch (Exception ex) {
                //    SystemConsole.Console.WriteLine(ex.ToString(), AmbientOS.Environment.ConsoleColor.Red);
                //}
            }
        }
        */


        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        public Application Application { get; set; }
        public Context Context { get; set; }

        /// <summary>
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            //this.ServiceName = Application.ApplicationName;
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        public MainService()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Returns the name of the service run by the current process.
        /// </summary>
        private string GetServiceName()
        {
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            using (var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_Service where ProcessId = " + pid))
                foreach (System.Management.ManagementObject queryObj in searcher.Get())
                    return queryObj["Name"].ToString();
            return Application.Name;
        }

        protected override void OnStart(string[] args)
        {
            Application.Name = GetServiceName();
            new Task(() => {
                Application.Run(Context);
            }).Start();
            Context.Controller.OnCancellation(() => {
                Stop();
            });
        }

        protected override void OnPause()
        {
            Application.Controller.Pause();
        }

        protected override void OnContinue()
        {
            Application.Controller.Resume();
        }

        protected override void OnStop()
        {
            Application.Controller.Cancel();
        }
    }


    /// <summary>
    /// Provides the installer for the windows service
    /// </summary>
    [System.ComponentModel.RunInstaller(true)]
    public class WindowsServiceInstaller : System.Configuration.Install.Installer
    {
        public static Application Application { get; set; }

        public WindowsServiceInstaller()
        {
            ServiceProcessInstaller p = new ServiceProcessInstaller();
            ServiceInstaller s = new ServiceInstaller();

            p.Account = ServiceAccount.LocalSystem;
            s.StartType = ServiceStartMode.Automatic;
            s.ServiceName = Application.Name;
            s.Description = Application.Description;

            Installers.Add(p);
            Installers.Add(s);
        }
    }
}
