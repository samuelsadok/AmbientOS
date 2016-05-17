using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using AmbientOS.Net.KRPC;
using static AmbientOS.LogContext;

namespace AmbientOS.Net.DHT
{
    /// <summary>
    /// Represents a remote node in the DHT network.
    /// These nodes make up the entries of the routing table. This class implements sending queries to the remote endpoint.
    /// </summary>
    class Node
    {
        /// <summary>
        /// Identifies this node.
        /// </summary>
        public BigInt ID { get; private set; }

        /// <summary>
        /// The remote endpoint of this node.
        /// </summary>
        public IPEndPoint Endpoint { get; set; }

        /// <summary>
        /// Indicates whether this node is an IPv4 or an IPv6 node.
        /// If a DHT participant uses both protocols, two node instances with the respective addresses are neccessary.
        /// </summary>
        public bool IsIPv6 { get { return Endpoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6; } }

        /// <summary>
        /// Returns true if the node has ever responded to one of our requests and if it has been active recently.
        /// </summary>
        public bool IsGood { get { return LastResponse.HasValue && (LastActivity.HasValue ? (DateTime.Now - LastActivity.Value) < Bucket.REFRESH_RATE : false); } }

        /// <summary>
        /// Returns true if the node failed to respond to multiple queries in a row.
        /// A bad node is evicted from the routing table without further pings.
        /// A node that is neither good nor bad ("questionable") is pinged before being evicted.
        /// </summary>
        public bool IsBad { get { return !LastResponse.HasValue; } }

        private long lastResponse = -1;
        private long lastActivity = -1;

        /// <summary>
        /// The last time the node responded to one of our queries. Null if it never responded.
        /// </summary>
        public DateTime? LastResponse { get { var ticks = Interlocked.Read(ref lastResponse); return ticks < 0 ? (DateTime?)null : new DateTime(ticks); } }

        /// <summary>
        /// The last time the node sent us a query. Null if the node never sent a query.
        /// </summary>
        public DateTime? LastActivity { get { var ticks = Interlocked.Read(ref lastActivity); return ticks < 0 ? (DateTime?)null : new DateTime(ticks); } }

        private RoutingTable routingTable;

        /// <summary>
        /// Stores tokens received from this node.
        /// A store request requires such a token.
        /// Generally, tokens may be associated with a hash.
        /// </summary>
        private Dictionary<BigInt, Token> tokens = new Dictionary<BigInt, Token>();

        /// <summary>
        /// Tokens for announce_peer requests are not bound to the hash, so we store it separately.
        /// </summary>
        private Token unboundToken = null;

        public Node(RoutingTable routingTable, BigInt id, IPEndPoint endpoint)
        {
            this.routingTable = routingTable;
            ID = id;
            Endpoint = endpoint;
            lastActivity = DateTime.Now.Ticks;
        }

        //private void LogDHT(string str, int verbosity)
        //{
        //    Logging.LogDHT(routingTable?.Socket?.LocalEndpoint, str, verbosity);
        //}

        /// <summary>
        /// Renews the last activity (and potentially response) time of this node.
        /// </summary>
        public void UpdateActivityTime(ConsiderationReason reason)
        {
            if (reason != ConsiderationReason.Rumor && reason != ConsiderationReason.Refresh) {
                if (reason == ConsiderationReason.DidRespond || reason == ConsiderationReason.DidCooperate)
                    Interlocked.Exchange(ref lastResponse, DateTime.Now.Ticks);
                if (reason == ConsiderationReason.DidCooperate)
                    Interlocked.Exchange(ref lastResponse, DateTime.Now.Ticks);
                Interlocked.Exchange(ref lastActivity, DateTime.Now.Ticks);
            }
        }

        private void HonourResponse(BDict response)
        {
            routingTable.dht.Consider(ID, Endpoint, null, InterestFlags.NoInterest, ConsiderationReason.DidRespond);
        }

        private void HonourResponse(BDict response, BigInt hashForWhichWeGotAToken)
        {
            DebugLog(this + " received a token");

            routingTable.dht.Consider(ID, Endpoint, null, InterestFlags.Peers, ConsiderationReason.DidCooperate);

            if (hashForWhichWeGotAToken != null)
                routingTable.dht.Consider(ID, Endpoint, hashForWhichWeGotAToken, InterestFlags.Data, ConsiderationReason.DidCooperate);
        }

