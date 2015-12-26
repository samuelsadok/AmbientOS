using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using AmbientOS.Net.KRPC;

namespace AmbientOS.Net.DHT
{
    /// <summary>
    /// Indicates a reason why a node should be considered.
    /// </summary>
    public enum ConsiderationReason
    {
        /// <summary>
        /// Heard about the node from some other node, but haven't heard from it directly.
        /// This is the weakest consideration reason.
        /// </summary>
        Rumor,

        /// <summary>
        /// Received a query from the node.
        /// </summary>
        DidTalk,

        /// <summary>
        /// The node responded to one of our queries.
        /// </summary>
        DidRespond,

        /// <summary>
        /// The node responded and returned a token for the hash under consideration.
        /// This means that the node is suitable for storage of the according value.
        /// This is the strongest consideration reason.
        /// </summary>
        DidCooperate,

        /// <summary>
        /// The node was already known, but should be inquired once again.
        /// </summary>
        Refresh
    }

    /// <summary>
    /// Indicates reasons why the local node is interested in a specific hash.
    /// </summary>
    public enum InterestFlags
    {
        /// <summary>
        /// The local node is not really interested in this hash but stores it because it was told to do so by another node.
        /// </summary>
        NoInterest = 0,

        /// <summary>
        /// The hash is the ID of a locally operated node, so we're by design interested in close nodes.
        /// </summary>
        LocalID = 1,

        /// <summary>
        /// The local node is interested in peers of this info-hash.
        /// </summary>
        Peers = 2,

        /// <summary>
        /// The local node is interested in data of this hash.
        /// </summary>
        Data = 4,
    }


    /// <summary>
    /// Represents a DHT rounting table.
    /// </summary>
    class RoutingTable
    {

        /// <summary>
        /// The number of threads that query newly found nodes for interesting hashes.
        /// Interesting hashes include the local node ID and hashes for which we're looking for peers.
        /// More threads may help filling the routing table and finding peers more quickly.
        /// </summary>
        private const int SEARCH_THREADS = 64;

        /// <summary>
        /// Number of threads that refresh outdated buckets.
        /// A refresh may take a few seconds, so for 100+ buckets, multiple threads should be used.
        /// </summary>
        private const int REFRESH_THREADS = 16;

        /// <summary>
        /// Interval in which routing table statistics are logged.
        /// </summary>
        private static readonly TimeSpan STATS_INTERVAL = TimeSpan.FromMilliseconds(5000);

        internal readonly DHT dht;

        /// <summary>
        /// A list of the buckets that make up this routing table.
        /// This list is ordered by the range of the buckets.
        /// </summary>
        private readonly List<Bucket> buckets = new List<Bucket>();

        /// <summary>
        /// Bucket:
        /// In addition to the buckets drescribed in the specification, we keep an additional bucket
        /// for every special interest hash. Special interest hashes include our local ID and hashes for which
        /// a lookup is running.
        /// 
        /// Queue:
        /// If a node that should be considered cannot be placed in any relevant bucket,
        /// this may be just because they are filled with yet unverified nodes.
        /// However, if the node is particularily close to a hash of special interest, it should still get
        /// the chance to be queried (and possibly elevated to evict other bad nodes from a bucket).
        /// For this reason, it may be stored in one of these queues.
        /// </summary>
        private readonly Dictionary<BigInt, Tuple<Bucket, DynamicQueue<Node>, InterestFlags>> specialInterestStores = new Dictionary<BigInt, Tuple<Bucket, DynamicQueue<Node>, InterestFlags>>();

        /// <summary>
        /// A queue of nodes that should be considered by the search threads.
        /// By the time a node in this queue is dequeued and queried, the search thread
        /// checks if the node is still relevant. This is the case if either it's in one of the routing table buckets,
        /// one of the special-intrest-buckets or in hash-specific queue.
        /// A node that is still relevant is queried for close nodes to each of the interesting hashes.
        /// For hashes that have an associated DynamicSet of peers, there are also get_peers and announce_peer requests to the node.
        /// </summary>
        private readonly DynamicQueue<Node> nodesToBeInquired = new DynamicQueue<Node>();

