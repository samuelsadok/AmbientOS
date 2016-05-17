using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace AmbientOS.Net
{
    /// <summary>
    /// Implementation according to specification at
    /// http://wiki.theory.org/BitTorrentSpecification#Bencoding
    /// </summary>
    abstract class BEncode
    {
        public static Encoding Encoding = Encoding.Default;

        public byte[] Encode()
        {
            var result = new MemoryStream();
            Encode(result).Wait();
            return result.ToArray();
        }

        public abstract Task Encode(Stream stream);

        public static BEncode Decode(byte[] buffer)
        {
            var index = 0;
            return Decode(buffer, ref index);
        }

        public static BEncode Decode(byte[] buffer, ref int index)
        {
            var c = Encoding.GetChars(buffer, index, 1).Single();
            switch (c) {
                case 'i': return BInt.Decode(buffer, ref index);
                case 'l': return BList.Decode(buffer, ref index);
                case 'd': return BDict.Decode(buffer, ref index);
                case 'e': index++; return null;
                default:
                    if (char.IsDigit(c))
                        return BString.Decode(buffer, ref index);
                    else
                        throw new ServerException(ServerException.ErrorCode.ProtocolError, "invalid prefix char for bencoded value: " + c);
            }
        }
    }

    class BInt : BEncode
    {
        public long Value { get; }

        public BInt(long value)
        {
            Value = value;
        }

        public static new BInt Decode(byte[] buffer, ref int index)
        {
            var c = Encoding.GetChars(buffer, index++, 1).Single();
            if (c != 'i')
                throw new ArgumentException("This is not an integer.");

            var s = new StringBuilder();
            while (char.IsDigit(c = Encoding.GetChars(buffer, index++, 1).Single()))
                s.Append(c);

            if (c != 'e')
                throw new ServerException(ServerException.ErrorCode.ProtocolError, "the integer is not properly terminated");

            return new BInt(long.Parse(s.ToString()));
        }

        public override async Task Encode(Stream stream)
        {
            await stream.Write("i", Encoding);
            await stream.Write(Value.ToString(), Encoding);
            await stream.Write("e", Encoding);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    class BString : BEncode
    {
        public byte[] BinaryValue { get; }
        public string Value { get { return Encoding.GetString(BinaryValue); } }

        public BString(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException($"{value}");
            BinaryValue = value;
        }

        public BString(string value)
            : this(Encoding.GetBytes(value))
        {
        }

        public static new BString Decode(byte[] buffer, ref int index)
        {
            char c;
            var s = new StringBuilder();
            while (char.IsDigit(c = Encoding.GetChars(buffer, index++, 1).Single()))
                s.Append(c);

            if (c != ':')
                throw new ServerException(ServerException.ErrorCode.ProtocolError, "invalid bencoded string");

            long length;
            if (!long.TryParse(s.ToString(), out length))
                throw new ServerException(ServerException.ErrorCode.ProtocolError, "invalid string length specifier");

            var val = new byte[length];
            Array.Copy(buffer, index, val, 0, val.Count());
            index += val.Count();

            return new BString(val);
        }

        public override async Task Encode(Stream stream)
        {
            await stream.Write(BinaryValue.Count().ToString(), Encoding);
            await stream.Write(":", Encoding);
            await stream.Write(BinaryValue);
        }

        public override string ToString()
        {
            return "\"" + Value + "\" (" + BinaryValue.Count() + ")";
        }
    }

    class BList : BEncode
    {
        public List<BEncode> List { get; }

        public BList(List<BEncode> values)
        {
            List = values;
        }

        public BList(params BEncode[] values)
            : this(values.ToList())
        {
        }

        public BList()
            : this(new List<BEncode>())
        {
        }

        public static new BList Decode(byte[] buffer, ref int index)
        {
            var c = Encoding.GetChars(buffer, index++, 1).Single();
            if (c != 'l')
                throw new ArgumentException("This is not a list.");

            var val = new List<BEncode>();
            BEncode item;
            while ((item = BEncode.Decode(buffer, ref index)) != null)
                val.Add(item);

            return new BList(val);
        }

        public override async Task Encode(Stream stream)
        {
            await stream.Write("l", Encoding);
            foreach (var value in List)
                await value.Encode(stream);
            await stream.Write("e", Encoding);
        }

        public override string ToString()
        {
            return "[" + string.Join(", ", List.Select(item => item.ToString())) + "]";
        }
    }

    class BDict : BEncode
    {
        public Dictionary<string, BEncode> Dict { get; }

        public BDict(Dictionary<string, BEncode> values)
        {
            Dict = values;
        }

        public BDict()
            : this(new Dictionary<string, BEncode>())
        {
        }

        public static new BDict Decode(byte[] buffer, ref int index)
        {
            var c = Encoding.GetChars(buffer, index++, 1).Single();
            if (c != 'd')
                throw new ArgumentException("This is not a dictionary.");

            var val = new Dictionary<string, BEncode>();
            BString name;
            while ((name = (BString)BEncode.Decode(buffer, ref index)) != null) {
                var item = BEncode.Decode(buffer, ref index);
                if (item == null)
                    throw new ServerException(ServerException.ErrorCode.ProtocolError, "unexpected end of dictionary");
                val[name.Value] = item;
            }

            return new BDict(val);
        }

        public override async Task Encode(Stream stream)
        {
            await stream.Write("d", Encoding);
            foreach (var kv in Dict) {
                await new BString(kv.Key).Encode(stream);
                await kv.Value.Encode(stream);
            }
            await stream.Write("e", Encoding);
        }

        public override string ToString()
        {
            return "{" + string.Join(", ", Dict.Select(kv => "\"" + kv.Key + "\":" + kv.Value.ToString())) + "}";
        }
    }
}
