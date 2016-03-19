using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Flavor;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using System.Collections;
using VSLangProj;
using Microsoft.VisualStudio.OLE.Interop;
using System.Drawing;
using Microsoft.VisualStudio.Shell;
using System.IO;

namespace AmbientOS.VisualStudio
{


    class ForwardingProject : IVsAggregatableProjectCorrected
    {
        IVsAggregatableProjectCorrected inner;
        public ForwardingProject(IVsAggregatableProjectCorrected inner)
        {
            this.inner = inner;
        }

        public int GetAggregateProjectTypeGuids(out string pbstrProjTypeGuids)
        {
            var result = inner.GetAggregateProjectTypeGuids(out pbstrProjTypeGuids);
            if (pbstrProjTypeGuids != null)
                pbstrProjTypeGuids += (pbstrProjTypeGuids == "" ? "" : ";") + "{" + Constants.MonoTouchUnifiedProjectGuid + "};{" + Constants.MonoDroidProjectGuid + "}";
            return result;
        }

        public int InitializeForOuter(string pszFilename, string pszLocation, string pszName, uint grfCreateFlags, ref Guid iidProject, out IntPtr ppvProject, out int pfCanceled)
        {
            return inner.InitializeForOuter(pszFilename, pszLocation, pszName, grfCreateFlags, ref iidProject, out ppvProject, out pfCanceled);
        }

        public int OnAggregationComplete()
        {
            return inner.OnAggregationComplete();
        }

        public int SetAggregateProjectTypeGuids(string lpstrProjTypeGuids)
        {
            return inner.SetAggregateProjectTypeGuids(lpstrProjTypeGuids);
        }

        public int SetInnerProject(IntPtr punkInnerIUnknown)
        {
            return inner.SetInnerProject(punkInnerIUnknown);
        }
    }



    class AmbientOSFlavoredProject : FlavoredProjectBase, IVsProject, IVsProject2, IVsProject3, IVsProjectFlavorCfgProvider
    {
        AmbientOSVSPackage package;
        DTE dte;
        IVsProjectFlavorCfgProvider baseFlavorConfigProvider;
        IVsProject3 innerVsProject3;
        Dictionary<uint, IVsHierarchyEvents> eventListeners = new Dictionary<uint, IVsHierarchyEvents>();
        Dictionary<uint, List<Tuple<IVsHierarchy, uint>>> eventCookies = new Dictionary<uint, List<Tuple<IVsHierarchy, uint>>>();

        IVsAggregatableProjectCorrected[] innerAggeregatableProjects;
        IVsHierarchy[] innerHierarchies;
        IVsUIHierarchy[] innerUIHierarchies;

        public Project Project { get { return this.ToDteProject(); } }
        public Icon ProjectNodeIcon { get; private set; }

        public virtual bool IsDebuggable { get { return true; } } // todo: override
        public virtual bool IsStartable { get { return true; } } // todo: override
        public virtual bool IsDeployable { get { return true; } } // todo: override
        public virtual bool IsPublishable { get { return true; } } // todo: override


        private string FakeGetSubtypes(Project project)
        {
            IVsHierarchy vsHierarchy = project.ToHierarchy();
            IVsAggregatableProject vsAggregatableProject = vsHierarchy as IVsAggregatableProject;


            var o1 = Marshal.GetIUnknownForObject(_innerVsAggregatableProject);
            var o2 = Marshal.GetIUnknownForObject(vsAggregatableProject);
            var o3 = Marshal.GetIUnknownForObject(this);

            string guidList;
            if (vsAggregatableProject != null && vsAggregatableProject.GetAggregateProjectTypeGuids(out guidList) == 0)
                return guidList;
            return null;
        }

        private void FakeCreateDebuggerStartInfo(IVsAggregatableProjectCorrected project, object device)
        {
            var runSessionInfo = project.InvokeMethod("GetRunSessionInfo" , new Tuple<object, Type>(device, device.GetType())); // type: MonoTouchRunSessionInfo
            if (runSessionInfo == null)
                return;


            var vsProject = (EnvDTE.Project)project.GetProperty("Project");
            var appProject = (Project)project.InvokeMethod("GetAppProject", new Tuple<object, Type>(vsProject, typeof(Project)));

            var guids = FakeGetSubtypes(appProject);

            var appBundleDir = (string)runSessionInfo.GetProperty("AppBundleDir");
            string directoryName = Path.GetDirectoryName(appBundleDir);

            var projectProperties = project.GetProperty("ProjectProperties"); // type: IMonoTouchProjectProperties
            var deviceSpecificBuilds = projectProperties.GetProperty("DeviceSpecificBuilds"); // type: IMonoTouchDeviceSpecificBuildsProjectProperties
            var isActive = deviceSpecificBuilds.GetProperty("IsActive");
            if ((bool)isActive)
                directoryName = Path.GetDirectoryName(directoryName);
            List<string> list = new List<string>();
            list.Add(Path.Combine(Path.GetDirectoryName(appProject.FullName), directoryName));


            //if (vsProject.IsWatchApp())
            //    list.Add(Path.Combine(Path.GetDirectoryName(base.Project.FullName), Helpers.GetOutputPath(base.Project)));

            return;
        }

