using System;
using System.Linq;
using System.Text;

namespace AmbientOS.Utils
{
    public static class NetUtils
    {
        /// <summary>
        /// Converts a URL string into a normal string.
        /// Invalid strings will raise an exception
        /// </summary>
        public static string UnescapeFromURL(this string str)
        {
            int i = -1;
            str = str.Replace("+", " ");
            while ((i = str.IndexOf('%', i + 1)) >= 0)
                str = str.Insert(i, ((char)Convert.ToInt32(str.Substring(i + 1, 2), 16)).ToString()).Remove(i + 1, 3);
            return str;
        }

        /// <summary>
        /// Converts a arbitrary string into a URL string.
        /// </summary>
        public static string EscapeForURL(this string str)
        {
            const string legalChars = ".-_~";

            var builder = new StringBuilder(str.Count());
            foreach (var c in str) {
                if (char.IsLetterOrDigit(c))
                    builder.Append(c);
                else if (legalChars.Contains(c)) // do two separate checks for efficiency
                    builder.Append(c);
                else
                    builder.Append(string.Format("%{0,2:X2}", (int)c));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns true if the URL starts with the specified path element(s).
        /// This handles different scenarios regarding '/' at the beginning and the end of the path element.
        /// This function is case sensitive.
        /// </summary>
        /// <param name="prefix">The prefix that should be checked. Must not start or end with '/'</param>
        /// <param name="remainder">The remaining part of the URL (following the prefix). May be empty in case of a match (but not null). Set to null in case of a mismatch.</param>
        public static bool URLStartsWith(this string url, string prefix, out string remainder)
        {
            url = url.TrimStart('/');
            if (url.StartsWith(prefix)) {
                remainder = url.Substring(prefix.Length).TrimStart('/');
                return true;
            } else {
                remainder = null;
                return false;
            }
        }
    }
}
