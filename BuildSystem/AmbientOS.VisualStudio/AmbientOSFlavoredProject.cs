
//#define USE_INTERFACE_DEFINITIONS_VIRTUAL_FOLDER // this has still problems

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.OLE.Interop;
using EnvDTE;

namespace AmbientOS.VisualStudio
{

    class AmbientOSFlavoredProject : FlavoredProjectBase, IVsProject, IVsProject2, IVsProject3, IVsProjectFlavorCfgProvider
    {
        AmbientOSVSPackage package;
        AmbientOSProjectType type;

        DTE dte;
        IVsProjectFlavorCfgProvider baseFlavorConfigProvider;
        IVsProject3 innerVsProject3;
        Dictionary<uint, IVsHierarchyEvents> eventListeners = new Dictionary<uint, IVsHierarchyEvents>();
        Dictionary<uint, List<Tuple<IVsHierarchy, uint>>> eventCookies = new Dictionary<uint, List<Tuple<IVsHierarchy, uint>>>();

        IVsAggregatableProjectCorrected[] innerAggeregatableProjects;
        IVsHierarchy[] innerHierarchies;
        IVsUIHierarchy[] innerUIHierarchies;

        HierarchyItem FirstCustomItem; // head of the list of custom items
        List<uint> hiddenItems = new List<uint>(); // a list of itemIds to hide from the hierarchy

        public Project Project { get { return this.ToDteProject(); } }
        public Icon ProjectNodeIcon { get; private set; }

        public virtual bool IsDebuggable { get { return type == AmbientOSProjectType.Application; } } // todo: override
        public virtual bool IsStartable { get { return type == AmbientOSProjectType.Application; } } // todo: override
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
            var runSessionInfo = project.InvokeMethod("GetRunSessionInfo", new Tuple<object, Type>(device, device.GetType())); // type: MonoTouchRunSessionInfo
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

        public AmbientOSFlavoredProject(AmbientOSVSPackage package, object[] innerProjects, AmbientOSProjectType type)
        {
            this.package = package;
            this.type = type;

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


            //var testParent = new CustomHierarchyItem(VSConstants.VSITEMID_ROOT) { Caption = "Custom Item Root" };
            //
            //HierarchyItem lastItem = testParent;
            //
            //for (int i = 0; i < 80; i++) {
            //    lastItem = lastItem.NextSibling = new CustomHierarchyItem(testParent.ItemId) { Caption = "Custom Item " + i, Icon = i };
            //}
            //
            //testParent.FirstChild = testParent.NextSibling;
            //testParent.NextSibling = null;
            //
            //AddCustomItem(testParent);
        }

        private void AddCustomItem(HierarchyItem item)
        {
            if (FirstCustomItem == null) {
                FirstCustomItem = item;
                return;
            }

            var lastItem = FirstCustomItem;
            while (lastItem.NextSibling != null)
                lastItem = lastItem.NextSibling;
            lastItem.NextSibling = item;
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


            _innerVsAggregatableProject = new AggregatableProjectWrapper(_innerVsAggregatableProject);
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






            List<HierarchyItem> interfaceDefinitionItems = new List<HierarchyItem>();

            var interfaceDefinitionsFolder = new CustomHierarchyItem(VSConstants.VSITEMID_ROOT) {
                Caption = "Interface Definitions",
                Icon = 8
            };

            HierarchyItem lastItem = interfaceDefinitionsFolder;

            var allChildren = this.GetAllChildren().ToArray();
            foreach (var itemId in allChildren) {
                object property;
                if (GetProperty(itemId, (int)__VSHPROPID4.VSHPROPID_BuildAction, out property) != VSConstants.S_OK)
                    continue;
                if (property as string != ProjectWithInterfaceDefinitions.InterfaceDefinitionItemType)
                    continue;

                hiddenItems.Add(itemId);
                interfaceDefinitionItems.Add(lastItem = lastItem.NextSibling = new HierarchyItem(interfaceDefinitionsFolder.ItemId, new Tuple<IVsUIHierarchy, uint>(this, itemId)));
            }

            interfaceDefinitionsFolder.FirstChild = interfaceDefinitionsFolder.NextSibling;
            interfaceDefinitionsFolder.NextSibling = null;

#if USE_INTERFACE_DEFINITIONS_VIRTUAL_FOLDER
            AddCustomItem(interfaceDefinitionsFolder);
#endif
        }

