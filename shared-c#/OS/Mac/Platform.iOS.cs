using System;
using System.Threading;
using System.Threading.Tasks;
using Foundation;
using UIKit;
using AppInstall.UI;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.OS
{

    /// <summary>
    /// Represents platform specific or shared properties, instances, methodes and events
    /// </summary>
    public static class Platform
    {
        /// <summary>
        /// Returns the current platform.
        /// </summary>
        public static PlatformType Type { get { return PlatformType.iOS; } }

        /// <summary>
        /// Returns applications root path
        /// </summary>
        [Obsolete("use platform independent version")]
        public static string ApplicationPath { get { return "."; } }

        /// <summary>
        /// Returns the path of the applications assets directory
        /// </summary>
        [Obsolete("use AppData folder", true)]
        public static string AssetsPath { get { return ApplicationPath + "/Assets"; } }

        /// <summary>
        /// Returns size of the screen (on mobile devices) or the window (on desktop devices) in which the app is displayed.
        /// </summary>
        [Obsolete("which screen?")]
        public static Vector2D<float> ScreenSize { get { return UIScreen.MainScreen.Bounds.Size.ToVector2D(); } }

        /// <summary>
        /// Returns the height of the status bar
        /// </summary>
        [Obsolete("the status bar height is respected by the padding of the window")]
        public static float StatusBarHeight { get { return (UIApplication.SharedApplication.StatusBarHidden ? 0 : JustifyOrientation(UIApplication.SharedApplication.StatusBarOrientation, UIApplication.SharedApplication.StatusBarFrame.ToVector4D()).V4); } }

        /// <summary>
        /// Returns the current system theme color, the device color or a predefined color.
        /// You should normally use Application.ThemeColor instead.
        /// </summary>
        public static Color SystemThemeColor { get { return Color.Blue; } }

        /// <summary>
        /// Executes a routine in the context of the main thread (in GUI apps this is the GUI thread) and blocks until the execution finishes.
        /// This can also be called from the main thread.
        /// </summary>
        public static void InvokeMainThread(Action action)
        {
            ManualResetEvent done = new ManualResetEvent(false);
            if (NSThread.IsMain) {
                //Console.WriteLine("invoke from main");
                action();
            } else {
                //Console.WriteLine("invoke from other thread");
                //NSOperationQueue.MainQueue.AddOperation(delegate { action(); });
                Exception ex = null;
                NSOperationQueue.MainQueue.InvokeOnMainThread(delegate {
                    try {
                        action();
                    } catch (Exception exc) {
                        ex = exc;
                    }
                    done.Set();
                });
                done.WaitOne();
                if (ex != null) throw ex;
            }
            //Console.WriteLine("invoke exit");
        }

        /// <summary>
        /// Evaluates a function in the context of the main thread (in GUI apps this is the GUI thread) and returns the result.
        /// This can also be called from the main thread.
        /// </summary>
        public static T EvaluateOnMainThread<T>(Func<T> function)
        {
            T result = default(T);
            InvokeMainThread(() => result = function());
            return result;
        }

        /// <summary>
        /// Swaps X and Y and Width and Height of a rectangle if the orientation is landscape
        /// </summary>
        [Obsolete("justification is done at the very top of the view hierarchy (also what about the function in PlatformUtilities?)")]
        public static Vector4D<float> JustifyOrientation(UIInterfaceOrientation orientation, Vector4D<float> rectangle)
        {
            if ((orientation == UIInterfaceOrientation.Portrait) || (orientation == UIInterfaceOrientation.PortraitUpsideDown)) return rectangle;
            return new Vector4D<float>(rectangle.V2, rectangle.V1, rectangle.V4, rectangle.V3);
        }


        /// <summary>
        /// Displays a message box on top of the current window.
        /// This function should not be used in a production scenario, as it's very intrusive to the user.
        /// </summary>
        public static void MsgBox(string message, string title)
        {
            ManualResetEvent done = new ManualResetEvent(false);
            UIAlertView v = null;
            try {
                InvokeMainThread(() => {
                    v = new UIAlertView(title, message, null, "OK");
                    v.Show();
                    v.Dismissed += (o, e) => done.Set();
                });
                done.WaitOne();
            } finally {
                if (v != null) v.Dispose();
            }
        }


        /// <summary>
        /// Opens the specified URL in the standard webbrowser
        /// </summary>
        public static void OpenWebPage(string url)
        {
            UIApplication.SharedApplication.OpenUrl(NSUrl.FromString(url));
        }

        public static LogContext DefaultLog { get { return debugLog; } }
        private static LogContext debugLog = new LogContext((c, m, t) => { Console.WriteLine(c + ": " + m); }, "root");




        private static NSRunLoop backgroundRunLoop = null;

        private static NSRunLoop GetBackgroundRunLoop()
        {
            if (backgroundRunLoop == null) {
                var readySignal = new AutoResetEvent(false);
                new Thread(() => {
                    backgroundRunLoop = NSRunLoop.Current;
                    readySignal.Set();
                    NSRunLoop.Current.Run();
                }).Start();
                readySignal.WaitOne();
                new Task(() => {
                    ApplicationControl.ShutdownToken.WaitHandle.WaitOne();
                    backgroundRunLoop.Stop();
                }).Start();
            }
            return backgroundRunLoop;
        }

        [Obsolete("seems not to be working (was used for bluetooth)")]
        public static NSRunLoop BackgroundRunLoop { get { return GetBackgroundRunLoop(); } }
    }
}