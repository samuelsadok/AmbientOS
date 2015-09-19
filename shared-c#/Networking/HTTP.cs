using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.Networking
{
    public abstract class HTTP
    {
        public const string PROTOCOL_IDENTIFIER = "HTTP/1.1";

        public class HTTPException : Exception
        {
            public StatusCodes StatusCode { get; private set; }

            public HTTPException(StatusCodes statusCode)
                : this(statusCode, null)
            {
            }
            public HTTPException(StatusCodes statusCode, string message)
                : base("HTTP " + Utilities.EnumToInt(statusCode) + " " + Utilities.EnumToString(statusCode) + (string.IsNullOrEmpty(message) ? "" : ": " + message))
            {
                StatusCode = statusCode;
            }
        }

        public enum Methods
        {
            GET,
            HEAD,
            POST,
            PUT,

            /// <summary>
            /// not method specified
            /// </summary>
            unknown
        }

        public enum StatusCodes
        {
            OK = 200,
            SeeOther = 303,
            BadRequest = 400,
            Unauthorized = 401,
            Forbidden = 403,
            NotFound = 404,
            MethodNotAllowed = 405,
            PreconditionFailed = 412,
            InternalServerError = 500
        }
    }
}
