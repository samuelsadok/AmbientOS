using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.UI
{
    public class SystemConsole : IConsoleImpl
    {
        static System.ConsoleColor defaultForegroundColor = System.Console.ForegroundColor;
        static System.ConsoleColor defaultBackgroundColor = System.Console.BackgroundColor;

        public IConsole ConsoleRef { get; }

        private static IConsole console = new SystemConsole().ConsoleRef.Retain();
        public static IConsole Console { get { return console; } }

        private readonly object lockRef = new object();

        private static System.ConsoleColor ToSystemColor(ConsoleColor color, bool foreground)
        {
            switch (color) {
                case ConsoleColor.DefaultForeground: return defaultForegroundColor;
                   case ConsoleColor.DefaultBackground: return defaultBackgroundColor;
                case ConsoleColor.Red : return System.ConsoleColor.Red;
                case ConsoleColor.Yellow : return System.ConsoleColor.Yellow;
                case ConsoleColor.Green: return System.ConsoleColor.Green;
                case ConsoleColor.White: return System.ConsoleColor.White;
                case ConsoleColor.Gray: return System.ConsoleColor.Gray;
                case ConsoleColor.DarkGray: return System.ConsoleColor.DarkGray;
                case ConsoleColor.Black: return System.ConsoleColor.Black;
                default : throw new Exception("invalid console color");
            }
        }

        public SystemConsole()
        {
            ConsoleRef = new ConsoleRef(this);
        }

        public void Write(string text, ConsoleColor textColor, ConsoleColor backgroundColor)
        {
            lock (lockRef) {
                System.Console.ForegroundColor = ToSystemColor(textColor, true);
                System.Console.BackgroundColor = ToSystemColor(backgroundColor, false);
                System.Console.Write(text);
                System.Console.ResetColor();
            }
        }

        private static Key Convert(ConsoleKey key)
        {
            switch (key) {
                case ConsoleKey.UpArrow: return Key.ArrowUp;
                case ConsoleKey.DownArrow: return Key.ArrowDown;
                case ConsoleKey.LeftArrow: return Key.ArrowLeft;
                case ConsoleKey.RightArrow: return Key.ArrowRight;
                case ConsoleKey.PageUp: return Key.PageUp;
                case ConsoleKey.PageDown: return Key.PageDown;
                case ConsoleKey.Home: return Key.Home;
                case ConsoleKey.End: return Key.End;
                case ConsoleKey.Escape: return Key.Esc;
                case ConsoleKey.Enter: return Key.Enter;
                case ConsoleKey.Insert: return Key.Insert;
                case ConsoleKey.Tab: return Key.Tab;
                case ConsoleKey.Backspace: return Key.Backspace;
                case ConsoleKey.Spacebar: return Key.Space;
                default: return Key.Unknown;
            }
        }

        private static KeyModifiers Convert(ConsoleModifiers modifiers)
        {
            KeyModifiers result = 0;
            if (modifiers.HasFlag(ConsoleModifiers.Control)) result |= KeyModifiers.Control;
            if (modifiers.HasFlag(ConsoleModifiers.Alt)) result |= KeyModifiers.Alt;
            if (modifiers.HasFlag(ConsoleModifiers.Shift)) result |= KeyModifiers.Shift;
            return result;
        }

        public KeyPress Read()
        {
            var key = System.Console.ReadKey(true);
            return new KeyPress() {
                Key = Convert(key.Key),
                Modifiers = Convert(key.Modifiers),
                Char = key.KeyChar
            };
        }

        public void Clear(ConsoleColor color)
        {
            System.Console.Clear();
        }

        public Vector2D<int> GetDimensions()
        {
            return new Vector2D<int>(System.Console.BufferWidth, System.Console.BufferHeight);
        }

        public Vector2D<int> GetCursorPosition()
        {
            return new Vector2D<int>(System.Console.CursorLeft, System.Console.CursorTop);
        }

        public void SetCursorPosition(Vector2D<int> position, bool visible)
        {
            System.Console.SetCursorPosition(position.X, position.Y);
            System.Console.CursorVisible = visible;
        }
    }
}
