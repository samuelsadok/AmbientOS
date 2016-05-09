using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

namespace AmbientOS.VisualStudio
{
    class AmbientOSProjectFactory : FlavoredProjectFactoryBase
    {
        AmbientOSVSPackage package;
        AmbientOSProjectType type;
        object[] innerProjects;

        protected AmbientOSProjectFactory(AmbientOSVSPackage package, AmbientOSProjectType type)
        {
            if (package == null)
                throw new ArgumentNullException($"{package}");
            this.package = package;
            this.type = type;
        }

        protected override object PreCreateForOuter(IntPtr outerProjectIUnknown)
        {
            var solution = ((IServiceProvider)package).GetService(typeof(SVsSolution)) as IVsSolution;
            if (type == AmbientOSProjectType.Application)
                innerProjects = CreateInnerProjects(solution, outerProjectIUnknown).ToArray();
            else
                innerProjects = new object[0];
            return new AmbientOSFlavoredProject(package, innerProjects, type);
        }

        private IEnumerable<object> CreateInnerProjects(IVsSolution solution, IntPtr outerProjectIUnknown)
        {
            foreach (var guid in Constants.WrappedProjectTypes) {
                IVsProjectFactory factory;
                if (solution.GetProjectFactory(0, new Guid[] { guid }, null, out factory) != VSConstants.S_OK)
                    continue;
                if (factory == null)
                    continue;

                var aggeregatableFactory = factory as IVsAggregatableProjectFactoryCorrected;
                if (aggeregatableFactory == null)
                    continue;
                
                object actualProject;
                if (PreCreateForOuter(aggeregatableFactory, outerProjectIUnknown, out actualProject) != VSConstants.S_OK)
                    continue;

                if (actualProject != null)
                    yield return actualProject;
            }
        }



        /// <summary>
        /// This is the disassembled version of the method from Microsoft.VisualStudio.Shell.Flavor.FlavoredProjectFactoryBase
        /// Source Assembly: Microsoft.VisualStudio.Shell.14.0 (14.0.0.0)
        /// The thing is that we need the actual project object, and not the COM pointer.
        /// No copyright infringement intended.
        /// </summary>
        private static int PreCreateForOuter(IVsAggregatableProjectFactoryCorrected factory, IntPtr outerProjectIUnknown, out object actualProject)
        {
            var projectIUnknown = IntPtr.Zero; // this is no longer needed
            actualProject = factory.InvokeMethod("PreCreateForOuter", outerProjectIUnknown); // instead we return this
            IntPtr intPtr = IntPtr.Zero;
            ILocalRegistryCorrected localRegistryCorrected = (ILocalRegistryCorrected)ServiceProvider.GlobalProvider.GetService(typeof(SLocalRegistry));
            if (localRegistryCorrected == null) {
                throw new InvalidOperationException();
            }
            
            Guid gUID = new Guid("1CACE4D9-C378-42BD-87DB-3C5D27334331"); // = typeof(Microsoft.VisualStudio.ProjectAggregator.CProjectAggregatorClass).GUID
            Guid iID_IUnknown = VSConstants.IID_IUnknown;
            uint dwFlags = 1u;
            IntPtr zero = IntPtr.Zero;
            try {
                ErrorHandler.ThrowOnFailure(localRegistryCorrected.CreateInstance(gUID, outerProjectIUnknown, ref iID_IUnknown, dwFlags, out zero));
                if (outerProjectIUnknown != IntPtr.Zero) {
                    intPtr = Marshal.CreateAggregatedObject(outerProjectIUnknown, actualProject);
                } else {
                    intPtr = Marshal.CreateAggregatedObject(zero, actualProject);
                }
                IVsProjectAggregator2 vsProjectAggregator = (IVsProjectAggregator2)Marshal.GetObjectForIUnknown(zero);
                if (vsProjectAggregator != null) {
                    vsProjectAggregator.SetMyProject(intPtr);
                }
                projectIUnknown = zero;
                zero = IntPtr.Zero;
            } finally {
                if (intPtr != IntPtr.Zero) {
                    Marshal.Release(intPtr);
                }
                if (zero != IntPtr.Zero) {
                    Marshal.Release(zero);
                }
            }
            if (projectIUnknown == IntPtr.Zero) {
                return -2147467259;
            }
            return 0;
        }
    }


    [Guid(Constants.ApplicationFactoryGuidString)]
    class AmbientOSApplicationFactory : AmbientOSProjectFactory
    {
        public AmbientOSApplicationFactory(AmbientOSVSPackage package)
            : base(package, AmbientOSProjectType.Application)
        {
        }
    }

    [Guid(Constants.LibraryFactoryGuidString)]
    class AmbientOSLibraryFactory : AmbientOSProjectFactory
    {
        public AmbientOSLibraryFactory(AmbientOSVSPackage package)
            : base(package, AmbientOSProjectType.Library)
        {
        }
    }
}
