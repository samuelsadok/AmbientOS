//------------------------------------------------------------------------------
// <copyright file="VSPackage1.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;

namespace AmbientOS.VisualStudio
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    //[ProvideAutoLoad(UIContextGuids80.NoSolution)]
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [Guid(Constants.PackageGuidString)]
    /*[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]*/
    [ProvideProjectFactory(typeof(ApplicationFactory), "AmbientOS", "AmbientOS Application Projects (*.csproj);*.csproj", null, null, @"Templates\Projects", LanguageVsTemplate = "CSharp", TemplateGroupIDsVsTemplate = "AmbientOS", TemplateIDsVsTemplate = "Template_A,Template_B")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    //[ProvideMSBuildTargets("IronPythonCompilerTasks", @"$PackageFolder$\IronPython.targets")]
    public sealed class AmbientOSVSPackage : Package, IVsShellPropertyEvents
    {

        // todo: remove this class
        private class ItemFilter : IVsFilterAddProjectItemDlg
        {
            private string[] excludedDirs;

            public ItemFilter(params string[] excludedDirs)
            {
                this.excludedDirs = excludedDirs;
            }

            public int FilterListItemByLocalizedName(ref Guid rguidProjectItemTemplates, string pszLocalizedName, out int pfFilter)
            {
                pfFilter = 0;
                return 0;
            }

            public int FilterListItemByTemplateFile(ref Guid rguidProjectItemTemplates, string pszTemplateFile, out int pfFilter)
            {
                pfFilter = 0;
                return 0;
            }

            public int FilterTreeItemByLocalizedName(ref Guid rguidProjectItemTemplates, string pszLocalizedName, out int pfFilter)
            {
                pfFilter = 0;
                return 0;
            }

            public int FilterTreeItemByTemplateDir(ref Guid rguidProjectItemTemplates, string pszTemplateDir, out int pfFilter)
            {
                if (rguidProjectItemTemplates == Constants.ProjectFactoryGuid && this.excludedDirs.Contains(pszTemplateDir))
                    pfFilter = 1;
                else
                    pfFilter = 0;
                return 0;
            }
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="AmbientOSVSPackage"/> class.
        /// </summary>
        public AmbientOSVSPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        public int OnShellPropertyChange(int propid, object var)
        {
            Console.WriteLine("shell property " + propid + " changed");
            return VSConstants.S_OK;
        }


        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            try {
                base.Initialize();
                uint shellEventsCookie;

                IVsShell shell = GetService(typeof(SVsShell)) as IVsShell;
                shell.AdviseShellPropertyChanges(this, out shellEventsCookie);

                if (!Debugger.IsAttached) {
                    AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(HandleUnobservedAppDomainException);
                    Application.Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(HandleUnobservedDispatcherException);
                    TaskScheduler.UnobservedTaskException += new EventHandler<UnobservedTaskExceptionEventArgs>(HandleUnobservedTaskException);
                }

                // load packages that implement project types that we want to immitate
                GetWrappedProjectFactories(shell);



                //// this filter seems to be no longer neccessary
                //uint num;
                //ErrorHandler.ThrowOnFailure(((IVsRegisterNewDialogFilters)GetService(typeof(SVsRegisterNewDialogFilters))).RegisterAddNewItemDialogFilter(new ItemFilter(new string[] { "CSharp\\General", "CSharp\\Data" }), out num));


                var cancelBuildCommandInterceptor = VSCommandInterceptor.FromEnum(this, VSConstants.VSStd97CmdID.CancelBuild);
                cancelBuildCommandInterceptor.AfterExecute += (o, e) => Console.WriteLine("build was cancelled");

                // todo: setup logging and initialize designer tool window

                RegisterProjectFactory(new ApplicationFactory(this));
                
                //todo: add editor factories, connect to services (see IMacServer as an example)

            } catch (Exception ex) {
                Console.WriteLine("error initializing AmbientOS VS package: " + ex);
                throw;
            }
            AmbientOS.VisualStudio.DebugCommand.Initialize(this);
        }

        /// <summary>
        /// Returns a collection of project factories. These implement the project types that we want to wrap.
        /// </summary>
        private IEnumerable<int> GetWrappedProjectFactories(IVsShell shell)
        {
            // make sure the required packages get loaded
            foreach (var guid in Constants.WrappedPackages) {
                IVsPackage package;
                var guid2 = guid;

                if (shell.LoadPackage(ref guid2, out package) != VSConstants.S_OK)
                    continue;

                var projectFactories = package.GetType().GetCustomAttributes<ProvideProjectFactoryAttribute>();

                foreach (var factory in projectFactories) {
                    Console.WriteLine("factory: " + factory.FactoryType);
                    Console.WriteLine("guid: " + factory.FactoryType.GUID);
                }
            }
            yield break;
        }
        

        private bool Handle(Exception ex)
        {
            Console.WriteLine("an unhandled exception occurred in the AmbientOS VisualStudio package: " + ex);
            return true;
        }

        private void HandleUnobservedAppDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null) {
                this.Handle(ex);
            }
        }

        private void HandleUnobservedDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = this.Handle(e.Exception);
        }

        private void HandleUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            bool observe = true;
            e.Exception.Handle(delegate (Exception ex) {
                bool flag = this.Handle(ex);
                observe &= flag;
                return flag;
            });
            if (observe) {
                e.SetObserved();
            }
        }
    }
}
