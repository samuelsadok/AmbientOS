using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using AmbientOS.Net.KRPC;

namespace AmbientOS.Net.DHT
{
    /// <summary>
    /// Implements Mainline DHT (BEP5), the distributed hash table used by BitTorrent.
    /// The DHT acts like a large hash table, where we can store/edit and look up 3 types of data:
    /// 1. lists of peers for a specific torrent (the key is the info-hash of the torrent) (original MLDHT)
    /// 2. arbitrary immutable data (the key is the hash of the data) (this was introduced with BEP44)
    /// 3. arbitrary signed mutable data (the key is the hash of the public key of the originator) (this was introduced with BEP44)
    /// 
    /// Extension support:
    /// BEP32 (IPv6 support): implemented
    /// BEP42 (choose local node ID according to IP address): implemented for IPv4 only (seems to be broken for IPv6)
    /// BEP43 (read-only nodes): todo
    /// BEP44 (storing arbitrary data): implemented
    /// BEP45 (multiple-address operation): kindof implemented
    /// </summary>
    public class DHT
    {
        public const string PING_FUNC = "ping";
        public const string FIND_NODES_FUNC = "find_node";
        public const string GET_PEERS_FUNC = "get_peers";
        public const string ANNOUNCE_PEER_FUNC = "announce_peer";
        public const string GET_DATA_FUNC = "get";
        public const string PUT_DATA_FUNC = "put";

        /// <summary>
        /// Minimum refresh rate for mutable data (30sec)
        /// </summary>
        private const int STORAGE_MIN_REFRESH = 30;

        /// <summary>
        /// Maximum refresh rate for mutable data (30min)
        /// </summary>
        private const int STORAGE_MAX_REFRESH = 1800;

        /// <summary>
        /// The number of threads that ping bootstrapping nodes.
        /// More threads may help filling the routing table more quickly.
        /// </summary>
        private const int BOOT_THREADS = 8;

        /// <summary>
        /// A list of well known bootstrap nodes.
        /// </summary>
        public static readonly string[] BOOTSTRAP_NODES = new string[] {
            "router.utorrent.com",
            //"localhost", // don't make this the first address - it's a waste of time under normal operation
            "router.bittorrent.com",
            "router.bitcomet.com",
            "dht.transmissionbt.com",
            "dht.aelitis.com"
        };

        /// <summary>
        /// A list of standard ports used by bootstrap nodes.
        /// </summary>
        public static readonly int[] BOOTSTRAP_PORTS = new int[] { 6881, 8991 };
        

        /// <summary>
        /// The local routing tables, each having a unique ID and consisting of up to 157 buckets, each containg up to 8 nodes.
        /// </summary>
        internal readonly RoutingTable[] routingTables;

        /// <summary>
        /// Stores data that was announced to this node and data that this node is interested in.
        /// </summary>
        internal readonly Storage storage;

        /// <summary>
        /// Represents the context in which the DHT runs.
        /// If this is cancelled, DHT shuts down.
        /// </summary>
        internal Context context;

        public DHT(MultiEndpointSocket socket)
        {
            storage = new Storage();

            routingTables = new RoutingTable[socket.Sockets.Count()];

            for (int i = 0; i < socket.Sockets.Count(); i++) {
                var currentSocket = socket.Sockets[i];
                var routingTable = routingTables[i] = new RoutingTable(this, currentSocket);

                currentSocket.Register(PING_FUNC, (args, endpoint) => PingHandler(args, endpoint, routingTable));
                currentSocket.Register(FIND_NODES_FUNC, (args, endpoint) => FindNodesHandler(args, endpoint, routingTable));
                currentSocket.Register(GET_PEERS_FUNC, (args, endpoint) => GetPeersHandler(args, endpoint, routingTable));
                currentSocket.Register(ANNOUNCE_PEER_FUNC, (args, endpoint) => AnnouncePeerHandler(args, endpoint, routingTable));
                currentSocket.Register(GET_DATA_FUNC, (args, endpoint) => GetDataHandler(args, endpoint, routingTable));
                currentSocket.Register(PUT_DATA_FUNC, (args, endpoint) => PutDataHandler(args, endpoint, routingTable));
            }
        }

