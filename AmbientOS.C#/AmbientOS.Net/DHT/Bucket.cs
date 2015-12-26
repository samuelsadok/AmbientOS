using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;

namespace AmbientOS.Net.DHT
{
    class Bucket
    {
        /// <summary>
        /// If there is no change in the bucket for this time span, a random hash in the range is looked up to refresh the nodes.
        /// This value is defined as 15 minutes by the Mainline DHT specification.
        /// </summary>
        internal static TimeSpan REFRESH_RATE = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maximum number of nodes per bucket. This is defined by the Mainline DHT specification.
        /// </summary>
        internal const int BUCKET_SIZE = 8;

        /// <summary>
        /// Number of pings we send to questionnable nodes until we discard them.
        /// The Mainline DHT specification suggests to use 2 pings.
        /// </summary>
        private const int PING_COUNT = 2;

        /// <summary>
        /// This bucket cannot contain nodes with an ID smaller than this value.
        /// </summary>
        public BigInt MinValue { get; }

        /// <summary>
        /// This bucket cannot contain nodes with an ID larger than or equal to this value.
        /// This value decreases when the bucket is splitted.
        /// </summary>
        public BigInt MaxValue { get; private set; }

        /// <summary>
        /// Specifies the center of this bucket.
        /// If the bucket has a center (not null), nodes closer to this center are favored.
        /// </summary>
        public BigInt Center { get; }

        /// <summary>
        /// Keeps track of the farthest distance of any node in the bucket to the center.
        /// Only valid if center not null and the bucket is full.
        /// </summary>
        public BigInt FarthestDistance { get; private set; }

        private int FarthestNodeIndex = 0;

        /// <summary>
        /// Stores the last time a node in this bucket was added or swapped.
        /// </summary>
        public DateTime UpdateTime { get { return new DateTime(Interlocked.Read(ref updateTime)); } private set { Interlocked.Exchange(ref updateTime, value.Ticks); } }
        private long updateTime;

        /// <summary>
        /// Stores the nodes of this bucket.
        /// </summary>
        public Node[] Nodes { get; }

        /// <summary>
        /// Triggered for each node that is added to the routing table.
        /// If a node is transferred to another bucket (at splitting), this event is not triggered again.
        /// </summary>
        //public event Action<Node> AddedNode;

        private int refreshing = 0;

        private readonly RoutingTable routingTable;

        public Bucket(RoutingTable routingTable, BigInt minValue, BigInt maxValue, BigInt center)
        {
            this.routingTable = routingTable;

            MinValue = minValue;
            MaxValue = maxValue;
            Center = center;
            Nodes = new Node[BUCKET_SIZE];
            UpdateTime = DateTime.Now;
        }

        /// <summary>
        /// Checks when the next refresh (any is neccessary.
        /// If a refresh is already due, the method returns a zero or negative timespan. In this case (and only in this case), the caller should call EndRefresh after refreshing.
        /// If a refresh is in progress, the method returns the maximum refresh interval.
        /// </summary>
        internal TimeSpan StartRefresh()
        {
            var remainingTime = (UpdateTime + REFRESH_RATE) - DateTime.Now;

            if (remainingTime <= TimeSpan.Zero)
                if (Interlocked.Exchange(ref refreshing, 1) == 1)
                    return REFRESH_RATE;

            return remainingTime;
        }

        /// <summary>
        /// Checks if a refresh is still required.
        /// The result indicates for both IPv4 and IPv6 stacks, if a refresh is required.
        /// </summary>
        internal bool? ReevaluateRefresh()
        {
            var remainingTime = (UpdateTime + REFRESH_RATE) - DateTime.Now;
            return refreshing == 0 ? (bool?)null : remainingTime <= TimeSpan.Zero;
        }

        internal void EndRefresh(bool updateRefreshTime)
        {
            if (updateRefreshTime)
                UpdateTime = DateTime.Now;
            Interlocked.Exchange(ref refreshing, 0);
        }

        /// <summary>
        /// Checks if the specified node is within the range of this bucket.
        /// </summary>
        public bool InRange(BigInt id)
        {
            return (MinValue <= id) && (id < MaxValue);
        }