        protected override int GetNestedHierarchy(uint itemId, ref Guid guidHierarchyNested, out IntPtr hierarchyNested, out uint itemIdNested)
        {
            ThereIsThisItem(itemId);

            var result = base.GetNestedHierarchy(itemId, ref guidHierarchyNested, out hierarchyNested, out itemIdNested);

            if (result != VSConstants.S_OK)
                foreach (var hierarchy in innerHierarchies)
                    if (hierarchy.GetNestedHierarchy(itemId, ref guidHierarchyNested, out hierarchyNested, out itemIdNested) == VSConstants.S_OK)
                        return VSConstants.S_OK;

            return result;
        }

        private HierarchyItem GetCustomItem(uint itemId)
        {
            return FirstCustomItem?.GetItem(itemId);
        }

        private bool IsAtRoot(uint itemId)
        {
            return FirstCustomItem?.GetItem(itemId, false) != null;
        }

        public void InvalidateItem(uint itemid)
        {
            var listeners = eventListeners.Values.ToArray();
            for (int i = 0; i < listeners.Length; i++)
                listeners[i].OnInvalidateItems(itemid);
        }

        public void InvalidateProperty(uint itemid, __VSHPROPID prop)
        {
            var listeners = eventListeners.Values.ToArray();
            for (int i = 0; i < listeners.Length; i++)
                listeners[i].OnPropertyChanged(itemid, (int)prop, 0u);
        }

        private Dictionary<uint, Tuple<string, object, object, object, object, object>> blabla = new Dictionary<uint, Tuple<string, object, object, object, object, object>>();

        private void ThereIsThisItem(uint itemId)
        {
#if __DEBUG__
            object c, o0, o1, o2, o3, o4;
            base.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_Caption, out c);
            base.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_IconImgList, out o0);
            base.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_IconIndex, out o1);
            base.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_OpenFolderIconIndex, out o2);
            base.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_IconHandle, out o3);
            base.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_OpenFolderIconHandle, out o4);
            blabla[itemId] = new Tuple<string, object, object, object, object, object>((string)c, o0, o1, o2, o3, o4);
#endif
        }

#if __DEBUG__
        bool dumpedOnce = false;
