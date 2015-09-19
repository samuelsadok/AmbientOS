using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public static partial class Abstraction // todo: move to AppInstall.Graphics
    {
        public static ConsoleColor ToConsoleColor(this Color color)
        {
            if (color == Color.Red) {
                return ConsoleColor.Red;
            } else if (color == Color.Yellow) {
                return ConsoleColor.Yellow;
            } else if (color == Color.Green) {
                return ConsoleColor.Green;
            } else {
                return ConsoleColor.Gray;
            }
        }
    }

}
