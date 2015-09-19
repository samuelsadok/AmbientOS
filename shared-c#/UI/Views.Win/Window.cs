using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.Hardware;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public class Window : View<System.Windows.Window>
    {
        View view;

        public event Action Closed;

        public Vector2D<float> Location {
            get { return new Vector2D<float>((float)nativeView.Left, (float)nativeView.Top); }
            set { nativeView.Left = value.X; nativeView.Top = value.Y; }
        }
        private void UpdateFrame()
        {
            Size = new Vector2D<float>((float)nativeView.Width, (float)nativeView.Height);
        }


        public Window(Screen screen, ViewController viewController, Color themeColor)
            : base(new System.Windows.Window(), true)
        {
            if (screen == null) throw new ArgumentNullException("screen");
            if (viewController == null) throw new ArgumentNullException("view");
            if (themeColor == null) throw new ArgumentNullException("themeColor");

            this.view = viewController.ConstructView();
            nativeView.Title = viewController.Title;

            //Size = view.GetMinSize(new Vector2D<float>(float.MaxValue, float.MaxValue)); // todo: respect screen size
            //Location = new Vector2D<float>(0, 0);

            nativeView.Content = view.ToNativeView();

            ManualResetEvent closedSignal = new ManualResetEvent(false);
            nativeView.Closed += (o, e) => { closedSignal.Set(); Closed.SafeInvoke(); };
            new Task(() => {
                WaitHandle.WaitAny(new WaitHandle[] { closedSignal, ApplicationControl.ShutdownToken.WaitHandle });
                nativeView.Close();
            });

        }

        public Window(ViewController view)
            : this(view, Application.ThemeColor)
        {
        }

        public Window(ViewController view, Color themeColor)
            : this(AppInstall.Hardware.Screen.MainScreen, view, themeColor)
        {
        }


        /// <summary>
        /// Opens this window, sets input focus to this view and brings it to the front.
        /// </summary>
        public void Show()
        {
            Size = new Vector2D<float>(1000, 700); // todo: read from config file
            UpdateLayout();
            nativeView.Show();
            nativeView.Focus();
        }

        /// <summary>
        /// Opens this window as a dialog, sets input focus to this view and brings it to the front.
        /// </summary>
        public void ShowDialog()
        {
            Size = new Vector2D<float>(500, 500);
            UpdateLayout();
            nativeView.ShowInTaskbar = true;
            nativeView.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            //nativeView.ResizeMode = System.Windows.ResizeMode.CanMinimize;
            //nativeView.WindowStyle = System.Windows.WindowStyle.ToolWindow;
            nativeView.ShowDialog();
            nativeView.Focus();
        }

        /// <summary>
        /// Closes this window
        /// </summary>
        public void Close()
        {
            nativeView.Close();
        }

        public void DumpLayout(LogContext logContext)
        {
            var str = new StringBuilder(256);
            view.DumpLayout(str, "");
            logContext.Log(str.ToString());
        }
    }
}
