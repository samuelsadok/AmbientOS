using System;
using Foundation;
using UIKit;
using AppInstall.Framework;

namespace AppInstall.OS
{
    public class iOSApplication
    {
        // This is the main entry point of the application.
        static void Main(string[] args)
        {
            // if you want to use a different Application Delegate class from "AppDelegate"
            // you can specify it here.
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture("de-DE"); // todo: remove workaround (xamarin bug 17690)
            UIApplication.Main(args, null, "AppDelegate");
        }
    }


    // The UIApplicationDelegate for the application. This class is responsible for launching the 
    // User Interface of the application, as well as listening (and optionally responding) to 
    // application events from iOS.
    [Register("AppDelegate")]
    public partial class AppDelegate : UIApplicationDelegate
    {
        //
        // This method is invoked when the application has loaded and is ready to run. In this 
        // method you should instantiate the window, load the UI into it and then make the window
        // visible.
        //
        // You have 17 seconds to return from this method, or iOS will terminate your application.
        //
        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            // start the program
            try {
                ApplicationControl.Start(null); // todo: extract arguments from options
                return true;
            } catch (Exception ex) {
                Platform.DefaultLog.Log("critical error while launching application: " + ex);
                return false;
            }
        }

        //public override UIWindow Window { get { return AppInstall.UI.Window.ApplicationWindow; } set { AppInstall.UI.Window.ApplicationWindow = value; } }

    }
}