        /// <summary>
        /// Ping the node to see if it's still alive.
        /// </summary>
        /// <param name="count">Number of attempts.</param>
        public bool Ping(int count)
        {
            while (count-- > 0)
                if (Ping() != null)
                    return true;
            return false;
        }

        /// <summary>
        /// Checks if the node has given us a token for the specified hash.
        /// </summary>
        /// <param name="hash">Null to check for the unbound token</param>
        public bool HasToken(BigInt hash)
        {
            lock (tokens) {
                if (hash == null)
                    return unboundToken != null;
                return tokens.ContainsKey(hash);
            }
        }

        internal BDict Ping()
        {
            var args = new BDict();
            args.Dict["id"] = routingTable.Contact;
            try {
                var startTime = DateTime.Now;
                var response = routingTable.Socket.Call(DHT.PING_FUNC, args, Endpoint);

                if (response == null) {
                    Log("ping failed", LogType.Warning);
                    return null;
                }

                var latency = DateTime.Now - startTime;
                if (ID == null)
                    ID = DHTUtils.DeserializeHashes((BString)response.Dict["id"]).Single();
                HonourResponse(response);

                Log(string.Format("ping succeeded ({0} ms), response: {1}", latency.TotalMilliseconds, response), LogType.Success);

                return response;
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                Log(string.Format("unexpected error in {0} request to {1}: {2}", "ping", Endpoint, ex.Message), LogType.Error);
                return null;
            }
        }


        private BDict FindNodesOrPeers(string hashKey, string method, BigInt hash, ref IEnumerable<Tuple<BigInt, IPEndPoint>> nodes, bool includeIPv4Nodes, bool includeIPv6Nodes)
        {
            var args = new BDict();
            args.Dict["id"] = routingTable.Contact;
            args.Dict[hashKey] = DHTUtils.SerializeHash(hash);

            var want = new BList();
            if (includeIPv4Nodes)
                want.List.Add(new BString("n4"));
            if (includeIPv6Nodes)
                want.List.Add(new BString("n6"));
            args.Dict["want"] = want;

            return GetValue(args, method, null, ref nodes);
        }

        private BDict GetValue(BDict args, string method, BigInt tokenStoreHash, ref IEnumerable<Tuple<BigInt, IPEndPoint>> nodes)
        {
            try {
                var response = routingTable.Socket.Call(method, args, Endpoint);
                if (response == null)
                    return null;

                BEncode nodesString, tokenString;

                // todo: maybe we shouldn't filter our own ID here, since we may have multiple IDs
                if (response.Dict.TryGetValue("nodes", out nodesString))
                    nodes = nodes.Concat(DHTUtils.DeserializeNodes((BString)nodesString, false).Where(node => node.Item1 != routingTable.LocalID));
                if (response.Dict.TryGetValue("nodes6", out nodesString))
                    nodes = nodes.Concat(DHTUtils.DeserializeNodes((BString)nodesString, true).Where(node => node.Item1 != routingTable.LocalID));
                
                if (response.Dict.TryGetValue("token", out tokenString)) {
                    Token token = new Token(((BString)tokenString).BinaryValue);
                    lock (tokens) {
                        if (tokenStoreHash == null)
                            unboundToken = token;
                        else
                            tokens[tokenStoreHash] = token;
                    }
                    HonourResponse(response, tokenStoreHash);
                } else {
                    HonourResponse(response);
                }


                return response;

            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                Log(string.Format("unexpected error in {0} request to {1}: {2}", method, Endpoint, ex.Message), LogType.Error);
            }
            return null;
        }

        /// <summary>
        /// Query the node for the closest nodes it knows about.
        /// If the node knows the exact requested node, it returns only that node, otherwise it returns multiple close nodes.
        /// </summary>
        /// <param name="hash">The node ID to which the closest nodes are requested.</param>
        /// <param name="nodes">The list to which to append the new nodes.</param>
        public bool FindNodes(BigInt hash, ref IEnumerable<Tuple<BigInt, IPEndPoint>> nodes, bool includeIPv4, bool includeIPv6)
        {
            return FindNodesOrPeers("target", DHT.FIND_NODES_FUNC, hash, ref nodes, includeIPv4, includeIPv6) != null;
        }