        public void Start(Context context)
        {
            this.context = context;

            foreach (var routingTable in routingTables)
                routingTable.Start(context.SubContext(routingTable.Socket.LocalEndpoint.ToString()));

            var startBootBarrier = new Barrier(BOOT_THREADS);
            var endBootBarrier = new Barrier(BOOT_THREADS);
            var successfulBoot = false;
            IEnumerator<Tuple<string, int>> bootEndpoints = BOOTSTRAP_NODES.SelectMany(host => BOOTSTRAP_PORTS.Select(port => new Tuple<string, int>(host, port))).ToArray().AsEnumerable().GetEnumerator();

            // start the boot threads
            for (int i = 0; i < BOOT_THREADS; i++) {
                var threadID = i;
                new CancelableThread(() => {
                    while (!successfulBoot) {
                        // prepare combinations of bootstrap host and port
                        if (threadID == 0)
                            bootEndpoints.Reset();
                        startBootBarrier.SignalAndWait(context.Controller.CancellationToken);

                        while (true) {
                            // dequeue next host-port combination
                            Tuple<string, int> hostAndPort;
                            lock (bootEndpoints) {
                                if (!bootEndpoints.MoveNext())
                                    break;
                                hostAndPort = bootEndpoints.Current;
                            }

                            // resolve IP address of host
                            IPAddress[] addressList;
                            try {
                                addressList = Dns.GetHostAddresses(hostAndPort.Item1);
                            } catch (Exception ex) {
                                context.Log.Log(string.Format("DNS lookup for {0} failed: {1}", hostAndPort.Item1, ex.Message), LogType.Warning);
                                addressList = new IPAddress[0];
                            }

                            foreach (var addr in addressList) {
                                var endpoint = new IPEndPoint(addr, hostAndPort.Item2);
                                context.Log.Debug("boot thread {0}: ping on {1}", threadID, endpoint.ToString());
                                Consider(endpoint);
                            }
                        }

                        // we consider a boot successful if there is any routing table with at least 8 nodes (if there is no routing table, we give up)
                        successfulBoot = !routingTables.Any() || routingTables.Any(routingTable => routingTable.FindNodesLocally(routingTable.LocalID, 8).Count() >= 8);

                        if (!successfulBoot) {
                            endBootBarrier.SignalAndWait(context.Controller.CancellationToken);
                        } else {
                            startBootBarrier.RemoveParticipant();
                            endBootBarrier.RemoveParticipant();
                        }

                        if (threadID == 0) {
                            if (successfulBoot) {
                                context.Log.Log("DHT boot was successful", LogType.Success);
                                foreach (var routingTable in routingTables) {
                                    context.Log.Log("Routing Table for local endpoint " + routingTable.Socket.LocalEndpoint + ", public endpoint " + routingTable.Socket.PublicEndpoint, LogType.Info);
                                    context.Log.Log(routingTable.Dump(), LogType.Debug);
                                }
                            } else {
                                context.Log.Log("DHT boot failed (no node responded successfully - maybe your internet connection is broken)\nnext boot attempt in 5 seconds", LogType.Warning);
                                context.Controller.Wait(TimeSpan.FromSeconds(5));
                            }
                        }
                    }
                }).Start();
            }
        }


        internal void RenewNode(BDict args, IPEndPoint endpoint, RoutingTable routingTable)
        {
            BEncode contact;
            if (!args.Dict.TryGetValue("id", out contact))
                throw new ArgumentException("unspecified querier ID");
            var hash = DHTUtils.DeserializeHashes((BString)contact).Single();
            routingTable.Consider(hash, endpoint, null, InterestFlags.NoInterest, ConsiderationReason.DidTalk, context);
        }

        private BDict PingHandler(BDict args, IPEndPoint endpoint, RoutingTable routingTable)
        {
            RenewNode(args, endpoint, routingTable);
            return new BDict(new Dictionary<string, BEncode>() {
                { "id", routingTable.Contact }
            });
        }

        private BDict FindNodesHandler(BDict args, out BigInt hash, RoutingTable routingTable)
        {
            BEncode hashes;
            if (!args.Dict.TryGetValue("target", out hashes))
                hashes = args.Dict["info_hash"];
            hash = DHTUtils.DeserializeHashes((BString)hashes).Single();

            var dict = new BDict(new Dictionary<string, BEncode>() {
                { "id", routingTable.Contact }
            });

            var includeIPv4 = true;
            var includeIPv6 = false;

            BEncode wants;
            if (args.Dict.TryGetValue("want", out wants)) {
                includeIPv4 = ((BList)wants).List.Contains(new BString("n4"));
                includeIPv6 = ((BList)wants).List.Contains(new BString("n6"));
            }

            if (includeIPv4) {
                var nodes = FindIPv4NodesLocally(hash, Bucket.BUCKET_SIZE).ToArray();
                // The original specification only returns a single node if there is a perfect match.
                // With all the extensions, however, it's not clear if this still holds.
                //if (nodes.FirstOrDefault()?.ID == hash)
                //    nodes = new Node[] { nodes.First() };
                dict.Dict["nodes"] = DHTUtils.SerializeNodes(nodes);
            }

            if (includeIPv6) {
                var nodes = FindIPv6NodesLocally(hash, Bucket.BUCKET_SIZE).ToArray();
                //if (nodes.FirstOrDefault()?.ID == hash)
                //    nodes = new Node[] { nodes.First() };
                dict.Dict["nodes6"] = DHTUtils.SerializeNodes(nodes);
            }

            return dict;
        }

