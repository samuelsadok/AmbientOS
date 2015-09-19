using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.Networking
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
            return string.Join("", (from c in str
                                    select ((Char.IsLetterOrDigit(c) || legalChars.Contains(c)) ? c.ToString() : string.Format("%{0,2:X2}", (int)c))));
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

        /// <summary>
        /// Escapes a string for insertion into HTML code
        /// </summary>
        public static string EscapeForHTML(this string str)
        {
            return System.Net.WebUtility.HtmlEncode(str).Replace("\n", "<br>");
        }

        private static Dictionary<string, string> GetMIMEDictionary()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            result["js"] = "application/javascript";
            result["pdf"] = "application/pdf";
            result["zip"] = "application/zip";
            result["gif"] = "image/gif";
            result["jpg"] = "image/jpeg";
            result["png"] = "image/png";
            result["svg"] = "image/svg+xml";
            result["tif"] = "image/tiff";
            result["css"] = "text/css";
            result["csv"] = "text/csv";
            result["htm"] = "text/html";
            result["html"] = "text/html";
            result["txt"] = "text/plain";
            result["xml"] = "text/xml";
            result["mpg"] = "video/mpeg";
            result["mp4"] = "video/mp4";
            result["ogg"] = "video/ogg";
            result["avi"] = "video/avi";
            result["mov"] = "video/quicktime";
            result["webm"] = "video/webm";
            result["mkv"] = "video/x-matroska";
            result["wmv"] = "video/x-ms-wmv";
            return result;
        }
        private static Dictionary<string, string> mimeDictionary = GetMIMEDictionary();

        /// <summary>
        /// Returns the MIME type name for the specified file.
        /// Files without an extension are classified as binary data.
        /// </summary>
        public static string GetMIMEType(string fileName)
        {
            return mimeDictionary.GetValueOrDefault(Path.GetExtension(fileName).TrimStart('.'), "application/octet-stream");
        }

        /// <summary>
        /// Parses a query string of the form var1=123&var2=abc... and returns a dictionary that contains the argument names and values (in unescaped (normalized) form)
        /// </summary>
        public static Dictionary<string, string> ParseQueryString(string str)
        {
            return (from q in str.Split('&') where q != "" select q.Split('='))
                    .ToDictionary((q) => q[0].UnescapeFromURL(), (q) => q[1].UnescapeFromURL());
        }


        public class TableRow<T>
        {
            public string Header { get; private set; }
            public Func<T, string> CellFactory { get; private set; }
            public TableRow(string header, Func<T, string> cellFactory)
            {
                Header = header;
                CellFactory = cellFactory;
            }
        }

        /// <summary>
        /// Makes a table row from a set of cells.
        /// </summary>
        /// <param name="items">The list of items to be displayed</param>
        /// <param name="rows">A set of rows that define the table. Each row has a title and a function that generates the value from data.</param>
        /// <param name="linkFactory">A function that generates the link if the row should be clickable, null otherwise</param>
        public static string HTMLTable<T>(IEnumerable<T> items, IEnumerable<TableRow<T>> rows, Func<T, string> linkFactory)
        {
            return "<table alternateRows border=\"1\" style=\"font-size:80%;\">" +

                // generate header
                "<tr>" + string.Concat(rows.Select((row) => "<th>" + NetUtils.EscapeForHTML(row.Header) + "</th>")) + "</tr>" +
                
                // generate rows
                string.Concat(items.Select((item) =>
                    "<tr" + (linkFactory == null ? "" : " onclick=\"window.location='" + linkFactory(item) + "'\"") + ">" +
                    string.Concat(rows.Select((row) => "<td>" + NetUtils.EscapeForHTML(row.CellFactory(item)) + "</td>")) + // generate cells
                    "</tr>\n")
                    ) +

                "</table>";
        }
    }
}
