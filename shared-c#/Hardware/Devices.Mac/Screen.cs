using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UIKit;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class Screen
    {
        private UIScreen screen;

        private Screen(UIScreen screen)
        {
            this.screen = screen;
        }

        public Vector4D<float> Bounds { get { return screen.Bounds.ToVector4D(); } }
        public Vector4D<float> ApplicationSpace { get { return screen.ApplicationFrame.ToVector4D(); } }

        public static Screen MainScreen { get { return new Screen(UIScreen.MainScreen); } }

        public static IEnumerable<Screen> GetScreens()
        {
            return from s in UIScreen.Screens select new Screen(s);
        }
    }
}