        private BDict FindNodesHandler(BDict args, IPEndPoint endpoint, RoutingTable routingTable)
        {
            RenewNode(args, endpoint, routingTable);
            BigInt hash;
            return FindNodesHandler(args, out hash, routingTable);
        }

        private BDict GetPeersHandler(BDict args, IPEndPoint endpoint, RoutingTable routingTable)
        {
            RenewNode(args, endpoint, routingTable);
            BigInt hash;
            var dict = FindNodesHandler(args, out hash, routingTable);

            var list = storage.GetPeerList(this, hash, false).Snapshot().Where(item => item.AddressFamily == endpoint.AddressFamily);

            if (list.Any())
                dict.Dict["values"] = new BList(list.Select(ep => DHTUtils.SerializePeer(ep)).Cast<BEncode>().ToList());

            dict.Dict["token"] = new BString(new Token(endpoint.Address).Value);
            return dict;
        }

        private BDict AnnouncePeerHandler(BDict args, IPEndPoint endpoint, RoutingTable routingTable)
        {
            RenewNode(args, endpoint, routingTable);

            if (!new Token(((BString)args.Dict["token"]).BinaryValue).Validate(endpoint.Address))
                throw new Exception("the provided token is invalid");

            if (((BInt)args.Dict["implied_port"]).Value == 0)
                endpoint = new IPEndPoint(endpoint.Address, (int)((BInt)args.Dict["port"]).Value);

            var hash = DHTUtils.DeserializeHashes((BString)args.Dict["info_hash"]).Single();

            var list = storage.GetPeerList(this, hash, true);
            list.Add(endpoint, false);

            return new BDict(new Dictionary<string, BEncode>() {
                { "id", routingTable.Contact }
            });
        }

        private BDict GetDataHandler(BDict args, IPEndPoint endpoint, RoutingTable routingTable)
        {
            RenewNode(args, endpoint, routingTable);

            // BEP44 dictates to always include both IPv4 and IPv6 nodes for "get" queries, so let's fake the respective arguments
            args.Dict["want"] = new BList(new BString("n4"), new BString("n6"));

            BigInt hash;
            var dict = FindNodesHandler(args, out hash, routingTable);

            BEncode seqNo;
            long sequenceNumber;
            if (!args.Dict.TryGetValue("seq", out seqNo))
                sequenceNumber = -1;
            else if ((sequenceNumber = ((BInt)seqNo).Value) < 0)
                throw new Exception("sequence number out of range");

            var value = storage.GetData(hash);

            if (value?.Data != null)
                dict.Dict["v"] = new BString(value.Data);
            if (value?.PublicKey != null)
                dict.Dict["k"] = new BString(value.PublicKey);
            if (value?.Signature != null)
                dict.Dict["sig"] = new BString(value.Signature);
            if (value?.SequenceNumber != null)
                dict.Dict["seq"] = new BInt(value.SequenceNumber.Value);

            dict.Dict["token"] = new BString(new Token(endpoint.Address, hash).Value);
            return dict;
        }

        private BDict PutDataHandler(BDict args, IPEndPoint endpoint, RoutingTable routingTable)
        {
            RenewNode(args, endpoint, routingTable);

            BEncode value, seqNo, key, signature, cas, salt;

            if (!args.Dict.TryGetValue("v", out value))
                throw new Exception("no data attached");
            args.Dict.TryGetValue("seq", out seqNo);
            args.Dict.TryGetValue("k", out key);
            args.Dict.TryGetValue("sig", out signature);
            args.Dict.TryGetValue("cas", out cas);
            args.Dict.TryGetValue("salt", out salt);

            DHTData newData;
            if (key != null && signature != null)
                newData = new DHTData(((BString)value).BinaryValue, ((BString)key).BinaryValue, ((BString)key).BinaryValue, ((BString)signature).BinaryValue, ((BInt)seqNo)?.Value);
            else
                newData = new DHTData(((BString)value).BinaryValue);

            if (!new Token(((BString)args.Dict["token"]).BinaryValue).Validate(endpoint.Address, newData.Hash))
                throw new Exception("the provided token is invalid");

            storage.UpdateData(newData.Hash, ((BInt)cas)?.Value, newData);

            return new BDict(new Dictionary<string, BEncode>() {
                { "id", routingTable.Contact }
            });
        }