        /// <summary>
        /// Stores the IDs of nodes that were rejected recently because they didn't respond or cooperate.
        /// If they appear again (e.g. as a result of a find_nodes query), they are rejected immediately.
        /// This prevents bad nodes from pointlessly being inquired many times.
        /// </summary>
        private readonly DynamicQueue<BigInt> rejectedNodes = new DynamicQueue<BigInt>(Bucket.BUCKET_SIZE * 4);

        //  /// <summary>
        //  /// A set of hashes that are interesting in that new nodes should be queried about nodes close to that hash.
        //  /// The queue holds a few nodes that did not fit in the routing table.
        //  /// The set tracks the peers in the set (it is null if we're not interested in peers of this hash).
        //  /// </summary>
        //  internal readonly Dictionary<Hash, DynamicSet<IPEndPoint>> interestingHashes = new Dictionary<Hash, Tuple<FixedSizeQueue<Node>, DynamicSet<IPEndPoint>>>();


        /// <summary>
        /// The ID of the local DHT node represented by this routing table.
        /// </summary>
        public BigInt LocalID { get; private set; }
        internal BString Contact { get { return DHTUtils.SerializeHash(LocalID); } }

        public SingleEndpointSocket Socket { get; }

        private static int CompareDistances(BigInt hash, Node node1, Node node2)
        {
            // distances to nodes with no ID are considered to be very small, as such nodes represent actual peers
            if (node1.ID == null)
                if (node2.ID == null)
                    return 0;
                else
                    return -1;
            else if (node2.ID == null)
                return 1;

            if (hash.GetDistance(node1.ID) > hash.GetDistance(node2.ID))
                return 1;
            else if (hash.GetDistance(node1.ID) < hash.GetDistance(node2.ID))
                return -1;
            return 0;
        }

        public RoutingTable(DHT dht, SingleEndpointSocket socket)
        {
            this.dht = dht;
            Socket = socket;
            buckets.Add(new Bucket(this, BigInt.Zero, BigInt.MaxValue160 + 1, null));
        }

        //private void LogDHT(string str, int verbosity)
        //{
        //    Logging.LogDHT(Socket.LocalEndpoint, str, verbosity);
        //}

        /// <summary>
        /// Validates the consistency of the bucket list.
        /// Throws an exception if any inconsistency is found.
        /// </summary>
        public void ValidateConsistency()
        {
            lock (buckets) {
                var currentHash = buckets[0].MinValue;
                if (currentHash != BigInt.Zero)
                    throw new Exception("the bucket list doesn't cover the entire hash space");

                for (int bucket = 0; bucket < buckets.Count(); bucket++) {
                    if (buckets[bucket].MinValue != currentHash)
                        throw new Exception("bucket list not contiguous");
                    if (buckets[bucket].MinValue > buckets[bucket].MaxValue)
                        throw new Exception("there is a bucket with a negative range");
                    if (buckets[bucket].MinValue + Bucket.BUCKET_SIZE > buckets[bucket].MaxValue)
                        throw new Exception("there is a bucket with a range smaller than " + Bucket.BUCKET_SIZE);

                    foreach (var node in buckets[bucket].Nodes.Where(node => node != null))
                        if (!buckets[bucket].InRange(node.ID))
                            throw new Exception("a node was found in the wrong bucket");

                    currentHash = buckets[bucket].MaxValue;
                }

                if (currentHash != BigInt.MaxValue160 + 1)
                    throw new Exception("the bucket list doesn't cover the entire hash space");
            }
        }

