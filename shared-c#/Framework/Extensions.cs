using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppInstall.Framework
{
    public static class Extensions
    {
        /// <summary>
        /// Returns an alternative value in case this one is null.
        /// </summary>
        public static T Or<T>(this T value, T alternativeValue)
            where T : class
        {
            return (value == null ? alternativeValue : value);
        }

        /// <summary>
        /// Thread-safely raises an event that might be null
        /// </summary>
        public static void SafeInvoke(this Action handler)
        {
            if (handler != null) handler();
        }
        /// <summary>
        /// Thread-safely raises an event that might be null
        /// </summary>
        public static void SafeInvoke<T>(this Action<T> handler, T arg)
        {
            if (handler != null) handler(arg);
        }

        /// <summary>
        /// Thread-safely raises an event that might be null
        /// </summary>
        public static void SafeInvoke<T1, T2>(this Action<T1, T2> handler, T1 arg1, T2 arg2)
        {
            if (handler != null) handler(arg1, arg2);
        }

        /// <summary>
        /// Thread-safely raises an event that might be null
        /// </summary>
        public static void SafeInvoke<T>(this EventHandler<T> handler, object o, T e)
        {
            if (handler != null) handler(o, e);
        }

        /// <summary>
        /// Thread-safely raises an event that might be null
        /// </summary>
        public static void SafeInvoke<T>(this EventHandler handler, object o)
        {
            if (handler != null) handler(o, EventArgs.Empty);
        }

        #region "Stream Extensions"

        /// <summary>
        /// Writes the contents of this stream to another stream.
        /// </summary>
        /// <param name="length">set to -1 to transfer the entire input stream</param>
        public static async Task WriteTo(this System.IO.Stream stream, System.IO.Stream target, CancellationToken cancellationToken, long length = -1, long bufferSize = 4194304)
        {
            byte[] buffer = new byte[bufferSize];
            long remaining = (length == -1 ? stream.Length - stream.Position : length);
            int count;

            while (remaining > 0) {
                count = await stream.ReadAsync(buffer, 0, (int)((remaining > bufferSize) ? bufferSize : remaining), cancellationToken);
                await target.WriteAsync(buffer, 0, count, cancellationToken);
                remaining -= count;
                if (count == 0)
                    break; // todo: see if this causes problems
            }
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream. 0 is a valid count.
        /// The returned buffer may be smaller than the requested length, but only if the stream ends before.
        /// </summary>
        public static async Task<byte[]> ReadBytes(this System.IO.Stream stream, int count, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[count];
            int pos = 0;

            while (pos < count) {
                var readBytes = await stream.ReadAsync(buffer, pos, count - pos, cancellationToken);
                pos += readBytes;

                // abort if no more bytes are incoming
                if (readBytes == 0)
                    break;

                cancellationToken.ThrowIfCancellationRequested();
            }

            // prune buffer
            if (pos != buffer.Count())
                Array.Resize(ref buffer, pos);

            return buffer;
        }

        /// <summary>
        /// Returns a buffer that contains all remaining bytes in the stream.
        /// The stream is considered to end when no more bytes are expected.
        /// </summary>
        public static async Task<byte[]> ReadToEnd(this System.IO.Stream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[256];
            int pos = 0;

            while (true) {

                // double buffer length if required
                if (buffer.Count() - pos <= 0)
                    Array.Resize(ref buffer, buffer.Count() * 2);

                var readBytes = await stream.ReadAsync(buffer, pos, buffer.Count() - pos, cancellationToken);
                pos += readBytes;

                // abort if no more bytes are incoming
                if (readBytes == 0)
                    break;

                cancellationToken.ThrowIfCancellationRequested();
            }


            // prune buffer
            if (pos != buffer.Count())
                Array.Resize(ref buffer, pos);

            return buffer;
        }

        /// <summary>
        /// Reads the next line that is terminated by \r\n or \n or by the end of stream
        /// </summary>
        public static async Task<string> ReadLine(this System.IO.Stream stream, CancellationToken cancellationToken)
        {
            StringBuilder s = new StringBuilder(100);
            char c;

            while (true) {
                do {
                    var cArr = Encoding.ASCII.GetChars(await stream.ReadBytes(1, cancellationToken));
                    if (cArr.Count() == 0)
                        cArr = new char[] { '\n' };
                    c = cArr[0];
                } while (c == '\r');

                if (c == '\n') break;
                s.Append(c);
            }

            return s.ToString();
        }


        
        public static async Task Write(this System.IO.Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(buffer, 0, buffer.Count(), cancellationToken);
        }
        public static async Task Write(this System.IO.Stream stream, string buffer, CancellationToken cancellationToken)
        {
            await stream.Write(Encoding.ASCII.GetBytes(buffer), cancellationToken);
        }

        #endregion


        /// <summary>
        /// Recursively unwraps an aggeregate exception that consists of only one exception into the inner exception
        /// </summary>
        public static Exception Condense(this Exception ex)
        {
            var aggEx = ex as AggregateException;
            if (aggEx == null) return ex;
            if (aggEx.InnerExceptions.Count() != 1) return ex;
            return aggEx.InnerExceptions[0];
        }


        /// <summary>
        /// Inserts spaces into a CamelCase string
        /// </summary>
        public static string CamelCaseToNormal(this string str)
        {
            if (str == null) return null;
            var result = new StringBuilder(str.Count());
            bool isUpperCase, wasUpperCase = true;
            foreach (char c in str) {
                if (isUpperCase = char.IsUpper(c))
                    if (!wasUpperCase)
                        result.Append(' ');
                wasUpperCase = isUpperCase;
                result.Append(c);
            }
            return result.ToString();
        }

        /// <summary>
        /// Advances the the position to the first non-whitespace character.
        /// </summary>
        public static void ConsumeWhitespace(this string str, ref int position)
        {
            for (; position < str.Length; position++)
                if (!char.IsWhiteSpace(str, position))
                    break;
        }

        /// <summary>
        /// Checks if the dictionary is null or empty
        /// </summary>
        public static bool IsNullOrEmpty<K, V>(this Dictionary<K, V> dictionary)
        {
            if (dictionary == null) return true;
            return !dictionary.Any();
        }

        /// <summary>
        /// Returns the value for the specified key or a default value if the key was not found.
        /// </summary>
        public static V GetValueOrDefault<K, V>(this Dictionary<K, V> dictionary, K key, V defaultValue)
        {
            V result;
            if (dictionary.TryGetValue(key, out result)) return result;
            return defaultValue;
        }

        /// <summary>
        /// Waits for a started task to complete and returns the result
        /// </summary>
        public static T WaitForResult<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            task.Wait(cancellationToken);
            return task.Result;
        }

        /// <summary>
        /// Waits until either the wait handle is triggered or the cancellation token is asserted.
        /// </summary>
        /// <exception cref="OperationCancelledException">the cancellation token was asserted before the wait handle triggered</exception>
        public static Task WaitAsync(this WaitHandle waitHandle, CancellationToken cancellationToken)
        {
            return Task.Run(() => {
                WaitHandle.WaitAny(new WaitHandle[] { waitHandle, cancellationToken.WaitHandle });
                cancellationToken.ThrowIfCancellationRequested();
            });
        }

        /// <summary>
        /// Waits until the wait handle is triggered while allowing for cancellation.
        /// </summary>
        /// <exception cref=""></exception>
        public static void WaitOne(this WaitHandle waitHandle, CancellationToken cancellationToken)
        {
            WaitHandle.WaitAny(new WaitHandle[] { waitHandle, cancellationToken.WaitHandle });
            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Does nothing with the task. Used to suppress warnings and to make clear that asynchronous running of the task was intended.
        /// </summary>
        public static void Run(this Task task)
        {
        }

        /// <summary>
        /// Adds or removes an item from the list, based on a boolean parameter
        /// </summary>
        public static void SetIncluded<T>(this IList<T> list, T item, bool include)
        {
            var included = list.Contains(item);
            if (!included && include)
                list.Add(item);
            else if (included && !include)
                list.Remove(item);
        }

        /// <summary>
        /// Removes some items from a list based on a predicate
        /// </summary>
        public static void RemoveAll<T>(this IList<T> list, Func<T, bool> predicate)
        {
            var toRemove = list.Where(predicate).ToArray();
            foreach (var item in toRemove)
                list.Remove(item);
        }

        /// <summary>
        /// Adds a collection of items to an existing list
        /// </summary>
        public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
        {
            foreach (var item in items)
                list.Add(item);
        }

        /// <summary>
        /// Returns a DateTime that is not lower or higher than the lower or upper bound.
        /// </summary>
        /// <param name="minVal">The lower bound. Set to null to not specify any lower bound.</param>
        /// <param name="maxVal">The upper bound. Set to null to not specify any upper bound.</param>
        public static DateTime Bound(this DateTime dateTime, DateTime? minVal, DateTime? maxVal)
        {
            if (minVal.HasValue)
                if (dateTime < minVal) return minVal.Value;
            if (maxVal.HasValue)
                if (dateTime > maxVal) return maxVal.Value;
            return dateTime;
        }
    }
}