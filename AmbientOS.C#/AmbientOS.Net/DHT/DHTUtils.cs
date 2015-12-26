using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace AmbientOS.Net.DHT
{
    public static class DHTUtils
    {

        /// <summary>
        /// Returns the 20-byte contact info for a node
        /// </summary>
        internal static BString SerializeHash(BigInt id)
        {
            return new BString(id.GetBytes(20, Endianness.NetworkByteOrder));
        }

        /// <summary>
        /// Transforms a sequence of 20-byte node infos to hashes
        /// </summary>
        internal static IEnumerable<BigInt> DeserializeHashes(BString contactString)
        {
            var buffer = contactString.BinaryValue;
            if (buffer.Count() % 20 != 0)
                throw new Exception("not a complete list of hash values (length: " + buffer.Count() + ")");

            for (int i = 0; i < buffer.Count(); i += 20)
                yield return new BigInt(buffer.Skip(i).Take(20).ToArray(), Endianness.NetworkByteOrder);
        }

        /// <summary>
        /// Returns the 6-byte contact info for a peer
        /// </summary>
        internal static BString SerializePeer(IPEndPoint endpoint)
        {
            var buffer = endpoint.Address.GetAddressBytes().Concat(ByteConverter.WriteVal((ushort)endpoint.Port, Endianness.NetworkByteOrder)).ToArray();
            return new BString(buffer);
        }

        /// <summary>
        /// Transforms a 6-byte or 18-byte peer address to an IPv4 or IPv6 endpoint
        /// </summary>
        internal static IPEndPoint DeserializePeer(BString value)
        {
            var buffer = value.BinaryValue;

            IPAddress addr;
            int port;

            if (buffer.Count() == 6) {
                addr = new IPAddress(buffer.Take(4).ToArray());
                port = buffer.ReadUInt16(4, Endianness.NetworkByteOrder);
            } else if (buffer.Count() == 18) {
                addr = new IPAddress(buffer.Take(16).ToArray());
                port = buffer.ReadUInt16(16, Endianness.NetworkByteOrder);
            } else {
                throw new Exception("unknown peer contact format (length " + buffer.Count() + ")");
            }

            return new IPEndPoint(addr, port);
        }

        /// <summary>
        /// Transforms a mixed sequence of 6-byte and 18-byte peer addresses to IP endpoints
        /// </summary>
        internal static IEnumerable<IPEndPoint> DeserializePeers(BList list)
        {
            return list.List.Select(item => DeserializePeer((BString)item));
        }

        /// <summary>
        /// Returns the 26-byte contact info for a node (ID and IP endpoint)
        /// </summary>
        internal static BString SerializeNodes(IEnumerable<Node> nodes)
        {
            var result = Enumerable.Empty<byte>();

            foreach (var node in nodes) {
                var part1 = SerializeHash(node.ID).BinaryValue;
                var part2 = SerializePeer(node.Endpoint).BinaryValue;
                if (part1.Length + part2.Length != 26 && part1.Length + part2.Length != 38)
                    throw new Exception("fail length");
                result = result.Concat(part1).Concat(part2);
            }

            return new BString(result.ToArray());
        }

        /// <summary>
        /// Transforms a sequence of 26-byte or 38-byte contact strings into to nodes.
        /// </summary>
        internal static IEnumerable<Tuple<BigInt, IPEndPoint>> DeserializeNodes(BString contactString, bool isIPv6)
        {
            var buffer = contactString.BinaryValue;
            var lengthPerItem = isIPv6 ? 38 : 26;
            var addressLength = lengthPerItem - 22;

            if (buffer.Count() % lengthPerItem != 0) {
                yield return null;
                //throw new Exception("not a complete list of 26-byte or 38-byte contact strings (length: " + buffer.Count() + ")");
            }

            // this is only to make debugging easier
            var count = (buffer.Count() / lengthPerItem) * lengthPerItem;

            for (int i = 0; i < count; i += lengthPerItem) {
                var addr = new IPAddress(buffer.Skip(i + 20).Take(addressLength).ToArray());
                var port = buffer.ReadUInt16(i + 20 + addressLength, Endianness.NetworkByteOrder);
                yield return new Tuple<BigInt, IPEndPoint>(
                    new BigInt(buffer.Skip(i).Take(20).ToArray(), Endianness.NetworkByteOrder),
                    new IPEndPoint(addr, port)
                    );
            }
        }


        private static readonly byte[] sanityMask4 = new byte[] { 0x03, 0x0f, 0x3f, 0xff };
        private static readonly byte[] sanityMask6 = new byte[] { 0x01, 0x03, 0x07, 0x0f, 0x1f, 0x3f, 0x7f, 0xff };

        /// <summary>
        /// Computes the 3 bytes required for an ID that complies to BEP42.
        /// The result represents the first 3 bytes of the node ID.
        /// In the 3rd byte, only the MSB is relevant, the rest is zero and can be ORed with a random number.
        /// </summary>
        /// <param name="random">A random byte. This should be equal to the last byte in the final hash.</param>
        public static byte[] ComputeImportantIDBytes(IPAddress address, byte random)
        {
            var input = address.GetAddressBytes()
                .Zip(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? sanityMask6 : sanityMask4, (b1, b2) => (byte)(b1 & b2))
                .ToArray();
            input[0] |= (byte)((random & 7) << 5);
            var output = CRC32.Castagnoli().ComputeHash(input).Take(3).ToArray(); // todo: make CRC static and see if it still works
            output[2] = (byte)(output[2] & 0x80);
            //output[3] = random;
            return output;
        }

        /// <summary>
        /// Computes a random node ID that complies to BEP42 for the specified IP address.
        /// </summary>
        public static BigInt ComputeCompliantID(IPAddress address)
        {
            var bytes = BigInt.FromRandom(143).GetBytes(20, Endianness.BigEndian);
            var hashed = ComputeImportantIDBytes(address, bytes[19]);
            bytes[0] |= hashed[0];
            bytes[1] |= hashed[1];
            bytes[2] |= hashed[2];
            return new BigInt(bytes, Endianness.BigEndian);
        }

        /// <summary>
        /// Checks if the ID of this node complies to the rules defined in BEP42.
        /// Nodes that don't use a compliant ID should still be served, but request should be sent to them with care.
        /// </summary>
        public static bool IsIDCompliant(this BigInt hash, IPAddress address)
        {
            // todo: ignore IP addresses from certain ranges (such as subnets) - see BEP42

            // for some reason, none of the IPv6 nodes comply with BEP42, probably we're doing something wrong.
            // todo: fix this (for now we just ignore it and accept all IPv6 nodes)
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                return true;

            var hashed = ComputeImportantIDBytes(address, hash.GetByte(19, 20));
            return (hash.GetByte(0, 20) == hashed[0]) && (hash.GetByte(1, 20) == hashed[1]) && ((hash.GetByte(2, 20) & 0x80) == hashed[2]);
        }
    }
}
