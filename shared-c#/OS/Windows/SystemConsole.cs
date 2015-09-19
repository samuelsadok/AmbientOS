using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.UI;
using AppInstall.Framework;

namespace AppInstall.OS
{
    public class SystemConsole : IConsole
    {
        private static SystemConsole console = new SystemConsole();
        public static SystemConsole Console { get { return console; } }


        public static System.ConsoleColor ToSystemColor(AppInstall.Framework.ConsoleColor color)
        {
            switch (color) {
                case AppInstall.Framework.ConsoleColor.Red : return System.ConsoleColor.Red;
                case AppInstall.Framework.ConsoleColor.Yellow : return System.ConsoleColor.Yellow;
                case AppInstall.Framework.ConsoleColor.Green: return System.ConsoleColor.Green;
                case AppInstall.Framework.ConsoleColor.White: return System.ConsoleColor.White;
                case AppInstall.Framework.ConsoleColor.Gray: return System.ConsoleColor.Gray;
                case AppInstall.Framework.ConsoleColor.DarkGray: return System.ConsoleColor.DarkGray;
                case AppInstall.Framework.ConsoleColor.Black: return System.ConsoleColor.Black;
                default : throw new Exception("invalid console color");
            }
        }

        public void SetColor(AppInstall.Framework.ConsoleColor textColor, AppInstall.Framework.ConsoleColor backgroundColor)
        {
            System.Console.ForegroundColor = ToSystemColor(textColor);
            System.Console.BackgroundColor = ToSystemColor(backgroundColor);
        }

        public void WriteLine(string text)
        {
            System.Console.WriteLine(text);
        }

        public void WriteLine(string text, AppInstall.Framework.ConsoleColor color)
        {
            var oldColor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ToSystemColor(color);
            WriteLine(text);
            System.Console.ForegroundColor = oldColor;
        }

        public void WaitForInput()
        {
            System.Console.ReadKey(true);
        }
    }
}
