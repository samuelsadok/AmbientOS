using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.UI
{
    public static class Extensions
    {
        /*/// <summary>
        /// Reads an integer number from the console.
        /// todo: accept hexadecimal input
        /// Returns null if the user submits an invalid input.
        /// </summary>
        /// <param name="defaultVal">If not null, this value is returned if the user submits an empty input</param>
        public static long? ReadNumber(this IConsole console, long? defaultValue)
        {
            long result;
            var line = console.ReadLine();
            if (line.Trim() == "" && defaultValue.HasValue)
                return defaultValue;
            if (long.TryParse(line, out result))
                return result;
            return null;
        }*/

        public static void Clear(this IConsole console)
        {
            console.Clear(ConsoleColor.DefaultBackground);
        }

        public static void Write(this IConsole console, string text, ConsoleColor textColor = ConsoleColor.DefaultForeground, ConsoleColor backgroundColor = ConsoleColor.DefaultBackground)
        {
            console.Write(text, textColor, backgroundColor);
        }

        public static void WriteLine(this IConsole console)
        {
            console.Write("\n", ConsoleColor.DefaultForeground, ConsoleColor.DefaultBackground);
        }

        public static void WriteLine(this IConsole console, string text, ConsoleColor textColor = ConsoleColor.DefaultForeground, ConsoleColor backgroundColor = ConsoleColor.DefaultBackground)
        {
            console.Write(text + "\n", textColor, backgroundColor);
        }

        public static long PresentDialog(this IUI ui, Text message, params Option[] options)
        {
            return ui.PresentDialog(message, options);
        }

        /// <summary>
        /// Transforms a string into a set of lines according to new line characters and console width.
        /// The output lines don't contain \n chars.
        /// </summary>
        public static IEnumerable<string> ToLines(this IConsole console, string message, string indent = "")
        {
            var dims = console.WindowSize.Get();
            var maxLength = Math.Max(dims.X - indent.Length, 0);

            int start = 0, length = 0;

            while (start + length < message.Count()) {
                if ((message[start + length] == '\n') || (length >= maxLength)) {
                    yield return indent + message.Substring(start, length);
                    if (message[start + length] != '\n')
                        length++;
                    start += length;
                    length = 0;
                }

                length++;
            }

            if (length > 0)
                yield return indent + message.Substring(start, length);
        }
    }
}