        /// <summary>
        /// Starts the maintainer threads of the routing table.
        /// </summary>
        public void Start(Context context)
        {
            context.Log.Debug("starting routing table");

            Action<IPEndPoint, IPEndPoint> updateLocalID = (localEndpoint, publicEndpoint) => {
                // todo: implement BEP45, create and dispose IP-address-specific routing tables on demand and share some information accross the tables.
                // also, on ID change we might want to store and restore routing tables from disk

                var oldID = LocalID;

                if (publicEndpoint == null && LocalID != null)
                    return;

                if (publicEndpoint == null)
                    LocalID = BigInt.FromRandom(160);
                else if (publicEndpoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    LocalID = DHTUtils.ComputeCompliantID(publicEndpoint.Address); // todo: make ID access thread-safe
                else // ignore IPv6 for now
                    return; // todo: use separate ID for IPv6 and update it here (do this along with BEP45)

                if (oldID != null)
                    RemoveInterest(oldID);
                AddInterest(LocalID, InterestFlags.LocalID, context);
                context.Log.Log(string.Format("local node ID updated: {0}", LocalID), LogType.Info);
            };

            // init local ID
            Socket.IPEndpointChanged += updateLocalID;
            updateLocalID(Socket.LocalEndpoint, Socket.PublicEndpoint);


            // start info thread (this thread logs routing table statistics every now and then)
            new CancelableThread(() => {
                while (true) {
                    context.Controller.Wait(STATS_INTERVAL);
                    bool?[] nodeStats;
                    int bucketCount;
                    int hashCount;
                    lock (buckets) {
                        bucketCount = buckets.Count();
                        nodeStats = buckets.SelectMany(bucket => bucket.Nodes.Where(node => node != null).Select(node => node.IsGood ? true : node.IsBad ? false : (bool?)null)).ToArray();
                    }
                    lock (specialInterestStores) {
                        hashCount = specialInterestStores.Count();
                    }
                    int good = nodeStats.Sum(node => node.HasValue ? node.Value ? 1 : 0 : 0);
                    int bad = nodeStats.Sum(node => node.HasValue ? node.Value ? 0 : 1 : 0);
                    int questionnable = nodeStats.Sum(node => node.HasValue ? node.Value ? 0 : 0 : 1);
                    if (good + bad + questionnable > 0)
                        context.Log.Log(string.Format("buckets: {0}, good: {1}, questionnable: {2}, bad: {3}, in queue: {4}, threads: {5}, hashes: {6}", bucketCount, good, questionnable, bad, nodesToBeInquired.Count(), System.Diagnostics.Process.GetCurrentProcess().Threads.Count, hashCount), LogType.Info);
                    else
                        context.Log.Log("routing table is empty", LogType.Info);
                }
            }).Start();

            // start the search threads
            for (int i = 0; i < SEARCH_THREADS; i++) {
                var threadID = i;
                new CancelableThread(() => {
                    while (true) {
                        var freshNode = nodesToBeInquired.Dequeue(context.Controller);
                        foreach (var specialInterest in CheckRelevance(freshNode)) {
                            context.Log.Debug("maintainer thread {0}: find_node on {1}", threadID, freshNode.ToString());
                            var nodes = Enumerable.Empty<Tuple<BigInt, IPEndPoint>>();
                            bool success = false;
                            bool didCooperate = true;
                            try {
                                success = freshNode.FindNodes(specialInterest.Item1, ref nodes, true, true, context);

                                if (success && specialInterest.Item4) {
                                    dht.storage.Inquire(specialInterest.Item1, () => {
                                        context.Log.Debug("peer exchange with " + freshNode);

                                        var peers = freshNode.GetPeers(specialInterest.Item1, ref nodes, true, true, context).ToArray();
                                        context.Log.Debug("maintainer thread {0}: get_peers on {1} returned {2} peers", threadID, freshNode.ToString(), peers.Count());
                                        //var peerList = dht.GetPeerList(searchJob.Key, freshNode.Endpoint.AddressFamily, true);

                                        var announceError = freshNode.AnnouncePeer(specialInterest.Item1, context);
                                        if (announceError != null)
                                            context.Log.Log(string.Format("announce_peer failed on {0}: {1}", freshNode, announceError), LogType.Warning);

                                        return peers;
                                    }, (data) => {
                                        string message;
                                        if (!freshNode.Sync(data, ref nodes, out message, context))
                                            didCooperate = false;
                                        context.Log.Debug("data exchange with {0} ({1}): {2}", freshNode, didCooperate ? "have token" : "no token", message);
                                    });
                                }

                                nodes = nodes.ToArray();
                                if (nodes.Contains(null))
                                    throw new Exception();
                            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                                context.Log.Log(string.Format("find_node failed on {0}: {1}", freshNode, ex.Message), LogType.Warning);
                                success = false;
                                continue;
                            }

                            if (!success && freshNode.IsBad) // this node is not even bad, it's just plain stupid, so we'll remove it immediately
                                RemoveNode(freshNode.ID, specialInterest.Item2);

                            if (!didCooperate)
                                rejectedNodes.StrongEnqueueDistinct(freshNode.ID);

                            foreach (var reportedNode in nodes) {
                                //dht.Consider(reportedNode.Item1, reportedNode.Item2, specialInterest.Item1, ConsiderationReason.Rumor, cancellationToken);
                                dht.Consider(reportedNode.Item1, reportedNode.Item2, null, InterestFlags.NoInterest, ConsiderationReason.Rumor, context);
                                //Consider(reportedNode.Item1, reportedNode.Item2, specialInterest.Item1, specialInterest.Item2, specialInterest.Item3, ConsiderationReason.Rumor, cancellationToken);
                            }
                        }
                    }
                }).Start();
            }


            DynamicQueue<Bucket> refreshingBuckets = new DynamicQueue<Bucket>();

            // start bucket refresh coordinator thread
            new CancelableThread(() => {
                TimeSpan nextInterval;
                while (true) {
                    nextInterval = Bucket.REFRESH_RATE;
                    lock (buckets) {
                        for (int i = 0; i < buckets.Count(); i++) {
                            var refreshTime = buckets[i].StartRefresh();
                            if (refreshTime <= TimeSpan.Zero)
                                refreshingBuckets.StrongEnqueue(buckets[i]);
                            else if (refreshTime < nextInterval)
                                nextInterval = refreshTime;
                        }
                    }
                    context.Controller.Wait(nextInterval);
                }
            }).Start();

            // start bucket refresh worker threads
            for (int i = 0; i < REFRESH_THREADS; i++) {
                var threadID = i;
                new CancelableThread(() => {
                    while (true) {
                        var bucket = refreshingBuckets.Dequeue(context.Controller);
                        var refresh = bucket.ReevaluateRefresh();
                        if (refresh.HasValue) {
                            if (refresh.Value) {
                                context.Log.Debug("refresh thread {0}: refreshing {1}, in queue: {2}", threadID, bucket.ToString(), refreshingBuckets.Count());
                                var hash = BigInt.FromRandom(bucket.MinValue, bucket.MaxValue);
                                AddInterest(hash, InterestFlags.NoInterest, context); // add interest with NoInterest, to inquire close nodes in a one-time shot
                                bucket.EndRefresh(true);
                            } else {
                                bucket.EndRefresh(false);
                            }
                        }
                    }
                }).Start();
            }
        }

        /// <summary>
        /// Sends a ping to the specified endpoint to determine if it's a valid node.
        /// If it responds, the corresponding node may be stored and enqueued for further inquiry later on.
        /// </summary>
        /// <param name="endpoint">An endpoint that the underlying socket can handle.</param>
        public void Consider(IPEndPoint endpoint, Context context)
        {
            // create a node without ID and ping it.
            // the ID is set automatically in the Ping call
            // also, the Ping call honours the response by calling Consider for the new node.
            var node = new Node(this, null, endpoint);
            BDict response = node.Ping(context);
        }

        public void Consider(BigInt nodeID, IPEndPoint endpoint, BigInt specialInterestHash, InterestFlags interestFlags, ConsiderationReason reason, Context context)
        {
            BigInt[] hashes;

            if (specialInterestHash == null) {
                lock (specialInterestStores) {
                    hashes = specialInterestStores.Select(store => store.Key).ToArray();
                }
            } else {
                hashes = new BigInt[] { specialInterestHash };
            }

            foreach (var hash in hashes) {
                Tuple<Bucket, DynamicQueue<Node>, InterestFlags> specialInterestStore;
                lock (specialInterestStores) {
                    if (!specialInterestStores.TryGetValue(hash, out specialInterestStore)) {
                        specialInterestStore = new Tuple<Bucket, DynamicQueue<Node>, InterestFlags>(new Bucket(this, BigInt.Zero, BigInt.MaxValue160, hash), new DynamicQueue<Node>(Bucket.BUCKET_SIZE), interestFlags);
                        if (interestFlags != InterestFlags.NoInterest)
                            specialInterestStores[hash] = specialInterestStore;
                    } else if ((interestFlags & ~specialInterestStore.Item3) != 0) {
                        specialInterestStores[hash] = specialInterestStore = new Tuple<Bucket, DynamicQueue<Node>, InterestFlags>(specialInterestStore.Item1, specialInterestStore.Item2, specialInterestStore.Item3 | interestFlags);
                    }
                }

                Consider(nodeID, endpoint, hash, specialInterestStore.Item1, specialInterestStore.Item2, specialInterestStore.Item3, reason, context);
            }
        }

        /// <summary>
        /// Puts the specified node into an appropriate node store.
        /// The exact semantics depend on the reason why the node should be considered.
        /// If the node is already known, only it's activity times are updated (if at all).
        /// If there is no space and the node cannot evict any other node, the node is ignored.
        /// If the node is newly added, it may be added to the queue of nodes to be inquired.
        /// </summary>
        public void Consider(BigInt nodeID, IPEndPoint endpoint, BigInt specialInterestHash, Bucket specialInterestBucket, DynamicQueue<Node> specialInterestQueue, InterestFlags interestFlags, ConsiderationReason reason, Context context)
        {
            if (!nodeID.IsIDCompliant(endpoint.Address)) {
                //LogDHT("found non-compliant node", 2);
                return;
            }

            if (rejectedNodes.Contains(nodeID))
                return;

            Node node = null;
            bool newlyAdded = false;
            string addLocation = null; // for debugging only

            // place node in one of the contiguous buckets and split if appropriate
            if (reason != ConsiderationReason.Refresh) {
                int bucketIndex = -1;
                Bucket bucket;
                while (true) {
                    lock (buckets) {
                        var copy = new Bucket[buckets.Count()];
                        do { // this scan loop allows us to quickly find the right bucket without locking on each bucket
                            if (++bucketIndex >= buckets.Count())
                                throw new Exception("No bucket was found that is responsible for the node with ID " + nodeID + ". This was not supposed to happen.");
                        } while (!(copy[bucketIndex] = buckets[bucketIndex]).InRange(nodeID));
                        bucket = buckets[bucketIndex];
                    }

                    lock (bucket) { // this lock takes potentially long
                                    // until locking, the bucket could already have been splitted, so we need to repeat the check
                        lock (buckets) {
                            if (!bucket.InRange(nodeID))
                                continue;
                        }

                        newlyAdded |= bucket.AddNode(ref node, nodeID, endpoint, reason, context);
                        if (node != null) {
                            addLocation = "routing table";
                            break;
                        }

                        if (bucket.InRange(LocalID)) {
                            lock (buckets) {
                                var newBucket = bucket.Split();
                                bucketIndex = buckets.IndexOf(bucket); // the bucket may have moved in the list
                                buckets.Insert(bucketIndex + 1, newBucket);
                                //newBucket.AddedNode += addedNode => nodesToBeConsidered.Enqueue(addedNode, cancellationToken);
                                bucketIndex--; // we'll have to reconsider this bucket
                            }
                        } else {
                            break; // don't split bucket if own ID is not in its range
                        }
                    }
                }
            }


            if (reason == ConsiderationReason.DidCooperate || (reason == ConsiderationReason.DidRespond && interestFlags.HasFlag(InterestFlags.LocalID))) { // place node in the according special-interest-bucket
                //node = node ?? new Node(this, nodeID, endpoint);
                bool addedToBucket;
                lock (specialInterestBucket) {
                    addedToBucket = specialInterestBucket.AddNode(ref node, nodeID, endpoint, reason, context);
                }
                if (addedToBucket)
                    addLocation = "specific bucket";
                newlyAdded |= addedToBucket;
            } else { // place unverified node in hash-specific queue if there's space for it in the bucket
                // (the queue only makes sense for nodes that didn't just respond)

                BigInt farthestDistance, currentDistance = specialInterestHash.GetDistance(nodeID);
                BigInt[] existingHashes;
                bool shouldAddToQueue = false;

                lock (specialInterestBucket) {
                    if (specialInterestBucket.Center != specialInterestHash)
                        throw new Exception("wrong center");

                    farthestDistance = specialInterestBucket.FarthestDistance;
                    existingHashes = (from n in specialInterestBucket.Nodes where n?.ID != null select n.ID).ToArray();
                    shouldAddToQueue = farthestDistance == null || currentDistance < farthestDistance;
                    if (shouldAddToQueue)
                        shouldAddToQueue = !specialInterestBucket.Nodes.Any(n => n?.ID == nodeID);
                }

                if (shouldAddToQueue) {
                    node = node ?? new Node(this, nodeID, endpoint);
                    var addedToQueue = specialInterestQueue.ReplaceLarger(node, (newNode, oldNode) => CompareDistances(specialInterestHash, newNode, oldNode));
                    if (addedToQueue)
                        addLocation = "specific queue, improving distance from " + farthestDistance + " to " + currentDistance + ", bucket (" + existingHashes.Count() + ") has " + string.Join(", ", existingHashes.Select(h => h.ToString()));
                    newlyAdded |= addedToQueue;
                }
            }

            if (node != null) {
                node.UpdateActivityTime(reason);

                // if the node was newly added to any of the places where we store nodes, enqueue it for further inquiry.
                if (newlyAdded) {
                    if (nodesToBeInquired.StrongEnqueueDistinct(node))
                        context.Log.Debug("newly added {0} to {1}", node, addLocation);
                }
            }
        }


        /// <summary>
        /// Checks the relevance of the specified node ID against all of the special-interest-hashes at the instant of calling.
        /// For each special-interest-hash for which the node is relevant, the following items are returned:
        /// 
        /// - The respective special-interest-hash
        /// - A boolean that indicates whether the specified node ID is close to the special-interest-hash
        /// - The bucket for nodes close to this special-interest-hash
        /// - The queue for nodes close to this special-interest-hash
        /// 
        /// This method and the lazy evaluation of the result are thread-safe.
        /// </summary>
        private IEnumerable<Tuple<BigInt, Bucket, DynamicQueue<Node>, bool>> CheckRelevance(Node node)
        {
            bool inRoutingTable;
            lock (buckets) {
                inRoutingTable = buckets.Any(bucket => bucket.Nodes.Any(nodeInBucket => nodeInBucket?.ID == node.ID));
            }

            KeyValuePair<BigInt, Tuple<Bucket, DynamicQueue<Node>, InterestFlags>>[] specialInterestStores;
            lock (this.specialInterestStores) {
                specialInterestStores = this.specialInterestStores.ToArray();
            }

            var result = new Tuple<BigInt, Bucket, DynamicQueue<Node>, bool>[specialInterestStores.Count()];

            for (int i = 0; i < specialInterestStores.Count(); i++) {
                bool isClose;
                lock (specialInterestStores[i].Value.Item2) {
                    isClose = specialInterestStores[i].Value.Item2.Dequeue(node);
                }
                lock (specialInterestStores[i].Value.Item1) {
                    if (!isClose)
                        isClose = specialInterestStores[i].Value.Item1.Nodes.Any(nodeInBucket => nodeInBucket?.ID == node.ID);
                }
                if (inRoutingTable || isClose)
                    result[i] = new Tuple<BigInt, Bucket, DynamicQueue<Node>, bool>(
                        specialInterestStores[i].Key,
                        specialInterestStores[i].Value.Item1,
                        specialInterestStores[i].Value.Item2,
                        isClose
                        );
                else
                    result[i] = null;
            }

            // we make the check at the end because even if the node was rejected there may be queues from which we have to remove it
            if (rejectedNodes.Contains(node.ID))
                return Enumerable.Empty<Tuple<BigInt, Bucket, DynamicQueue<Node>, bool>>();
            return result.Where(x => x != null);
        }


        public void RemoveNode(BigInt nodeID, Bucket specialInterestBucket)
        {
            lock (specialInterestBucket) {
                specialInterestBucket.RemoveNode(nodeID);
            }
            for (int bucket = 0; bucket < buckets.Count(); bucket++) {
                lock (buckets[bucket]) {
                    buckets[bucket].RemoveNode(nodeID);
                }
            }
        }

        public static IEnumerable<Node> OrderByDistance(IEnumerable<Node> nodes, BigInt hash)
        {
            return nodes.Where(node => node != null).OrderBy(node => hash.GetDistance(node.ID), Comparer<BigInt>.Create((d1, d2) => d1 < d2 ? -1 : d1 == d2 ? 0 : 1));
        }

        /// <summary>
        /// Returns all of the good nodes in the routing table, ordered by the closeness to the specified hash.
        /// </summary>
        public Node[] FindNodesLocally(BigInt hash, int maxElements)
        {
            // todo: clean up
            Node[] store1;
            IEnumerable<Node> store2a = Enumerable.Empty<Node>();

            lock (buckets) {
                store1 = OrderByDistance(buckets.SelectMany(bucket => bucket.Nodes), hash).Take(maxElements).ToArray();
            }

            KeyValuePair<BigInt, Tuple<Bucket, DynamicQueue<Node>, InterestFlags>>[] stores;
            lock (specialInterestStores) {
                stores = specialInterestStores.ToArray();
            }

            foreach (var store in stores) {
                lock (store.Value.Item1) {
                    var storeCopy = new Node[store.Value.Item1.Nodes.Count()];
                    Array.Copy(store.Value.Item1.Nodes, storeCopy, storeCopy.Count());
                    store2a = store2a.Concat(storeCopy);
                }
            }

            return OrderByDistance(store1.Concat(store2a), hash).Take(maxElements).ToArray();
        }

        /*
        /// <summary>
        /// Asks in each iteration for the closest node that other nodes know.
        /// In each iteration, up to K (=8) nodes are queried.
        /// </summary>
        public Tuple<IEnumerable<Node>, IEnumerable<IPEndPoint>> FindNodesAndPeersGlobally(Hash id, bool includePeers, bool ipv6, CancellationToken cancellationToken)
        {
            var list = FindNodesLocally(id, ipv6, 8).ToArray();
            var allNodes = list.AsEnumerable();
            var allPeers = Enumerable.Empty<IPEndPoint>();

            while (list.Any()) {
                var newList = Enumerable.Empty<Node>();
                foreach (var item in list) {
                    try {
                        var newNodes = Enumerable.Empty<Node>();
                        item.FindNodes(id, ref newNodes, !ipv6, ipv6, cancellationToken);
                        if (includePeers)
                            allPeers = allPeers.Concat(item.GetPeers(id, ref newNodes, !ipv6, ipv6, cancellationToken));
                        newNodes = newNodes.ToArray();
                        newNodes = newNodes.Where(newNode => !list.Any(oldNode => id.GetDistance(oldNode.ID) <= id.GetDistance(newNode.ID))).ToArray();
                        newList = newList.Concat(newNodes);
                    } catch (OperationCanceledException) {
                        throw;
                    } catch (Exception) {
                        LogDHT("find_node failed on " + item, 1);
                    }
                }

                // keep only the nodes that are closer than any previous nodes
                newList = newList.Distinct();
                allNodes = allNodes.Concat(newList);

                // order the remaining new nodes by ascending distance
                list = OrderByDistance(newList, id).Take(8).ToArray();
            }

            return new Tuple<IEnumerable<Node>, IEnumerable<IPEndPoint>>(OrderByDistance(allNodes, id), allPeers);
        }*/

        /// <summary>
        /// Adds the specified hash to the set of special interest hashes and re-considers the specified nodes.
        /// </summary>
        /// <param name="interestFlags">If set to no interest, and there is not already interest in the hash, the interest is not added permanently, but close nodes are still inquired.</param>
        public void AddInterest(BigInt hash, InterestFlags interestFlags, Context context)
        {
            Tuple<Bucket, DynamicQueue<Node>, InterestFlags> specialInterestStore;
            lock (specialInterestStores) {
                if (!specialInterestStores.TryGetValue(hash, out specialInterestStore)) {
                    specialInterestStore = new Tuple<Bucket, DynamicQueue<Node>, InterestFlags>(new Bucket(this, BigInt.Zero, BigInt.MaxValue160, hash), new DynamicQueue<Node>(Bucket.BUCKET_SIZE), interestFlags);
                    if (interestFlags != InterestFlags.NoInterest)
                        specialInterestStores[hash] = specialInterestStore;
                } else if ((interestFlags & ~specialInterestStore.Item3) != 0) {
                    specialInterestStores[hash] = specialInterestStore = new Tuple<Bucket, DynamicQueue<Node>, InterestFlags>(specialInterestStore.Item1, specialInterestStore.Item2, specialInterestStore.Item3 | interestFlags);
                }
            }

            foreach (var node in FindNodesLocally(hash, 8))
                Consider(node.ID, node.Endpoint, hash, specialInterestStore.Item1, specialInterestStore.Item2, specialInterestStore.Item3, ConsiderationReason.Refresh, context);
        }

        /// <summary>
        /// Removes the specified hash from the set of special interest hashes.
        /// After this, nodes are no longer inquired about this hash.
        /// </summary>
        public void RemoveInterest(BigInt hash)
        {
            lock (specialInterestStores) {
                specialInterestStores.Remove(hash);
            }
        }

        public string Dump()
        {
            StringBuilder dump = new StringBuilder();
            lock (buckets) {
                foreach (var bucket in buckets)
                    bucket.Dump(dump);
            }
            return dump.ToString();
        }
    }
}
