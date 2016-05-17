using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceProcess;

namespace AmbientOS.Platform
{
    class WindowsService : ServiceBase
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        private TaskController ServiceController { get; }

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

        public WindowsService(TaskController serviceController)
        {
            ServiceController = serviceController;
            InitializeComponent();
        }

        /// <summary>
        /// Returns the name of the service run by the current process.
        /// Returns null if the name cannot be determined or if the current process is not a service.
        /// </summary>
        private static string GetServiceName()
        {
            if (System.Environment.UserInteractive)
                return null;

            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            using (var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_Service where ProcessId = " + pid))
                foreach (System.Management.ManagementObject queryObj in searcher.Get())
                    return queryObj["Name"].ToString();

            return null;
        }

        protected override void OnStart(string[] args)
        {
            ServiceController.OnCancellation(() => {
                Stop();
            });

            ServiceController.Resume();
        }

        protected override void OnPause()
        {
            ServiceController.Pause();
        }

        protected override void OnContinue()
        {
            ServiceController.Resume();
        }

        protected override void OnStop()
        {
            ServiceController.Cancel();
        }
    }
}
