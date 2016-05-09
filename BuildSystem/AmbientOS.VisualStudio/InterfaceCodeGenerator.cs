using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace AmbientOS.VisualStudio
{
    /*
    /// <summary>
    /// Provides a converter that generates C# code from an XML AmbientOS interface description.
    /// </summary>
    [ComVisible(true)]
    [Guid(Constants.InterfaceCSCodeGeneratorGuidString)]
    [ProvideObject(typeof(InterfaceCodeGenerator))]
    [CodeGeneratorRegistration(typeof(InterfaceCodeGenerator), "C# Interface Code Generator", Constants.VSContextGuids.VCSProject, GeneratesDesignTimeSource = true)]
    class InterfaceCodeGenerator : IVsSingleFileGenerator, IObjectWithSite
    {
        private CodeDomProvider codeDomProvider;
        private ServiceProvider serviceProvider;
        private object site;

        private CodeDomProvider CodeProvider
        {
            get
            {
                if (codeDomProvider == null) {
                    IVSMDCodeDomProvider provider = (IVSMDCodeDomProvider)SiteServiceProvider.GetService(typeof(IVSMDCodeDomProvider).GUID);
                    if (provider != null)
                        codeDomProvider = (CodeDomProvider)provider.CodeDomProvider;
                }
                
                return codeDomProvider;
            }
        }

        private ServiceProvider SiteServiceProvider { get { return serviceProvider ?? (serviceProvider = new ServiceProvider(site as IOleServiceProvider)); } }


        #region IVsSingleFileGenerator

        public int DefaultExtension(out string pbstrDefaultExtension)
        {
            pbstrDefaultExtension = "." + CodeProvider.FileExtension;
            return VSConstants.S_OK;
        }

        public int Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace, IntPtr[] rgbOutputFileContents, out uint pcbOutput, IVsGeneratorProgress pGenerateProgress)
        {
            if (bstrInputFileContents == null)
                throw new ArgumentException(bstrInputFileContents);

            // generate our comment string based on the programming language used 
            string comment = string.Empty;
            if (CodeProvider.FileExtension == "cs")
                comment = "// " + "SimpleGenerator invoked on : " + DateTime.Now.ToString();
            if (CodeProvider.FileExtension == "vb")
                comment = "' " + "SimpleGenerator invoked on: " + DateTime.Now.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(comment);

            if (bytes == null) {
                rgbOutputFileContents[0] = IntPtr.Zero;
                pcbOutput = 0;
            } else {
                rgbOutputFileContents[0] = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, rgbOutputFileContents[0], bytes.Length);
                pcbOutput = (uint)bytes.Length;
            }

            return VSConstants.S_OK;
        }

        #endregion


        #region IObjectWithSite

        public void GetSite(ref Guid riid, out IntPtr ppvSite)
        {
            if (site == null)
                Marshal.ThrowExceptionForHR(VSConstants.E_NOINTERFACE);

            // Query for the interface using the site object initially passed to the generator
            IntPtr punk = Marshal.GetIUnknownForObject(site);
            int hr = Marshal.QueryInterface(punk, ref riid, out ppvSite);
            Marshal.Release(punk);
            ErrorHandler.ThrowOnFailure(hr);
        }

        public void SetSite(object pUnkSite)
        {
            // Save away the site object for later use 
            site = pUnkSite;

            // These are initialized on demand via our private CodeProvider and SiteServiceProvider properties 
            codeDomProvider = null;
            serviceProvider = null;
        }

        #endregion 

    }
    */
}
