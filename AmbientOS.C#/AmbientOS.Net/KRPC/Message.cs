using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.Net.KRPC
{
    abstract class Message
    {
        protected abstract BDict GetDictionary();
        protected abstract string Type { get; }
        public ushort TransactionID { get; set; }
        public byte[] IPAddress { get; set; }

        public byte[] Serialize()
        {
            var dict = GetDictionary();
            if (dict.Dict.ContainsKey("y") || dict.Dict.ContainsKey("t"))
                throw new Exception("The keys \"t\" and \"y\" are reserved.");

            dict.Dict["t"] = new BString(ByteConverter.WriteVal(TransactionID, Endianness.NetworkByteOrder));
            dict.Dict["y"] = new BString(Type);

            if (IPAddress != null)
                dict.Dict["ip"] = new BString(IPAddress);

            return dict.Encode();
        }

        public static Message FromBytes(byte[] buffer)
        {
            var dict = (BDict)BEncode.Decode(buffer);

            var y = ((BString)dict.Dict["y"]).Value;
            Message msg;
            switch (y) {
                case "q": msg = new QueryMessage(dict); break;
                case "r": msg = new ResponseMessage(dict); break;
                case "e": msg = new ErrorMessage(dict); break;
                default: throw new FormatException("unknown message type");
            }

            msg.TransactionID = ((BString)dict.Dict["t"]).BinaryValue.ReadUInt16(0, Endianness.NetworkByteOrder);

            BEncode ip;
            if (dict.Dict.TryGetValue("ip", out ip))
                msg.IPAddress = (ip as BString)?.BinaryValue;

            if (dict.Dict.TryGetValue("ro", out ip))
                Console.WriteLine("*** FOUND READ-ONLY NODE ***");

            return msg;
        }
    }

    class QueryMessage : Message
    {
        protected override string Type { get { return "q"; } }
        public string Method { get; }
        public BDict Arguments { get; }

        public QueryMessage(string method, BDict args)
        {
            Method = method;
            Arguments = args;
        }

        public QueryMessage(BDict dict)
        {
            Method = ((BString)dict.Dict["q"]).Value;
            Arguments = (BDict)dict.Dict["a"];
        }

        protected override BDict GetDictionary()
        {
            return new BDict(new Dictionary<string, BEncode>() {
                { "q", new BString(Method) },
                { "a", Arguments }
            });
        }
    }

    class ResponseMessage : Message
    {
        protected override string Type { get { return "r"; } }
        public BDict ReturnValues { get; set; }

        public ResponseMessage()
        {
            ReturnValues = new BDict(new Dictionary<string, BEncode>());
        }

        public ResponseMessage(BDict dict)
        {
            ReturnValues = (BDict)dict.Dict["r"];
        }

        protected override BDict GetDictionary()
        {
            return new BDict(new Dictionary<string, BEncode>() {
                { "r", ReturnValues },
                { "ip", new BString(IPAddress) }
            });
        }
    }

    class ErrorMessage : Message
    {
        protected override string Type { get { return "e"; } }
        public string Message { get; }
        public long ErrorCode { get; }

        public ErrorMessage(Exception exception)
        {
            Message = exception.Message;

            var serverException = exception as ServerException;
            ErrorCode = (int)(serverException?.Code ?? ServerException.ErrorCode.ServerError);
        }

        public ErrorMessage(BDict dict)
        {
            var list = ((BList)dict.Dict["e"]).List;
            ErrorCode = ((BInt)list[0]).Value;
            Message = ((BString)list[1]).Value;
        }

        protected override BDict GetDictionary()
        {
            return new BDict(new Dictionary<string, BEncode>() {
                { "e", new BList(new BInt(ErrorCode), new BString(Message)) }
            });
        }

        public Exception ToException()
        {
            return new ServerException((ServerException.ErrorCode)ErrorCode, Message);
        }
    }
}