        /// <summary>
        /// Queries the node for the peers of the specified infohash.
        /// If the node doesn't know about any relevant peers, it returns the closest nodes instead.
        /// </summary>
        /// <param name="includeIPv4">If true, the node is explicitly queried for IPv4 nodes. If false, they may be included anyway.</param>
        /// <param name="includeIPv6">If true, the node is explicitly queried for IPv6 nodes. If false, they may be included anyway.</param>
        public IEnumerable<IPEndPoint> GetPeers(BigInt hash, ref IEnumerable<Tuple<BigInt, IPEndPoint>> nodes, bool includeIPv4, bool includeIPv6)
        {
            var response = FindNodesOrPeers("info_hash", DHT.GET_PEERS_FUNC, hash, ref nodes, includeIPv4, includeIPv6);
            try {
                BEncode values;
                if (response != null)
                    if (response.Dict.TryGetValue("values", out values))
                        return DHTUtils.DeserializePeers((BList)values);
            } catch (Exception ex) {
                Log(string.Format("unexpected error in {0} request to {1}: {2}", "get_peers", Endpoint, ex.Message), LogType.Error);
            }
            return Enumerable.Empty<IPEndPoint>();
        }

        /// <summary>
        /// Announces the local peer to this node in relation to the specified hash.
        /// In BitTorrent that means that the local peer is sharing the according torrent.
        /// This call does nothing on a node that has no token. A token is obtained by calling GetPeers first.
        /// Returns a string that describes the announce error (or null on success).
        /// </summary>
        public string AnnouncePeer(BigInt hash)
        {
            Token token;
            lock (tokens) {
                if ((token = unboundToken) == null)
                    return "this node hasn't given us a token for announcing peers";
            }

            var args = new BDict();
            args.Dict["id"] = routingTable.Contact;
            args.Dict["info_hash"] = DHTUtils.SerializeHash(hash);
            args.Dict["implied_port"] = new BInt(1); // always announce implied port (peer uses the same port as DHT)
            args.Dict["port"] = new BInt(0);
            args.Dict["token"] = new BString(token.Value);
            try {
                var response = routingTable.Socket.Call(DHT.ANNOUNCE_PEER_FUNC, args, Endpoint);
                if (response != null) {
                    HonourResponse(response);
                    return null;
                }
                return "no response from server";
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                Log(string.Format("unexpected error in {0} request to {1}: {2}", "announce_peer", Endpoint, ex.Message), LogType.Error);
                return ex.Message;
            }
        }

        /// <summary>
        /// Queries and verifies the value of a mutable or immutable data item on this node.
        /// Returns true if the query returned valid data or a token.
        /// </summary>
        /// <param name="sequenceNumber">Set to the sequence number that the node reported. Null if no sequence number was reported.</param>
        /// <param name="hash">For immutable items: the hash of the item. For mutable data: the hash of the public key and salt.</param>
        public bool Get(DHTData data, out long? sequenceNumber, ref IEnumerable<Tuple<BigInt, IPEndPoint>> nodes, out string message)
        {
            sequenceNumber = null;

            var args = new BDict();
            args.Dict["id"] = routingTable.Contact;
            args.Dict["target"] = DHTUtils.SerializeHash(data.Hash);
            lock (data) {
                if (data.SequenceNumber.HasValue)
                    args.Dict["seq"] = new BInt(data.SequenceNumber.Value);
            }

            var response = GetValue(args, DHT.GET_DATA_FUNC, data.Hash, ref nodes);
            if (response == null) {
                message = "timeout";
                return false;
            }

            try {
                BEncode value, seqNo, key, signature;

                if (!response.Dict.TryGetValue("v", out value)) {
                    message = "the node returned no value";
                    return HasToken(data.Hash);
                }
                response.Dict.TryGetValue("seq", out seqNo);
                response.Dict.TryGetValue("k", out key);
                response.Dict.TryGetValue("sig", out signature);

                DHTData newData;
                if (key != null && signature != null)
                    newData = new DHTData(((BString)value).BinaryValue, ((BString)key).BinaryValue, data.Salt, ((BString)signature).BinaryValue, ((BInt)seqNo)?.Value);
                else
                    newData = new DHTData(((BString)value).BinaryValue);

                lock (data) {
                    message = data.Apply(newData) ?? "new data was retrieved";
                    return true;
                }
            } catch (Exception ex) {
                Log(string.Format("unexpected error in {0} request to {1}: {2}", "get", Endpoint, ex.Message), LogType.Error);
                message = "get failed: " + ex.Message;
                return false;
            }
        }