        /// <summary>
        /// Splits this bucket into two and returns the new bucket (the one with the larger hash range). The maximum value of this bucket is decreased.
        /// Each node is placed in the correct bucket.
        /// This must not be called during a RenewNode call.
        /// This method is not thread-safe.
        /// </summary>
        public Bucket Split()
        {
            if (MinValue + BUCKET_SIZE >= MaxValue) {
                routingTable.ValidateConsistency();
                throw new Exception("the bucket is to small to be splitted");
            }

            var newBucket = new Bucket(routingTable, (MinValue + MaxValue) >> 1, MaxValue, Center);
            MaxValue = newBucket.MinValue;
            var newBucketSize = 0;
            for (int i = 0; i < BUCKET_SIZE; i++) {
                if (Nodes[i] == null ? false : newBucket.InRange(Nodes[i].ID)) {
                    newBucket.Nodes[newBucketSize] = Nodes[i];
                    Nodes[i] = null;
                    newBucketSize++;
                }
            }
            return newBucket;
        }

        private Node SetNode(int index, Node node)
        {
            var shouldReevaluateFarthestNode = false;
            if (Center != null) {
                var newDistance = Center.GetDistance(node.ID);
                shouldReevaluateFarthestNode = newDistance < FarthestDistance || FarthestNodeIndex == index;
            }

            Nodes[index] = node;

            if (shouldReevaluateFarthestNode) {
                var distances = Nodes.Select((n, i) => new { distance = n?.ID == null ? null : Center.GetDistance(n.ID), index = i }).ToArray();
                var farthestNode = distances
                    .Aggregate(new { distance = BigInt.Zero, index = -1 }, (n1, n2) => n1.distance == null ? n1 : n2.distance == null ? n2 : n1.distance >= n2.distance ? n1 : n2);

                FarthestNodeIndex = farthestNode.index;
                FarthestDistance = farthestNode.distance;
            }

            UpdateTime = DateTime.Now;
            return node;
        }
        
