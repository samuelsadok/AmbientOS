using Microsoft.VisualStudio.Shell.Flavor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.VisualStudio
{
    class AggregatableProjectWrapper : IVsAggregatableProjectCorrected
    {
        IVsAggregatableProjectCorrected inner;
        public AggregatableProjectWrapper(IVsAggregatableProjectCorrected inner)
        {
            this.inner = inner;
        }

        List<Tuple<Guid, uint, System.Diagnostics.StackTrace>> hits = new List<Tuple<Guid, uint, System.Diagnostics.StackTrace>>();
        string dump = "";

        public int GetAggregateProjectTypeGuids(out string pbstrProjTypeGuids)
        {
            var result = inner.GetAggregateProjectTypeGuids(out pbstrProjTypeGuids);
            //if (pbstrProjTypeGuids != null)
            //    pbstrProjTypeGuids += (pbstrProjTypeGuids == "" ? "" : ";") + string.Join(";", Constants.WrappedProjectTypes.Select(guid => "{" + guid + "}"));

            //hits.Clear();
            hits.Add(new Tuple<Guid, uint, System.Diagnostics.StackTrace>(AmbientOSFlavoredProject.cmdGuid, AmbientOSFlavoredProject.cmd, new System.Diagnostics.StackTrace()));
            //if (!AmbientOSFlavoredProject.IsRightClick)
            dump = string.Join("", hits.Select(trace => trace.ToString() + "\r\n\r\n"));

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
}
