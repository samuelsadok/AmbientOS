using EnvDTE;
using Microsoft.VisualStudio;
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
    static class ExtensionMethods
    {
        public static IEnumerable<Project> AllProjects(this Solution solution)
        {
            var queue = new Queue<Project>(solution.Projects.OfType<Project>());

            while (queue.Any()) {
                var project = queue.Dequeue();
                yield return project;

                if (project.ProjectItems != null)
                    foreach (ProjectItem projectItem in project.ProjectItems)
                        if ((projectItem.Kind == "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}" || projectItem.Kind == "{66A26722-8FB5-11D2-AA7E-00C04F688DDE}") && projectItem.SubProject != null)
                            queue.Enqueue(projectItem.SubProject);
            }
        }

        public static ProjectItem GetProjectItem(this ProjectItems items, string name)
        {
            return items.Cast<ProjectItem>().FirstOrDefault(item => string.Compare(item.Name, name, true) == 0);
        }

        private static readonly Guid VsSolutionFolder = new Guid("{66A26720-8FB5-11D2-AA7E-00C04F688DDE}");

        public static IVsHierarchy GetCurrentHierarchy(this IServiceProvider provider)
        {
            return provider.GetSelectedProject()?.ToHierarchy();
        }

        public static Project GetSelectedProject(this IServiceProvider provider)
        {
            var dte = provider.GetService(typeof(DTE)) as DTE;
            if (dte == null)
                throw new InvalidOperationException("DTE not found.");

            SelectedItem selectedItem = dte.SelectedItems.Item(1);
            return selectedItem.Project ?? selectedItem.ProjectItem?.ContainingProject;
        }

        public static bool IsSolutionFolder(this Project item)
        {
            return new Guid(item.Kind).Equals(VsSolutionFolder);
        }

        public static IVsBuildPropertyStorage ToVsBuildPropertyStorage(this Project project)
        {
            return project.ToHierarchy() as IVsBuildPropertyStorage;
        }

        public static IVsHierarchy ToHierarchy(this Project project)
        {
            if (project == null)
                throw new ArgumentNullException($"{project}");

            try {
                IVsHierarchy vsHierarchy;
                if ((Package.GetGlobalService(typeof(SVsSolution)) as IVsSolution2).GetProjectOfUniqueName(project.UniqueName, out vsHierarchy) == 0)
                    return vsHierarchy;
            } catch (NotImplementedException) {
                // ignore if not implemented
            }

            return null;
        }

        public static IVsProject3 ToVsProject(this Project project)
        {
            if (project == null)
                throw new ArgumentNullException($"{project}");

            var result = project.ToHierarchy() as IVsProject3;
            if (result == null)
                throw new ArgumentException("The project is not a VS project.");

            return result;
        }

        public static Project ToDteProject(this IVsHierarchy hierarchy)
        {
            if (hierarchy == null)
                throw new ArgumentNullException($"{hierarchy}");
            
            object result = null;
            if (hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, -2027, out result) == 0)
                return (Project)result;

            return null;
        }

        /*public static Project ToDteProject(this IVsProject project)
        {
            if (project == null)
                throw new ArgumentNullException($"{project}");

            return (project as IVsHierarchy)?.ToDteProject();
        }*/

        public static IEnumerable<T> GetAllInterfaces<T>(this IVsProjectFlavorCfg[] configs)
        {
            foreach (var config in configs.Select((config, i) => new { first = i == 0, config = config })) {
                var guid = typeof(T).GUID;
                IntPtr ppCfg;
                if (config.config.get_CfgType(ref guid, out ppCfg) != VSConstants.S_OK)
                    yield return default(T);
                else if (ppCfg == IntPtr.Zero)
                    yield return default(T);
                else
                    yield return (T)Marshal.GetTypedObjectForIUnknown(ppCfg, typeof(T));
            }
        }

        public static T[] GetAllInterfaces<T>(this IVsProjectFlavorCfg[] configs, out T firstResult)
        {
            var result = configs.GetAllInterfaces<T>();
            firstResult = result.FirstOrDefault();
            return result.ToArray();
        }

        public static bool CanLaunch(this IVsDebuggableProjectCfg config, uint grfLaunch)
        {
            int canLaunch;
            if (config.QueryDebugLaunch(grfLaunch, out canLaunch) != VSConstants.S_OK)
                return false;
            return canLaunch != 0;
        }








        public static object InvokeMethod(this object obj, string name, params Tuple<object, Type>[] args)
        {
            var t = obj.GetType();
            var method = t.GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance, null, args.Select(arg => arg.Item2).ToArray(), null);
            return method.Invoke(obj, args.Select(arg => arg.Item1).ToArray());
        }

        //public static object InvokeMethod(this object obj, string name)
        //{
        //    var method = obj.GetType().GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy, null, new Type[] { }, null);
        //    return method.Invoke(obj, new object[] { });
        //}

        public static object InvokeMethod<T>(this object obj, string name, T arg)
        {
            return obj.InvokeMethod(name, new Tuple<object, Type>(arg, typeof(T)));
        }

        public static object InvokeMethod<T1, T2>(this object obj, string name, T1 arg1, T2 arg2)
        {
            return obj.InvokeMethod(name,
                new Tuple<object, Type>(arg1, typeof(T1)),
                new Tuple<object, Type>(arg2, typeof(T2)));
        }

        public static object GetProperty(this object obj, string name)
        {
            var property = obj.GetType().GetProperty(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance);
            return property.GetValue(obj);
        }

        public static object GetField(this object obj, string name)
        {
            var t = obj.GetType();
            var property = obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance);
            return property.GetValue(obj);
        }
    }
}
