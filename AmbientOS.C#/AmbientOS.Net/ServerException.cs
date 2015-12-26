using System;

namespace AmbientOS.Net
{
    class ServerException : Exception
    {
        public enum ErrorCode
        {
            /*** defined in BEP5 **/

            /// <summary>
            /// Generic Error
            /// </summary>
            GenericError = 201,

            /// <summary>
            /// Server Error
            /// </summary>
            ServerError = 202,

            /// <summary>
            /// Protocol Error, such as a malformed packet, invalid arguments, or bad token
            /// </summary>
            ProtocolError = 203,

            /// <summary>
            /// KRPC Method Unknown
            /// </summary>
            MethodUnknown = 204,


            /*** defined in BEP44 ***/

            /// <summary>
            /// Value (v field) to big (shouldn't exceed 1000 bytes)
            /// </summary>
            MessageTooBig = 205,

            /// <summary>
            /// Invalid signature for mutable value
            /// </summary>
            InvalidSignature = 206,

            /// <summary>
            /// Salt too big (shouldn't exceed 64 bytes)
            /// </summary>
            SaltTooBig = 207,

            /// <summary>
            /// Expected sequence number mismatch for compare-and-swap, re-read and try again.
            /// </summary>
            CASMismatch = 301,

            /// <summary>
            /// Provided sequence number less than current
            /// </summary>
            SequenceNumberLess = 302
        }

        private static string GetStandardMessage(ErrorCode errorCode)
        {
            switch (errorCode) {
                case ErrorCode.GenericError: return "generic error";
                case ErrorCode.ServerError: return "server error";
                case ErrorCode.ProtocolError: return "protocol error";
                case ErrorCode.MethodUnknown: return "method unknown";
                case ErrorCode.MessageTooBig: return "message(v field) too big";
                case ErrorCode.InvalidSignature: return "invalid signature";
                case ErrorCode.SaltTooBig: return "salt(salt field) too big";
                case ErrorCode.CASMismatch: return "the CAS hash mismatched, re-read value and try again";
                case ErrorCode.SequenceNumberLess: return "sequence number less than current";
                default: return "unknown error code " + (int)errorCode;
            }
        }

        public ErrorCode Code { get; }
        public new string Message { get; }

        public ServerException(ErrorCode errorCode, string message)
            : base("server error: " + message)
        {
            Code = errorCode;
            Message = message;
        }

        public ServerException(ErrorCode errorCode)
            : this(errorCode, GetStandardMessage(errorCode))
        {
        }
    }
}
