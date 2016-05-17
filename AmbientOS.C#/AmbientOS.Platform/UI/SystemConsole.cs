using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AmbientOS.TaskController;

namespace AmbientOS.UI
{
    public class SystemConsole : IConsoleImpl
    {
        static System.ConsoleColor defaultForegroundColor = System.Console.ForegroundColor;
        static System.ConsoleColor defaultBackgroundColor = System.Console.BackgroundColor;

        public IConsole ConsoleRef { get; }
        public DynamicEndpoint<Vector2D<int>> WindowSize { get; }
        public DynamicEndpoint<Vector2D<int>> CursorPosition { get; }
        public DynamicEndpoint<bool> CursorVisibility { get; }

        private static IConsole console = new SystemConsole().ConsoleRef.Retain();
        public static IConsole Console { get { return console; } }

        private readonly object lockRef = new object();

        private static System.ConsoleColor ToSystemColor(ConsoleColor color)
        {
            switch (color) {
                case ConsoleColor.DefaultForeground: return defaultForegroundColor;
                case ConsoleColor.DefaultBackground: return defaultBackgroundColor;
                case ConsoleColor.Red: return System.ConsoleColor.Red;
                case ConsoleColor.Yellow: return System.ConsoleColor.Yellow;
                case ConsoleColor.Green: return System.ConsoleColor.Green;
                case ConsoleColor.White: return System.ConsoleColor.White;
                case ConsoleColor.Gray: return System.ConsoleColor.Gray;
                case ConsoleColor.DarkGray: return System.ConsoleColor.DarkGray;
                case ConsoleColor.Black: return System.ConsoleColor.Black;
                default: throw new Exception("invalid console color");
            }
        }

        public SystemConsole()
        {
            ConsoleRef = new ConsoleRef(this);

            WindowSize = new DynamicEndpoint<Vector2D<int>>(
                () => new Vector2D<int>(System.Console.WindowWidth, System.Console.WindowHeight),
                val => System.Console.SetWindowSize(val.X, val.Y)
                );

            CursorPosition = new DynamicEndpoint<Vector2D<int>>(
                () => new Vector2D<int>(System.Console.CursorLeft, System.Console.CursorTop),
                val => System.Console.SetCursorPosition(val.X, val.Y)
                );

            CursorVisibility = new DynamicEndpoint<bool>(
                () => System.Console.CursorVisible,
                val => System.Console.CursorVisible = val
                );
        }

        public void Write(string text, ConsoleColor textColor, ConsoleColor backgroundColor)
        {
            lock (lockRef) {
                if (textColor == ConsoleColor.DefaultForeground || backgroundColor == ConsoleColor.DefaultBackground)
                    System.Console.ResetColor();

                if (textColor != ConsoleColor.DefaultForeground)
                    System.Console.ForegroundColor = ToSystemColor(textColor);
                if (backgroundColor != ConsoleColor.DefaultBackground)
                    System.Console.BackgroundColor = ToSystemColor(backgroundColor);

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

            ConsoleKeyInfo key = default(ConsoleKeyInfo);


            var s = new System.Threading.ManualResetEvent(false);
            var t = new System.Threading.Thread(() => {
                key = System.Console.ReadKey(true);
                s.Set();
            });
            t.Start();


            System.Console.Title = "lol this is a title";
            Context.CurrentContext.Controller.OnCancellation(() => {

                // todo: this works like this but it's not great, we need to unsubscribe the handler if readkey succeeds

                t.Abort();
                s.Set();
            });

            s.WaitOne();
            ThrowIfCancellationRequested();

            return new KeyPress() {
                Key = Convert(key.Key),
                Modifiers = Convert(key.Modifiers),
                Char = key.KeyChar
            };
        }

        public void Clear(ConsoleColor color)
        {
            //System.Console.Clear(); // this scrolls down on Unix (which we don't want)

            System.Console.SetCursorPosition(0, 0);
            Write(new string(Enumerable.Repeat(' ', System.Console.WindowWidth * System.Console.WindowHeight).ToArray()), ConsoleColor.DefaultForeground, ConsoleColor.DefaultBackground);
            System.Console.SetCursorPosition(0, 0);
        }

        public void Scroll(int lines)
        {
            System.Console.SetWindowPosition(0, Math.Max(0, System.Console.WindowTop + lines));
        }

        public void CopyArea(Vector2D<int> source, Vector2D<int> destination, Vector2D<int> size)
        {
            System.Console.MoveBufferArea(source.X, source.Y, size.X, size.Y, destination.X, destination.Y);
        }
    }
}
