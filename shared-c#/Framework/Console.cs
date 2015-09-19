using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.Framework
{
    public enum ConsoleColor
    {
        Red,
        Yellow,
        Green,
        White,
        Gray,
        DarkGray,
        Black
    }

    public interface IConsole
    {
        void SetColor(ConsoleColor textColor, ConsoleColor backgroundColor);

        void WriteLine(string text);

        void WriteLine(string text, ConsoleColor color);

        void WaitForInput();
    }
}