        /// <summary>
        /// Adds or updates the specified node to the bucket, removing old bad nodes if neccessary.
        /// If the node already exists in the bucket, only the endpoint is updated, else it is attempted to add the node.
        /// In this case, if none of the nodes are bad, the questionnable nodes are pinged instead.
        /// Returns true iif the node was newly added (i.e. if the node was already in the bucket or didn't fit, the method returns false).
        /// This method is not thread-safe.
        /// </summary>
        /// <param name="node">If not null, this instance is inserted (except if it's already present). Iif after the call, the node is in the bucket, this argument is set to the instance in the bucket, else it remains unchanged.</param>
        public bool AddNode(ref Node node, BigInt nodeID, IPEndPoint endpoint, ConsiderationReason reason, Context context)
        {
            if (!InRange(nodeID))
                throw new ArgumentException(string.Format("The node with ID {0} is not in the range {1} ... {2}", nodeID.ToString(), MinValue.ToString(), MaxValue.ToString()));

            var onIPv6 = endpoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            
            var existingNode = Nodes.FirstOrDefault(n => n?.ID == nodeID);
            if (existingNode != null) {
                node = existingNode;
                // todo: make thread safe
                node.Endpoint = endpoint;
                return false;
            }

            //if (reason == ConsiderationReason.Refresh) // this should also work
            if (reason == ConsiderationReason.Refresh && Center == null)
                return false;

            var newInstance = node ?? new Node(routingTable, nodeID, endpoint);


            // if the bucket has a center, replacement is simple - we just replace the farthest node or any null node
            if (Center != null) {
                //var obsoleteNode = Nodes
                //    .Select((n, i) => new { distance = n?.ID == null ? null : Center.GetDistance(n.ID), index = i })
                //    .Aggregate(new { distance = Hash.MIN_VALUE_160, index = -1 }, (n1, n2) => n1.distance == null ? n1 : n1.distance >= n2.distance ? n1 : n2);

                if (FarthestDistance == null || Center.GetDistance(nodeID) < FarthestDistance) {
                    SetNode(FarthestNodeIndex, node = newInstance);
                    return true;
                } else {
                    return false;
                }
            }


            var questionnableNodes = new List<int>();

            for (int i = 0; i < Nodes.Count(); i++) {
                if (Nodes[i] == null ? true : (Nodes[i].IsBad && reason != ConsiderationReason.Rumor)) {
                    SetNode(i, node = newInstance);
                    return true;
                } else if (!Nodes[i].IsGood && Center == null) { // if the bucket has a center, every node is considered for eviction, so the list is built later
                    questionnableNodes.Add(i);
                }
            }

            if (Center != null)
                questionnableNodes = Enumerable.Range(0, BUCKET_SIZE - 1).ToList();
            
            if (reason == ConsiderationReason.Rumor && Center != null) { // this should not happen (maybe remove)
                if (newInstance.Ping(PING_COUNT, context))
                    reason = ConsiderationReason.DidRespond;
            }

            if (reason == ConsiderationReason.Rumor)
                return false;

            //if (newInstance.IsGood)
            //    reason = ConsiderationReason.DidRespond;

            while (questionnableNodes.Any()) {
                //// depending on whether the bucket has a center, we select the worst node by either last activity time or distance to center
                int worst;
                //if (Center == null)
                    worst = questionnableNodes.Aggregate(questionnableNodes.First(), (i, j) => (Nodes[i].LastActivity?.Ticks ?? 0) < (Nodes[j].LastActivity?.Ticks ?? 0) ? i : j);
                //else
                var worstNode = Nodes[worst];

                // 
                var evict = false;
                //if (Center != null) {
                //    // if this bucket has a center, we give the new node a chance to elevate it's relevance
                //    //if (reason == ConsiderationReason.DidTalk) {
                //    //    if (newInstance.Ping(PING_COUNT, cancellationToken))
                //    //        reason = ConsiderationReason.DidRespond;
                //    //    else
                //    //        reason = ConsiderationReason.Rumor;
                //    //}
                //    if (worstNode.ID == null ? true : Center.GetDistance(nodeID) < Center.GetDistance(Nodes[worst].ID)) {
                //        evict = true;
                //        //if (reason == ConsiderationReason.DidRespond)
                //        //    evict = true;
                //        //else if (!worstNode.IsGood)
                //        //    evict = !worstNode.Ping(PING_COUNT, cancellationToken);
                //    }
                //} else {
                    evict = !worstNode.Ping(PING_COUNT, context);
                //}

                if (evict) {
                    SetNode(worst, node = newInstance);
                    return true;
                }

                questionnableNodes.Remove(worst);
            }

            return false;
        }

        /// <summary>
        /// This method is not thread-safe.
        /// </summary>
        public void RemoveNode(BigInt id)
        {
            var obsoleteNodes = Nodes
                        .Select((node, i) => new { node = node, i = i })
                        .Where(item => item.node?.ID == id)
                        .Select(item => item.i).ToArray();
            foreach (var obsoleteNode in obsoleteNodes)
                Nodes[obsoleteNode] = null;

            if (obsoleteNodes.Any()) {
                FarthestNodeIndex = obsoleteNodes.First();
                FarthestDistance = null;
            }
        }


        /*
        /// <summary>
        /// Returns the good node that is closest to the specified hash.
        /// Returns null if the bucket contains no good nodes.
        /// </summary>
        public IEnumerable<Node> FindNodes(Hash id)
        {
            return Nodes.Where(node => node.IsGood).OrderBy(node => id.GetDistance(node.ID), Comparer<Hash>.Create( delegate(Hash d1, Hash d2) { return d1 < d2 ?; } ));
        }
        */

        public override string ToString()
        {
            return string.Format("bucket {0} to {1} (last update: {2})", MinValue.ToString(), MaxValue.ToString(), UpdateTime);
        }

        public void Dump(StringBuilder dump)
        {
            dump.AppendLine(ToString());
            dump.AppendLine("nodes:");
            foreach (var node in Nodes.Where(n => n != null))
                dump.AppendLine(string.Format("  {0} ({1})", node.ToString(), node.IsGood ? "good" : node.IsBad ? "bad" : "questionnable"));
        }
    }
}