        public int FakeDebugLaunch(__VSDBGLAUNCHFLAGS grfLaunch)
        {
            var xamarinAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Xamarin.VisualStudio.IOS");
            var xamarinProjectType = xamarinAssembly.GetType("Xamarin.VisualStudio.IOS.MonoTouchFlavoredProject", false);

            var xamarinProject = innerAggeregatableProjects.FirstOrDefault(); //.Select(p => Convert.ChangeType(p, xamarinPackageType)); //.FirstOrDefault(p => xamarinPackageType.IsAssignableFrom(p.GetType())); // type: MonoTouchFlavoredProject

            //var x2 = xamarinProject as Xamarin.VisualStudio.IOS.MonoTouchFlavoredProject;
            

            var macServer = xamarinProject.GetField("macServer");
            var isConnected = macServer.GetProperty("IsConnected");
            if (!(bool)isConnected)
                return VSConstants.E_FAIL;

            var packageService = xamarinAssembly.GetType("Xamarin.VisualStudio.IOS.PackageService", false);
            var xamarinIOSPacakge = packageService.GetProperty("Instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Static).GetValue(null, null);
            var outputPane = xamarinIOSPacakge.InvokeMethod("GetOutputPane", VSConstants.OutputWindowPaneGuid.DebugPane_guid, "Debug"); // type: IVsOutputWindowPane

            var progressService = xamarinProject.GetProperty("ProgressService");
            var progress = progressService.InvokeMethod("GetCustomProgress", (IVsOutputWindowPane)outputPane); // type: IProgressReport

            var selectedDevice = xamarinProject.GetProperty("SelectedDevice"); // type: MonoTouchDevice

            //IVsOutputWindowPane outputPane = PackageService.Instance.GetOutputPane(VSConstants.OutputWindowPaneGuid.DebugPane_guid, "Debug");
            //IProgressReport progress = base.ProgressService.GetCustomProgress(outputPane);
            //MonoTouchDevice selectedDevice = this.SelectedDevice;

            if ((grfLaunch == (__VSDBGLAUNCHFLAGS)0 || grfLaunch == __VSDBGLAUNCHFLAGS.DBGLAUNCH_Selected)) {
                progress.InvokeMethod("Start", "fake-launching Application for debugging...");
                var package = xamarinProject.GetField("package");
                IVsDebugger2 debugger = (IVsDebugger2)package.InvokeMethod("GetService", typeof(SVsShellDebugger));
                System.Threading.Tasks.Task.Factory.StartNew(delegate {
                    var packageAssembliesIntermediateDir = xamarinProject.GetProperty("PackageAssembliesIntermediateDir");

                    //FakeCreateDebuggerStartInfo(xamarinProject, selectedDevice);

                    var debugSession = xamarinProject.InvokeMethod("GetDebugSession",
                        new Tuple<object, Type>(selectedDevice, selectedDevice.GetType()),
                        new Tuple<object, Type>(packageAssembliesIntermediateDir, typeof(string)),
                        new Tuple<object, Type>(debugger, typeof(IVsDebugger2)),
                        new Tuple<object, Type>(progress, progress.GetType())); // type: MonoToolsDebuggerSession

                    if (debugSession != null)
                        debugSession.InvokeMethod("Start");
                    progress.InvokeMethod("End", string.Empty);
                });
                return 0;
            }
            return -2147467260;
        }

        public AmbientOSFlavoredProject(AmbientOSVSPackage package, object[] innerProjects, AmbientOSProjectType projectType)
        {
            this.package = package;

            innerAggeregatableProjects = innerProjects.Select(p => p as IVsAggregatableProjectCorrected).Where(p => p != null).ToArray();
            innerHierarchies = innerProjects.Select(p => p as IVsHierarchy).Where(p => p != null).ToArray();
            innerUIHierarchies = innerProjects.Select(p => p as IVsUIHierarchy).Where(p => p != null).ToArray();

            dte = ((System.IServiceProvider)package).GetService(typeof(DTE)) as DTE;

            var itemEvents = dte.Events.GetObject("ProjectItemsEvents") as ProjectItemsEvents;
            itemEvents.ItemRenamed += OnItemRenamed;
            itemEvents.ItemAdded += OnItemAdded;
            itemEvents.ItemRemoved += OnItemRemoved;

            var startupProjectSetInterceptor = VSCommandInterceptor.FromEnum(dte, VSConstants.VSStd97CmdID.SetStartupProject);
            startupProjectSetInterceptor.AfterExecute += OnAfterSetStartupProjectCommandExecuted;
        }

        
        protected override void SetInnerProject(IntPtr innerIUnknown)
        {
            var objectForIUnknown = Marshal.GetObjectForIUnknown(innerIUnknown);

            innerVsProject3 = objectForIUnknown as IVsProject3;
            baseFlavorConfigProvider = objectForIUnknown as IVsProjectFlavorCfgProvider;

            if (serviceProvider == null)
                serviceProvider = package;


            
            foreach (var project in innerAggeregatableProjects)
                project.SetInnerProject(innerIUnknown);

            base.SetInnerProject(innerIUnknown);


            _innerVsAggregatableProject = new ForwardingProject(_innerVsAggregatableProject);
        }

        protected override void InitializeForOuter(string fileName, string location, string name, uint flags, ref Guid guidProject, out bool cancel)
        {
            foreach (var project in innerAggeregatableProjects) {
                IntPtr maybeWeNeedThis;
                int cancelInt;
                project.InitializeForOuter(fileName, location, name, flags, ref guidProject, out maybeWeNeedThis, out cancelInt);
                if (cancel = (cancelInt != 0))
                    return;
            }

            base.InitializeForOuter(fileName, location, name, flags, ref guidProject, out cancel);
        }

        protected override void OnAggregationComplete()
        {
            base.OnAggregationComplete();

            foreach (var project in innerAggeregatableProjects)
                project.OnAggregationComplete();
        }

        protected override int GetProperty(uint itemId, int propId, out object property)
        {
            int result;

            foreach (var hierarchy in innerHierarchies)
                if ((result = hierarchy.GetProperty(itemId, propId, out property)) == VSConstants.S_OK)
                    if (property != null)
                        return VSConstants.S_OK;

            result = base.GetProperty(itemId, propId, out property);
            return result;
        }

        protected override int SetProperty(uint itemId, int propId, object property)
        {
            int result = base.SetProperty(itemId, propId, property);

            foreach (var hierarchy in innerHierarchies)
                if (hierarchy.SetProperty(itemId, propId, property) == VSConstants.S_OK)
                    result = VSConstants.S_OK;

            return result;
        }

        protected override Guid GetGuidProperty(uint itemId, int propId)
        {
            try {
                return base.GetGuidProperty(itemId, propId);
            } catch {
                foreach (var hierarchy in innerHierarchies) {
                    try {
                        Guid guid;
                        if (hierarchy.GetGuidProperty(itemId, propId, out guid) == VSConstants.S_OK)
                            return guid;
                    } catch {
                    }
                }

                throw;
            }
        }

        protected override void SetGuidProperty(uint itemId, int propId, ref Guid guid)
        {
            base.SetGuidProperty(itemId, propId, ref guid);

            foreach (var hierarchy in innerHierarchies)
                hierarchy.SetGuidProperty(itemId, propId, ref guid);
        }

        protected override int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            int result = base.QueryStatusCommand(itemid, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

            foreach (var hierarchy in innerUIHierarchies)
                if (hierarchy.QueryStatusCommand(itemid, ref pguidCmdGroup, cCmds, prgCmds, pCmdText) == VSConstants.S_OK)
                    result = VSConstants.S_OK;

            return result;
        }

        protected override int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            int result = base.ExecCommand(itemid, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            if (result == VSConstants.S_OK) // experimental: return on first success instead of executing multiple times
                return result;

            foreach (var hierarchy in innerUIHierarchies)
                if (hierarchy.ExecCommand(itemid, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) == VSConstants.S_OK)
                    return VSConstants.S_OK;

            return result;
        }

        protected override uint AdviseHierarchyEvents(IVsHierarchyEvents eventSink)
        {
            uint result = base.AdviseHierarchyEvents(eventSink);
            eventListeners.Add(result, eventSink);
            var list = eventCookies[result] = new List<Tuple<IVsHierarchy, uint>>();

            foreach (var hierarchy in innerHierarchies) {
                uint cookie;
                if (hierarchy.AdviseHierarchyEvents(eventSink, out cookie) == VSConstants.S_OK) {
                    list.Add(new Tuple<IVsHierarchy, uint>(hierarchy, cookie));
                    result = VSConstants.S_OK;
                }
            }

            return result;
        }

        protected override void UnadviseHierarchyEvents(uint cookie)
        {
            eventListeners.Remove(cookie);
            try {
                List<Tuple<IVsHierarchy, uint>> list;
                if (eventCookies.TryGetValue(cookie, out list)) {
                    foreach (var hierarchy in list)
                        hierarchy.Item1.UnadviseHierarchyEvents(hierarchy.Item2);
                }

                base.UnadviseHierarchyEvents(cookie);
            } catch {
                throw; // wow much sense
            }
        }

        protected override void Close()
        {
            base.Close();

            // todo: close inner projects (the problem is that they try to close already closed resources)
            //try {
            //    foreach (var hierarchy in innerHierarchies)
            //        hierarchy.Close();
            //} catch {
            //}
        }




        protected void OnItemAdded(ProjectItem projectItem)
        {
            Console.WriteLine("an item was added to the project");
        }

        protected void OnItemRenamed(ProjectItem projectItem, string OldName)
        {
            Console.WriteLine("an item was renamed");
        }

        protected void OnItemRemoved(ProjectItem projectItem)
        {
            Console.WriteLine("an item was removed from the project");
        }

        private void OnAfterSetStartupProjectCommandExecuted(object sender, EventArgs e)
        {
            var startupProjectNames = dte.Solution.SolutionBuild.StartupProjects as ICollection;
            if (startupProjectNames == null)
                return;

            var startupProjects = (from project in dte.Solution.AllProjects()
                                   where startupProjectNames.OfType<string>().Any(name => name == project.UniqueName)
                                   select project).ToArray();

            foreach (var p in startupProjects)
                Console.WriteLine("startup project: " + startupProjects);
        }

        #region Implementation for some interfaces

        public int CreateProjectFlavorCfg(IVsCfg baseProjectConfig, out IVsProjectFlavorCfg ppFlavorCfg)
        {
            ppFlavorCfg = null;

            if (baseProjectConfig == null)
                return VSConstants.E_FAIL;

            string platformName;
            if (baseProjectConfig.get_DisplayName(out platformName) != VSConstants.S_OK)
                return VSConstants.E_FAIL;
            if (string.IsNullOrEmpty(platformName))
                return VSConstants.E_FAIL;
            platformName = platformName.Split('|').Skip(1).FirstOrDefault();

            Tuple<string, string, Dictionary<string, string>> redirect;
            if (Constants.ConfigutationRedirect.TryGetValue(platformName, out redirect)) {
                var wrappedBaseProjectConfig = new VsCfgWrapper(baseProjectConfig, redirect.Item1, redirect.Item3);

                var flavorConfigProvider = innerAggeregatableProjects
                    .Select(innerProject => innerProject as IVsProjectFlavorCfgProvider)
                    .Where(p => p?.GetType()?.FullName == redirect.Item2)
                    .FirstOrDefault();

                if (flavorConfigProvider == null)
                    return VSConstants.E_FAIL; // this happens when the appropriate extension is not installed

                return flavorConfigProvider.CreateProjectFlavorCfg(wrappedBaseProjectConfig, out ppFlavorCfg);
            }

            if (baseFlavorConfigProvider == null)
                return VSConstants.E_FAIL;

            IVsProjectFlavorCfg baseFlavorConfig;
            var result = baseFlavorConfigProvider.CreateProjectFlavorCfg(baseProjectConfig, out baseFlavorConfig);
            if (result != VSConstants.S_OK)
                return result;

            ppFlavorCfg = new AmbientOSProjectConfig(this, baseProjectConfig, baseFlavorConfig);
            return VSConstants.S_OK;



            //List<IVsProjectFlavorCfg> innerFlavorConfigs = new List<IVsProjectFlavorCfg>(innerAggeregatableProjects.Count() + 1);
            //
            //
            //if (baseFlavorConfigProvider == null)
            //    return VSConstants.E_FAIL;
            //
            //IVsProjectFlavorCfg baseFlavorConfig;
            //baseFlavorConfigProvider.CreateProjectFlavorCfg(baseProjectConfig, out baseFlavorConfig);
            //innerFlavorConfigs.Add(baseFlavorConfig); // this must remain the first item
            //
            //foreach (var innerFlavorConfigProvider in innerAggeregatableProjects.Select(innerProject => innerProject as IVsProjectFlavorCfgProvider)) {
            //    if (innerFlavorConfigProvider != null) {
            //        IVsProjectFlavorCfg innerFlavorConfig;
            //        if (innerFlavorConfigProvider.CreateProjectFlavorCfg(baseProjectConfig, out innerFlavorConfig) != VSConstants.S_OK)
            //            continue;
            //        innerFlavorConfigs.Add(innerFlavorConfig);
            //    }
            //}
            //
            //
            //
            //AmbientOSProjectConfig tastyConfig = new AmbientOSProjectConfig(this, baseProjectConfig, baseFlavorConfig, innerFlavorConfigs.ToArray(), null); // todo: get debug target selection
            //ppFlavorCfg = tastyConfig;
            //configurations.Add(tastyConfig);
            //return VSConstants.S_OK;
        }

        #endregion

        #region Implementaion for IVsProject, IVsProject2 and IVsProject3

        public int AddItem(uint itemidLoc, VSADDITEMOPERATION dwAddItemOperation, string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen, IntPtr hwndDlgOwner, VSADDRESULT[] pResult)
        {
            return innerVsProject3.AddItem(itemidLoc, dwAddItemOperation, pszItemName, cFilesToOpen, rgpszFilesToOpen, hwndDlgOwner, pResult);
        }

        public int AddItemWithSpecific(uint itemidLoc, VSADDITEMOPERATION dwAddItemOperation, string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen, IntPtr hwndDlgOwner, uint grfEditorFlags, ref Guid rguidEditorType, string pszPhysicalView, ref Guid rguidLogicalView, VSADDRESULT[] pResult)
        {
            return innerVsProject3.AddItemWithSpecific(itemidLoc, dwAddItemOperation, pszItemName, cFilesToOpen, rgpszFilesToOpen, hwndDlgOwner, grfEditorFlags, ref rguidEditorType, pszPhysicalView, ref rguidLogicalView, pResult);
        }

        public int OpenItem(uint itemid, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame)
        {
            return innerVsProject3.OpenItem(itemid, ref rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
        }

        public int OpenItemWithSpecific(uint itemid, uint grfEditorFlags, ref Guid rguidEditorType, string pszPhysicalView, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame)
        {
            return innerVsProject3.OpenItemWithSpecific(itemid, grfEditorFlags, ref rguidEditorType, pszPhysicalView, ref rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
        }

        public int ReopenItem(uint itemid, ref Guid rguidEditorType, string pszPhysicalView, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame)
        {
            return innerVsProject3.ReopenItem(itemid, ref rguidEditorType, pszPhysicalView, ref rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
        }

        public int TransferItem(string pszMkDocumentOld, string pszMkDocumentNew, IVsWindowFrame punkWindowFrame)
        {
            return innerVsProject3.TransferItem(pszMkDocumentOld, pszMkDocumentNew, punkWindowFrame);
        }

        public int RemoveItem(uint dwReserved, uint itemid, out int pfResult)
        {
            return innerVsProject3.RemoveItem(dwReserved, itemid, out pfResult);
        }

        public int GenerateUniqueItemName(uint itemidLoc, string pszExt, string pszSuggestedRoot, out string pbstrItemName)
        {
            return innerVsProject3.GenerateUniqueItemName(itemidLoc, pszExt, pszSuggestedRoot, out pbstrItemName);
        }

        public int GetItemContext(uint itemid, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider ppSP)
        {
            return innerVsProject3.GetItemContext(itemid, out ppSP);
        }

        public int GetMkDocument(uint itemid, out string pbstrMkDocument)
        {
            return innerVsProject3.GetMkDocument(itemid, out pbstrMkDocument);
        }

        public int IsDocumentInProject(string pszMkDocument, out int pfFound, VSDOCUMENTPRIORITY[] pdwPriority, out uint pitemid)
        {
            return innerVsProject3.IsDocumentInProject(pszMkDocument, out pfFound, pdwPriority, out pitemid);
        }

        #endregion
    }

    enum AmbientOSProjectType
    {
        Application
    }
}