#endif

        private void DumpKnownItems()
        {
#if __DEBUG__
            bool successOnly = true;

            if (dumpedOnce)
                return;
            dumpedOnce = true;

            using (var output = new StreamWriter(@"C:\Data\Projects\AmbientOS\BuildSystem\itemdump.txt", false)) {
                output.WriteLine("item dump at " + DateTime.Now);

                foreach (var itemId in blabla.Keys.ToArray()) {
                    Action<int, string> dumpProperty = (propId, propName) => {
                        object property;
                        var result = base.GetProperty(itemId, propId, out property);
                        if (successOnly && result != VSConstants.S_OK)
                            return;
                        if (propName.StartsWith("__VSHPROPID"))
                            propName = propName.Substring("__VSHPROPID".Length);
                        output.WriteLine(propName + "(" + result + "): " + (property?.ToString() ?? "(null)") + " (" + (property?.GetType()?.ToString() ?? "(null)") + ")");
                    };


                    output.WriteLine("===== ITEM " + itemId + " =====");
                    output.WriteLine(">>> __VSHPROPID");
                    foreach (__VSHPROPID propId in Enum.GetValues(typeof(__VSHPROPID)))
                        dumpProperty((int)propId, propId.ToString());
                    output.WriteLine(">>> __VSHPROPID2");
                    foreach (__VSHPROPID2 propId in Enum.GetValues(typeof(__VSHPROPID2)))
                        dumpProperty((int)propId, propId.ToString());
                    output.WriteLine(">>> __VSHPROPID3");
                    foreach (__VSHPROPID3 propId in Enum.GetValues(typeof(__VSHPROPID3)))
                        dumpProperty((int)propId, propId.ToString());
                    output.WriteLine(">>> __VSHPROPID4");
                    foreach (__VSHPROPID4 propId in Enum.GetValues(typeof(__VSHPROPID4)))
                        dumpProperty((int)propId, propId.ToString());
                    output.WriteLine(">>> __VSHPROPID5");
                    foreach (__VSHPROPID5 propId in Enum.GetValues(typeof(__VSHPROPID5)))
                        dumpProperty((int)propId, propId.ToString());
                    output.WriteLine(">>> __VSHPROPID6");
                    foreach (__VSHPROPID6 propId in Enum.GetValues(typeof(__VSHPROPID6)))
                        dumpProperty((int)propId, propId.ToString());
                    output.WriteLine();
                }
            }
#endif
        }