        ///// <summary>
        ///// Queries and verifies the value of a mutable or immutable data item on this node.
        ///// Returns null if the data is not found, and throws an exception on verification error.
        ///// </summary>
        //public byte[] Get(Storage.DHTData data, ref IEnumerable<Tuple<Hash, IPEndPoint>> nodes, CancellationToken cancellationToken)
        //{
        //    long? seqNo = null;
        //    return Get(hash, null, ref nodes, ref seqNo, cancellationToken);
        //}

        ///// <summary>
        ///// Queries and verifies the value of an mutable data item on this node.
        ///// Returns null if the data is not found, and throws an exception on verification error.
        ///// </summary>
        //public byte[] Get(byte[] publicKey, byte[] salt, ref long? sequenceNumber, ref IEnumerable<Node> nodes, CancellationToken cancellationToken)
        //{
        //    long? seqNo = sequenceNumber;
        //    var result = Get(hash, null, ref nodes, ref seqNo, cancellationToken);
        //    if (result != null)
        //        sequenceNumber = seqNo.Value;
        //    return result;
        //}


        /// <summary>
        /// Should return false if there was no newer data (including if the request failed).
        /// </summary>
        /// <param name="oldSeqNo">The expected old sequence number. If the node has a mismatching sequence number, it will reject the value.</param>
        internal bool Put(DHTData data, long? oldSeqNo, ref string message)
        {
            Token token;
            lock (tokens) {
                if (!tokens.TryGetValue(data.Hash, out token)) {
                    message += "this node hasn't given us a token for " + data.Hash;
                    return false;
                }
            }

            var args = new BDict();
            args.Dict["id"] = routingTable.Contact;
            args.Dict["v"] = new BString(data.Data);
            args.Dict["token"] = new BString(token.Value);

            if (data.PublicKey != null)
                args.Dict["k"] = new BString(data.PublicKey);
            if (data.Signature != null)
                args.Dict["sig"] = new BString(data.Signature);
            if (data.SequenceNumber.HasValue)
                args.Dict["seq"] = new BInt(data.SequenceNumber.Value);
            if (oldSeqNo.HasValue)
                args.Dict["cas"] = new BInt(oldSeqNo.Value);
            if ((data.Salt?.Count() ?? 0) > 0)
                args.Dict["salt"] = new BString(data.Salt);

            try {
                var response = routingTable.Socket.Call(DHT.PUT_DATA_FUNC, args, Endpoint);
                if (response == null) {
                    message += "timeout";
                    return false;
                }

                HonourResponse(response);
                message += "data was stored on the node";
                return false;
            } catch (ServerException ex) {
                if (ex.Code == ServerException.ErrorCode.ProtocolError && ex.Message == "invalid token")
                    Log(string.Format("invalid token in put request to {1}:", "put", Endpoint, ex.Message, ex.Code), LogType.Warning);
                else
                    Log(string.Format("server error {3} in {0} request to {1}: {2}", "put", Endpoint, ex.Message, ex.Code), LogType.Warning);
                message += "put failed: " + ex.Message;
                return false;
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                //if (ex.Message == "invalid")
                Log(string.Format("unexpected error in {0} request to {1}: {2}", "put", Endpoint, ex.Message), LogType.Error);
                message += "put failed: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Retrieves the specified data item from the node and stores the most recent version back on the node.
        /// Returns true if the node did cooperate.
        /// </summary>
        public bool Sync(DHTData data, ref IEnumerable<Tuple<BigInt, IPEndPoint>> nodes, out string message)
        {
            long? sequenceNumber;

            while (Get(data, out sequenceNumber, ref nodes, out message)) {
                message += " and ";
                if (data.Data == null) {
                    message += "data is empty";
                    break;
                } else if (!Put(data, sequenceNumber, ref message)) {
                    break;
                }
            }

            lock (data) {
                data.MaybeInvokeCallback(); // todo: in event, sync with other nodes
            }

            return HasToken(data.Hash);
        }


        public override bool Equals(object obj)
        {
            var node = obj as Node;
            if (node == null)
                return false;
            return Equals(ID, node.ID) && Equals(Endpoint, node.Endpoint);
        }

        public override int GetHashCode()
        {
            return unchecked((ID?.GetHashCode() ?? 0) + (Endpoint?.GetHashCode() ?? 0));
        }

        public override string ToString()
        {
            //var endpoint = Endpoint4 == null ? (Endpoint6 == null ? "[unknown endpoint]" : Endpoint6.ToString()) : (Endpoint6 == null ? Endpoint4.ToString() : (Endpoint4.ToString() + "/" + Endpoint6.ToString()));
            return "node " + ID + " @" + Endpoint;
        }
    }
}