        public void Consider(IPEndPoint endpoint)
        {
            foreach (var routingTable in routingTables)
                if (routingTable.Socket.CanSendTo(endpoint))
                    routingTable.Consider(endpoint, context);
        }

        public void Consider(BigInt nodeID, IPEndPoint endpoint, BigInt specialInterestHash, InterestFlags interestFlags, ConsiderationReason reason, Context context)
        {
            foreach (var routingTable in routingTables)
                if (routingTable.Socket.CanSendTo(endpoint))
                    routingTable.Consider(nodeID, endpoint, specialInterestHash, interestFlags, reason, context);
        }

        private Node[] FindIPv4NodesLocally(BigInt hash, int maxElements)
        {
            var nodes = routingTables
                .Where(routingTable => routingTable.Socket.LocalEndpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .SelectMany(routingTable => routingTable.FindNodesLocally(hash, maxElements));
            return RoutingTable.OrderByDistance(nodes, hash).Take(maxElements).ToArray();
        }

        private Node[] FindIPv6NodesLocally(BigInt hash, int maxElements)
        {
            var nodes = routingTables
                .Where(routingTable => routingTable.Socket.LocalEndpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                .SelectMany(routingTable => routingTable.FindNodesLocally(hash, maxElements));
            return RoutingTable.OrderByDistance(nodes, hash).Take(maxElements).ToArray();
        }

        /// <summary>
        /// Adds the special-interest-hash to all routing tables.
        /// </summary>
        private void AddInterest(BigInt hash, InterestFlags interestFlags)
        {
            context.Log.Debug("adding interest in hash {0}", hash);
            foreach (var routingTable in routingTables)
                routingTable.AddInterest(hash, interestFlags, context);
        }

        /// <summary>
        /// Renews the special-interest-hash in all routing tables periodically.
        /// </summary>
        private void AddPeriodicInterest(BigInt hash, InterestFlags interestFlags, CancellationToken cancellationToken)
        {
            int delay = STORAGE_MIN_REFRESH / 3;

            new CancelableThread(() => {
                do {
                    AddInterest(hash, interestFlags);
                    delay = Math.Min(3 * delay, STORAGE_MAX_REFRESH);
                } while (!cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(delay)));
            }).Start();
        }

        /// <summary>
        /// Returns a dynamic set of peers for the specified info-hash.
        /// As peers are found, they are added to the set.
        /// Calling this method (re)initiates a lookup for peers.
        /// Unresponsive peers are not automatically removed from this set.
        /// </summary>
        public DynamicSet<IPEndPoint> GetPeers(BigInt hash)
        {
            var list = storage.GetPeerList(this, hash, true);
            AddInterest(hash, InterestFlags.Peers);
            return list;
        }

        /// <summary>
        /// Initiates the lookup of an immutable data item.
        /// The callback is normally only called once (when the data is found).
        /// The interval of refresh lookups is gradually increased to a maximum of 30min.
        /// </summary>
        internal void GetValue(BigInt hash, Action<DHTData> callback, CancellationToken cancellationToken)
        {
            var refreshToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationToken = refreshToken.Token;

            var data = storage.GetOrGenerateData(hash);
            data.DataChangedCallback += () => {
                refreshToken.Cancel(); // we can cancel the periodic lookup as soon as the immutable data is found
                callback(data);
            };

            AddPeriodicInterest(data.Hash, InterestFlags.Data, cancellationToken);
        }

        /// <summary>
        /// Subscribes to updates of a mutable data item.
        /// The callback may be called multiple times.
        /// The interval of refresh lookups is gradually increased to a maximum of 30min.
        /// 
        /// If this method was not called before, the returned data item will most likely have no associated data.
        /// </summary>
        internal DHTData GetValue(byte[] publicKey, byte[] salt,  Action<DHTData> callback, CancellationToken cancellationToken)
        {
            var data = storage.GetOrGenerateData(publicKey, salt);
            data.DataChangedCallback += () => {
                callback(data);
            };

            AddPeriodicInterest(data.Hash, InterestFlags.Data, cancellationToken);

            return data;
        }
    }
}
