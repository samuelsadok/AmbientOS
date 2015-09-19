using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using AppInstall.Framework;

namespace AppInstall.Networking
{

    public class NetMessageParsingError : Exception
    {
        public NetMessageParsingError(string message)
            : base("the received message has an invalid format: " + message)
        {
        }
    }

    /// <typeparam name="M">enumeration type for method</typeparam>
    /// <typeparam name="S">enumeration type for status code</typeparam>
    public class NetMessage<M, S>
        where S : struct, IConvertible
        where M : struct, IConvertible
    {

        private readonly string header;
        private Dictionary<string, string> headerFields = new Dictionary<string, string>();

        public string Header { get { return header; } }

        private string HeaderPart1 {
            get {
                int space1 = header.IndexOf(' ');
                if (space1 == -1) return null;
                return header.Substring(0, space1);
            }
        }

        private string HeaderPart2 {
            get {
                int space1 = header.IndexOf(' ');
                if (space1 == -1) return null;
                int space2 = header.IndexOf(' ' , space1 + 1);
                if (space2 == -1) return null;
                return header.Substring(space1 + 1, space2 - space1 - 1);
            }
        }

        private string HeaderPart3 {
            get {
                int space1 = header.IndexOf(' ');
                if (space1 == -1) return null;
                int space2 = header.IndexOf(' ', space1 + 1);
                if (space2 == -1) return null;
                return header.Substring(space2 + 1);
            }
        }

        /// <summary>
        /// Returns the status code. Only valid for a response type message.
        /// </summary>
        public S StatusCode { get { return Utilities.StringToEnum<S>(HeaderPart2); } }

        /// <summary>
        /// Returns the request method. Only valid for a request type message.
        /// </summary>
        public M Method { get { return Utilities.StringToEnum<M>(HeaderPart1); } }

        /// <summary>
        /// If this property returns false, the Method and Status properties can't be accessed.
        /// </summary>
        public bool ValidHeader { get { return HeaderPart1 != null && HeaderPart2 != null; } }

        /// <summary>
        /// Returns the requested resource without the query. Only valid for a request type message.
        /// </summary>
        public string Resource {
            get {
                var queryStart = HeaderPart2.IndexOf('?');
                return queryStart < 0 ? HeaderPart2 : HeaderPart2.Substring(0, queryStart);
            }
        }

        /// <summary>
        /// Returns a dictionary that represents all query variables in this request. Only valid for request type messages.
        /// The query string is only parsed at the first access, so subsequent calls are much faster.
        /// This property is never null, even if the request contains no query.
        /// </summary>
        public Dictionary<string, string> Query
        {
            get
            {
                if (query == null) {
                    var queryStart = HeaderPart2.IndexOf('?');
                    query = NetUtils.ParseQueryString(queryStart < 0 ? "" : HeaderPart2.Substring(queryStart + 1));
                }
                return query;
            }
        }
        private Dictionary<string, string> query = null;


        public string this[string key]
        {
            get { return headerFields[key]; }
            set
            {
                if (value == null) {
                    if (headerFields.ContainsKey(key)) headerFields.Remove(key);
                    return;
                }
                if (key.Contains('\n') || value.Contains('\n')) throw new ArgumentException("a header key or value must not contain a new line character");
                headerFields[key] = value;
            }
        }

        /// <summary>
        /// Looks for the specified field and returns a default value if it doesn't exist
        /// </summary>
        public string GetFieldOrDefault(string key, string defaultValue)
        {
            return headerFields.GetValueOrDefault(key, defaultValue);
        }

        public INetContent Content { get; set; }


        /// <summary>
        /// Writes the packet to the specified stream.
        /// </summary>
        /// <param name="cancellationToken">Warning: this token is not respected by network streams</param>
        public async Task WriteToStream(Stream stream, CancellationToken cancellationToken)
        {
            // let the content adjust the header
            if (Content != null)
                Content.AdjustHeader(headerFields);
            
            // send header
            await stream.Write(header + "\r\n" + string.Join("", from line in headerFields select line.Key + ": " + line.Value + "\r\n") + "\r\n", cancellationToken);

            // send content
            if (Content != null)
                await Content.WriteToStream(stream, cancellationToken);
        }

        /// <summary>
        /// Parses the header lines of the message without the content from a stream and returns the content length. The "Content-Length" attribute is not available after this routine.
        /// </summary>
        /// <param name="cancellationToken">Warning: this token is not respected by network streams</param>
        private async Task ReadHeaderFromStream(Stream stream, CancellationToken cancellationToken)
        {
            // read header until an empty line is received
            string line;
            while (!string.IsNullOrEmpty(line = await stream.ReadLine(cancellationToken))) {
                int delimiterIndex = line.IndexOf(':');
                if (delimiterIndex < 0) throw new NetMessageParsingError("\"" + line + "\" is not a valid header line");
                if (line.Count() < delimiterIndex + 2) throw new NetMessageParsingError("\"" + line + "\" is not a valid header line");
                if (line[delimiterIndex + 1] != ' ') throw new NetMessageParsingError("\"" + line + "\" is not a valid header line");
                this[line.Substring(0, delimiterIndex)] = line.Substring(delimiterIndex + 2, line.Length - delimiterIndex - 2);
            }
        }


        /// <summary>
        /// Parses a message from the stream and returns the message and the content length.
        /// </summary>
        public static async Task<Tuple<NetMessage<M, S>, T>> ReadFromStream<T>(Stream stream, CancellationToken cancellationToken) where T : INetContent, new()
        {
            var result = new NetMessage<M, S>(await stream.ReadLine(cancellationToken));
            await result.ReadHeaderFromStream(stream, cancellationToken);

            T content = new T();
            await content.ReadFromStream(stream, result.headerFields, cancellationToken);
            result.Content = content;

            return new Tuple<NetMessage<M, S>, T>(result, content);
        }




        /// <summary>
        /// Constructs an empty message with the specified header line.
        /// </summary>
        private NetMessage(string header)
        {
            this.header = header;
        }

        /// <summary>
        /// Constructs an empty request message with the specified header line.
        /// </summary>
        /// <param name="resource">the escaped resource URL with or without the query string</param>
        public NetMessage(string protocol, M method, string resource)
            : this(Utilities.EnumToString(method) + " " + resource + " " + protocol)
        {
        }

        /// <summary>
        /// Constructs an empty request message with the specified header line.
        /// </summary>
        /// <param name="resource">the escaped resource URL without the query string</param>
        /// <param name="query">All query variables. Can be null or empty. All keys and values will be escaped properly.</param>
        public NetMessage(string protocol, M method, string resource, Dictionary<string, string> query)
            : this(protocol, method,
            resource + (query.IsNullOrEmpty() ? "" : "?" + string.Join("&", (from q in query select q.Key.EscapeForURL() + "=" + q.Value.EscapeForURL()))))
        {
        }

        /// <summary>
        /// Constructs an empty response message with the specified header line.
        /// </summary>
        public NetMessage(string protocol, S statusCode)
            : this(protocol + " " + Utilities.EnumToInt(statusCode) + " " + Utilities.EnumToString(statusCode))
        {
        }
    }
}
