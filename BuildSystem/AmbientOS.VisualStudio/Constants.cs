using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.VisualStudio
{
    static class Constants
    {
        public const string PackageGuidString = "25cb5f0d-788f-4290-8406-1528d59fc72e";

        public const string ApplicationFactoryGuidString = "75B93CEE-1B3E-4BD1-9DB6-45105FFA44DA";
        public static readonly Guid ApplicationFactoryGuid = new Guid(ApplicationFactoryGuidString);

        public const string LibraryFactoryGuidString = "699B6161-23FD-4A8C-A77B-F0D760584EB3";
        public static readonly Guid LibraryFactoryGuid = new Guid(LibraryFactoryGuidString);
        

        public static readonly Guid ProjectCmdSetGuid = new Guid("70630D12-9A76-4DA4-8B13-DB1DE447B337");
        //public static readonly Guid DebugTargetCommandGuid = new Guid("5AE99413-6D85-4011-B52C-D776829E3F55");
        //public const uint DebugTargetCommandID = 123u;
        public const string DebugTargetName = "Local Platform";


        public const string InterfaceCSCodeGeneratorGuidString = "2978BBA3-7154-4B21-8C6A-E1F219749577";
        public static readonly Guid InterfaceCSCodeGeneratorGuid = new Guid(InterfaceCSCodeGeneratorGuidString);


        public class VSContextGuids
        {
            public const string VCSProject = "{FAE04EC1-301F-11D3-BF4B-00C04F79EFBC}";
            public const string VCSEditor = "{694DD9B6-B865-4C5B-AD85-86356E9C88DC}";
            public const string VBProject = "{164B10B9-B200-11D0-8C61-00A0C91E29D5}";
            public const string VBEditor = "{E34ACDC0-BAAE-11D0-88BF-00A0C9110049}";
            public const string VJSProject = "{E6FDF8B0-F3D1-11D4-8576-0002A516ECE8}";
            public const string VJSEditor = "{E6FDF88A-F3D1-11D4-8576-0002A516ECE8}";
        }


        public static readonly Guid XamarinPackageGuid = new Guid("2d510815-1c4e-4210-bd82-3d9d2c56c140");
        public static readonly Guid XamarinIOSPackageGuid = new Guid("77875fa9-01e7-4fea-8e77-dfe942355ca1");
        public static readonly Guid XamarinAndroidPackageGuid = new Guid("296e6a4e-2bd5-44b7-a96d-8ee3d9cda2f6");
        public static readonly Guid MonoTouchUnifiedProjectGuid = new Guid("FEACFBD2-3405-455C-9665-78FE426C6842");
        public static readonly Guid MonoDroidProjectGuid = new Guid("EFBA0AD7-5A72-4C68-AF49-83D382785DCF");


        /// <summary>
        /// Holds the GUIDs of all VSPackages that we want to load.
        /// If a package is not found, it is ignored.
        /// </summary>
        public static readonly Guid[] WrappedPackages = new Guid[] {
            XamarinIOSPackageGuid,
            XamarinAndroidPackageGuid
        };

        /// <summary>
        /// Holds the GUIDs of all project types, that we want to immitate.
        /// For each type, ensure that the relevant package is loaded by adding its GUID to WrappedPackages.
        /// If a project factory is not found, it is ignored.
        /// </summary>
        public static readonly Guid[] WrappedProjectTypes = new Guid[] {
            MonoTouchUnifiedProjectGuid,
            MonoDroidProjectGuid
        };

        // An AmbientOS project can have the following
        // platforms: AnyCPU, Android, iPhone, iPhoneSimulator
        // configurations: Debug, Release, Publish
        // This dictionary helps to use the appropriate project flavor for each platform and translate the configuration into configurations that the flavor understands.
        public static Dictionary<string, Tuple<string, string, Dictionary<string, string>>> ConfigutationRedirect = new Dictionary<string, Tuple<string, string, Dictionary<string, string>>>() {
            { "Android", new Tuple<string, string, Dictionary<string, string>>( "AnyCPU", "Xamarin.VisualStudio.Android.MonoAndroidFlavoredProject", new Dictionary<string, string>() {
                { "Debug", "Debug" },
                { "Release", "Release" },
                { "Publish", "Release" }
            } ) },
            { "iPhone", new Tuple<string, string, Dictionary<string, string>>( "iPhone", "Xamarin.VisualStudio.IOS.MonoTouchFlavoredProject", new Dictionary<string, string>() {
                { "Debug", "Debug" },
                { "Release", "Release" },
                { "Publish", "AppStore" }
            } ) },
            { "iPhoneSimulator", new Tuple<string, string, Dictionary<string, string>>( "iPhoneSimulator", "Xamarin.VisualStudio.IOS.MonoTouchFlavoredProject", new Dictionary<string, string>() {
                { "Debug", "Debug" },
                { "Release", "Release" },
                { "Publish", "AppStore" }
            } ) }
        };
    }
}
