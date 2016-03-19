using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.VisualStudio
{
    class AmbientOSProjectConfig : IVsProjectFlavorCfg
    {
        AmbientOSFlavoredProject project;
        IVsCfg baseProjectConfig;
        IVsProjectFlavorCfg baseFlavorConfig;
        AmbientOSDebugTargetSelection debugTargetSelection = null;

        //EventSinkCollection sink = new EventSinkCollection();
        //List<Tuple<IVsDeployableProjectCfg, uint>> advisedConfigs;

        //IVsProjectFlavorCfg[] innerConfigs;
        //IVsDebuggableProjectCfg[] debuggableConfigs;
        //IVsDeployableProjectCfg[] deployableConfigs;
        //IVsPublishableProjectCfg[] publishableConfigs;

            /*
        private IVsDebuggableProjectCfg ActiveDebuggableConfig
        {
            get
            {
                var config = debuggableConfigs[debugTargetSelection.CurrentDebugTargetSelection];
                if (config == null)
                    config = innerConfigs[debugTargetSelection.CurrentDebugTargetSelection] as IVsDebuggableProjectCfg;
                if (config == null)
                    throw new Exception("The selected target is not debuggable.");
                return config;
            }
        }

        private IVsDeployableProjectCfg ActiveDeployableConfig
        {
            get
            {
                var config = deployableConfigs[debugTargetSelection.CurrentDebugTargetSelection];
                if (config == null)
                    config = innerConfigs[debugTargetSelection.CurrentDebugTargetSelection] as IVsDeployableProjectCfg;
                //if (config == null)
                //    throw new Exception("The selected target is not deployable.");
                return config;
            }
        }

        private IVsPublishableProjectCfg ActivePublishableConfig
        {
            get
            {
                var config = publishableConfigs[debugTargetSelection.CurrentDebugTargetSelection];
                if (config == null)
                    config = innerConfigs[debugTargetSelection.CurrentDebugTargetSelection] as IVsPublishableProjectCfg;
                if (config == null)
                    throw new Exception("The selected target is not publishable.");
                return config;
            }
        }

        public string DisplayName { get; }
        */
        
        /// <param name="innerFlavorConfigs">The first item must be the base flavor config</param>
        public AmbientOSProjectConfig(AmbientOSFlavoredProject project, IVsCfg baseProjectConfig, IVsProjectFlavorCfg baseFlavorConfig)
        {
            this.project = project;
            this.baseProjectConfig = baseProjectConfig;
            this.baseFlavorConfig = baseFlavorConfig;

            /*
            //var innerFlavorConfigs = new IVsProjectFlavorCfg[] { baseFlavorConfig };
            innerConfigs = innerFlavorConfigs;
            debuggableConfigs = innerFlavorConfigs.GetAllInterfaces<IVsDebuggableProjectCfg>().ToArray();
            deployableConfigs = innerFlavorConfigs.GetAllInterfaces<IVsDeployableProjectCfg>().ToArray();
            publishableConfigs = innerFlavorConfigs.GetAllInterfaces<IVsPublishableProjectCfg>().ToArray();

            this.debugTargetSelection = new AmbientOSDebugTargetSelection(innerFlavorConfigs.GetAllInterfaces<IVsProjectCfgDebugTargetSelection>().ToArray());

            string displayName;
            if (this.baseProjectConfig.get_DisplayName(out displayName) == 0)
                DisplayName = displayName.Replace(" ", string.Empty);
            else
                DisplayName = string.Empty;*/
        }

        #region IVsProjectFlavorCfg implementation

        public int get_CfgType(ref Guid iidCfg, out IntPtr ppCfg)
        {
            ppCfg = IntPtr.Zero;
            var result = baseFlavorConfig.get_CfgType(ref iidCfg, out ppCfg);

            //try {
            //    if (iidCfg == typeof(IVsPublishableProjectCfg).GUID && project.IsPublishable) {
            //        ppCfg = Marshal.GetComInterfaceForObject(this, typeof(IVsPublishableProjectCfg));
            //    } else if (iidCfg == typeof(IVsDebuggableProjectCfg).GUID && project.IsDebuggable) {
            //        ppCfg = Marshal.GetComInterfaceForObject(this, typeof(IVsDebuggableProjectCfg));
            //    } else if (iidCfg == typeof(IVsDeployableProjectCfg).GUID && project.IsDeployable) {
            //        ppCfg = Marshal.GetComInterfaceForObject(this, typeof(IVsDeployableProjectCfg));
            //    } else if (iidCfg == typeof(IVsProjectCfgDebugTargetSelection).GUID && project.IsDebuggable) {
            //        ppCfg = Marshal.GetComInterfaceForObject(debugTargetSelection, typeof(IVsProjectCfgDebugTargetSelection));
            //    } else if (baseFlavorConfig != null) {
            //    }
            //} catch (InvalidCastException) {
            //}

            // wrap DebugTargetSelection
            if (iidCfg == typeof(IVsProjectCfgDebugTargetSelection).GUID && project.IsDebuggable) {
                if (debugTargetSelection == null) {
                    IVsProjectCfgDebugTargetSelection baseDebugTargetSelection = null;
                    if (result == VSConstants.S_OK)
                        baseDebugTargetSelection = (IVsProjectCfgDebugTargetSelection)Marshal.GetTypedObjectForIUnknown(ppCfg, typeof(IVsProjectCfgDebugTargetSelection));

                    debugTargetSelection = new AmbientOSDebugTargetSelection(baseDebugTargetSelection);
                }

                ppCfg = Marshal.GetComInterfaceForObject(debugTargetSelection, typeof(IVsProjectCfgDebugTargetSelection));
                return VSConstants.S_OK;

                //    ppCfg = Marshal.GetComInterfaceForObject(debugTargetSelection, typeof(IVsProjectCfgDebugTargetSelection));
            }

            // if you want to wrap some more functionality, do it here

            return result;
        }

        public int Close()
        {
            if (baseFlavorConfig != null) {
                baseFlavorConfig.Close();
                baseFlavorConfig = null;
            }

            if (debugTargetSelection != null)
                debugTargetSelection.Close();

            return VSConstants.S_OK;
        }

        #endregion

        #region IVsProjectCfg implementation
        /*
        public int EnumOutputs(out IVsEnumOutputs ppIVsEnumOutputs)
        {
            throw new NotImplementedException();
        }

        public int OpenOutput(string szOutputCanonicalName, out IVsOutput ppIVsOutput)
        {
            throw new NotImplementedException();
        }

        public int DebugLaunch(uint grfLaunch)
        {
            var config = ActiveDebuggableConfig;
            //if (config.GetType().FullName.StartsWith("Xamarin")) // this was used for debugging the debugging
            //    return project.FakeDebugLaunch((__VSDBGLAUNCHFLAGS)grfLaunch);
            return config.DebugLaunch(grfLaunch);
        }

        public int QueryDebugLaunch(uint grfLaunch, out int pfCanLaunch)
        {
            //pfCanLaunch = project.IsDebuggable && debuggableConfigs.Any(config => config.CanLaunch(grfLaunch)) ? 1 : 0;
            //return VSConstants.S_OK;
            return ActiveDebuggableConfig.QueryDebugLaunch(grfLaunch, out pfCanLaunch);
        }

        public int get_BuildableProjectCfg(out IVsBuildableProjectCfg ppIVsBuildableProjectCfg)
        {
            throw new NotImplementedException();
        }

        public int get_CanonicalName(out string pbstrCanonicalName)
        {
            throw new NotImplementedException();
        }

        public int get_IsSpecifyingOutputSupported(out int pfIsSpecifyingOutputSupported)
        {
            throw new NotImplementedException();
        }

        public int get_Platform(out Guid pguidPlatform)
        {
            throw new NotImplementedException();
        }

        public int get_ProjectCfgProvider(out IVsProjectCfgProvider ppIVsProjectCfgProvider)
        {
            throw new NotImplementedException();
        }

        public int get_RootURL(out string pbstrRootURL)
        {
            throw new NotImplementedException();
        }

        public int get_TargetCodePage(out uint puiTargetCodePage)
        {
            throw new NotImplementedException();
        }

        public int get_UpdateSequenceNumber(ULARGE_INTEGER[] puliUSN)
        {
            throw new NotImplementedException();
        }

        public int get_IsPackaged(out int pfIsPackaged)
        {
            throw new NotImplementedException();
        }
        */
        #endregion

        #region IVsDeployableProjectCfg implementation
            /*
        public int Commit(uint dwReserved)
        {
            return ActiveDeployableConfig?.Commit(dwReserved) ?? VSConstants.E_NOTIMPL;
        }

        public int Rollback(uint dwReserved)
        {
            return ActiveDeployableConfig?.Rollback(dwReserved) ?? VSConstants.E_NOTIMPL;
        }

        public int AdviseDeployStatusCallback(IVsDeployStatusCallback pIVsDeployStatusCallback, out uint pdwCookie)
        {
            advisedConfigs = new List<Tuple<IVsDeployableProjectCfg, uint>>(deployableConfigs.Count());

            uint cookie;
            foreach (var config in deployableConfigs)
                if (config != null)
                    if (config.AdviseDeployStatusCallback(pIVsDeployStatusCallback, out cookie) == VSConstants.S_OK)
                        advisedConfigs.Add(new Tuple<IVsDeployableProjectCfg, uint>(config, cookie));

            pdwCookie = sink.Add(pIVsDeployStatusCallback);
            return VSConstants.S_OK;
        }

        public int UnadviseDeployStatusCallback(uint dwCookie)
        {
            foreach (var config in advisedConfigs)
                config.Item1.UnadviseDeployStatusCallback(config.Item2);

            sink.RemoveAt(dwCookie);
            return VSConstants.S_OK;
        }

        public int QueryStartDeploy(uint dwOptions, int[] pfSupported = null, int[] pfReady = null)
        {
            var result = ActiveDeployableConfig?.QueryStartDeploy(dwOptions, pfSupported, pfReady);

            if (result != null) {
                if (pfSupported?.Count() > 0)
                    pfSupported[0] = 1;
                return result.Value;
            }

            if (pfSupported?.Count() > 0)
                pfSupported[0] = 0;
            if (pfReady?.Count() > 0)
                pfReady[0] = 0;
            return VSConstants.S_OK;
        }

        public int QueryStatusDeploy(out int pfDeployDone)
        {
            pfDeployDone = 0;
            return ActiveDeployableConfig?.QueryStatusDeploy(out pfDeployDone) ?? VSConstants.S_OK;
        }

        public int StartDeploy(IVsOutputWindowPane pIVsOutputWindowPane, uint dwOptions)
        {
            return ActiveDeployableConfig?.StartDeploy(pIVsOutputWindowPane, dwOptions) ?? VSConstants.E_NOTIMPL;
        }

        public int WaitDeploy(uint dwMilliseconds, int fTickWhenMessageQNotEmpty)
        {
            return ActiveDeployableConfig?.WaitDeploy(dwMilliseconds, fTickWhenMessageQNotEmpty) ?? VSConstants.E_NOTIMPL;
        }

        public int StopDeploy(int fSync)
        {
            return ActiveDeployableConfig?.StopDeploy(fSync) ?? VSConstants.E_NOTIMPL;
        }
        */
        #endregion
    }
}
