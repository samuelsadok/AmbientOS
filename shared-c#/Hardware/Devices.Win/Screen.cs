using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.Hardware
{
    public class Screen
    {
        private System.Windows.Forms.Screen screen;

        private Screen(System.Windows.Forms.Screen screen)
        {
            this.screen = screen;
        }

        //public Vector4D<float> Bounds { get { return screen.Bounds.ToVector4D(); } }
        //public Vector4D<float> ApplicationSpace { get { return screen.WorkingArea.ToVector4D(); } }

        public static Screen MainScreen { get { return new Screen(System.Windows.Forms.Screen.PrimaryScreen); } }

        public static IEnumerable<Screen> GetScreens()
        {
            return from s in System.Windows.Forms.Screen.AllScreens select new Screen(s);
        }
    }
}