#region FlavoredProject

        protected override int GetProperty(uint itemId, int propId, out object property)
        {
            ThereIsThisItem(itemId);

            DumpKnownItems();

            // insert custom items before anything else
            if (itemId == VSConstants.VSITEMID_ROOT && propId == -2041 && FirstCustomItem != null) {
                property = FirstCustomItem.ItemId;
                return VSConstants.S_OK;
            }

            var customItem = GetCustomItem(itemId);

            if (customItem != null) {

                // let the custom item handle the property query
                var customResult = customItem.GetProperty((__VSHPROPID)propId, out property);

                // if the custom item list is exhausted, pretend we're at the beginning of the root item's children
                if ((__VSHPROPID)propId == __VSHPROPID.VSHPROPID_NextSibling || (__VSHPROPID)propId == __VSHPROPID.VSHPROPID_NextVisibleSibling) {
                    if (ExtensionMethods.ToItemID(property) == VSConstants.VSITEMID.Nil && IsAtRoot(itemId)) {
                        itemId = VSConstants.VSITEMID_ROOT;
                        if ((__VSHPROPID)propId == __VSHPROPID.VSHPROPID_NextSibling)
                            propId = (int)__VSHPROPID.VSHPROPID_FirstChild;
                        else if ((__VSHPROPID)propId == __VSHPROPID.VSHPROPID_NextVisibleSibling)
                            propId = (int)__VSHPROPID.VSHPROPID_FirstVisibleChild;
                    }
                }

                if (itemId != VSConstants.VSITEMID_ROOT)
                    return customResult;
            }

            // forward the call of GetProperty to the wrapped hierarchies

            var result = VSConstants.E_FAIL;
            property = null;

            bool repeatCall;
            do {
                repeatCall = false;

                result = base.GetProperty(itemId, propId, out property);

                if (result != VSConstants.S_OK)
                    foreach (var hierarchy in innerHierarchies)
                        if ((result = hierarchy.GetProperty(itemId, propId, out property)) == VSConstants.S_OK)
                            break;

                if (result != VSConstants.S_OK)
                    break;

#if USE_INTERFACE_DEFINITIONS_VIRTUAL_FOLDER
                // if the property was a hierarchy walk and returned a hidden item, we repeat the call to omit the hidden item
                if ((__VSHPROPID)propId == __VSHPROPID.VSHPROPID_FirstChild ||
                    (__VSHPROPID)propId == __VSHPROPID.VSHPROPID_FirstVisibleChild ||
                    (__VSHPROPID)propId == __VSHPROPID.VSHPROPID_NextSibling ||
                    (__VSHPROPID)propId == __VSHPROPID.VSHPROPID_NextVisibleSibling) {
                    object buildAction;
                    if (GetProperty(itemId, (int)__VSHPROPID4.VSHPROPID_BuildAction, out buildAction) != VSConstants.S_OK)
                        continue;
                    if (buildAction as string != ProjectWithInterfaceDefinitions.InterfaceDefinitionItemType)
                        continue;

                    //if (hiddenItems.Contains((uint)ExtensionMethods.ToItemID(property))) {
                    itemId = (uint)ExtensionMethods.ToItemID(property);
                    if ((__VSHPROPID)propId == __VSHPROPID.VSHPROPID_FirstChild)
                        propId = (int)__VSHPROPID.VSHPROPID_NextSibling;
                    else if ((__VSHPROPID)propId == __VSHPROPID.VSHPROPID_FirstVisibleChild)
                        propId = (int)__VSHPROPID.VSHPROPID_NextVisibleSibling;
                    repeatCall = true;
                    //}
                }
#endif
            } while (repeatCall);

            return result;
        }

        protected override int SetProperty(uint itemId, int propId, object property)
        {
            var customItem = GetCustomItem(itemId);
            if (customItem != null)
                return customItem.SetProperty((__VSHPROPID)propId, property);

            int result = base.SetProperty(itemId, propId, property);

            if (result != VSConstants.S_OK)
                foreach (var hierarchy in innerHierarchies)
                    if (hierarchy.SetProperty(itemId, propId, property) == VSConstants.S_OK)
                        return VSConstants.S_OK;

            return result;
        }

        protected override Guid GetGuidProperty(uint itemId, int propId)
        {
            ThereIsThisItem(itemId);

            var customItem = GetCustomItem(itemId);
            if (customItem != null)
                return customItem.GetGuidProperty((__VSHPROPID)propId);

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
            var customItem = GetCustomItem(itemId);
            if (customItem != null) {
                customItem.SetGuidProperty((__VSHPROPID)propId, ref guid);
                return;
            }

            base.SetGuidProperty(itemId, propId, ref guid);

            foreach (var hierarchy in innerHierarchies)
                hierarchy.SetGuidProperty(itemId, propId, ref guid);
        }

        protected override int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            var customItem = GetCustomItem(itemid);
            if (customItem != null)
                return customItem.QueryStatusCommand(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

            int result = base.QueryStatusCommand(itemid, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

            if (result != VSConstants.S_OK)
                foreach (var hierarchy in innerUIHierarchies)
                    if (hierarchy.QueryStatusCommand(itemid, ref pguidCmdGroup, cCmds, prgCmds, pCmdText) == VSConstants.S_OK)
                        return VSConstants.S_OK;

            return result;
        }

        //public static bool IsRightClick = false;
        public static Guid cmdGuid = Guid.Empty;
        public static uint cmd = 0;

        protected override int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            var customItem = GetCustomItem(itemid);
            if (customItem != null)
                return customItem.ExecCommand(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

            //IsRightClick = (pguidCmdGroup == typeof(VSConstants.VsUIHierarchyWindowCmdIds).GUID && nCmdID == (uint)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_RightClick);
            cmdGuid = pguidCmdGroup;
            cmd = nCmdID;

            int result = base.ExecCommand(itemid, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

            if (result != VSConstants.S_OK)
                foreach (var hierarchy in innerUIHierarchies)
                    if (hierarchy.ExecCommand(itemid, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) == VSConstants.S_OK)
                        return VSConstants.S_OK;

            //cmdGuid = Guid.Empty;
            //cmd = 0;

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

#endregion



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


#region IVsProjectFlavorCfgProvider

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


#region IVsProject, IVsProject2, IVsProject3

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
            var result = innerVsProject3.OpenItem(itemid, ref rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
            return result;
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
            var result = innerVsProject3.GetItemContext(itemid, out ppSP);
            return result;
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
        Application,
        Library
    }
}
