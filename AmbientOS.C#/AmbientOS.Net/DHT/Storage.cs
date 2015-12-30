using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace AmbientOS.Net.DHT
{
    /// <summary>
    /// Represents the DHT storage of the local node.
    /// </summary>
    class Storage
    {
        /// <summary>
        /// A set of peers for a few hashes.
        /// This set contains endpoints for both IPv4 and IPv6 stacks.
        /// 
        /// todo: limit number of peers we store
        /// </summary>
        private readonly Dictionary<BigInt, DynamicSet<IPEndPoint>> peers = new Dictionary<BigInt, DynamicSet<IPEndPoint>>();

        /// <summary>
        /// Stores mutable and immutable data for a few hashes according to BEP44.
        /// </summary>
        private readonly Dictionary<BigInt, DHTData> data = new Dictionary<BigInt, DHTData>();


        public DynamicSet<IPEndPoint> GetPeerList(DHT dht, BigInt hash, bool persist)
        {
            DynamicSet<IPEndPoint> list;

            lock (peers) {
                if (!peers.TryGetValue(hash, out list)) {
                    list = new DynamicSet<IPEndPoint>().Retain();
                    if (persist) {
                        var controller = list.GetLifecycleController();

                        var subscriber = new DynamicSet<IPEndPoint>((item, moreToFollow) => {
                            dht.Consider(item);
                        }, null, controller);

                        controller.OnResume(() => {
                            list.Subscribe(subscriber);
                        });

                        // todo: on pause unsubscribe

                        peers[hash] = list;
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Returns the specified data field or null.
        /// </summary>
        public DHTData GetData(BigInt hash)
        {
            lock (data) {
                DHTData value;
                if (data.TryGetValue(hash, out value))
                    return value;
                return null;
            }
        }

        /// <summary>
        /// Returns the data field with the specified hash.
        /// If the hash is not known, a new empty immutable item is created.
        /// </summary>
        public DHTData GetOrGenerateData(BigInt hash)
        {
            lock (data) {
                DHTData value;
                if (!data.TryGetValue(hash, out value))
                    data[hash] = value = new DHTData(hash);
                return value;
            }
        }

        /// <summary>
        /// Returns the data field with the specified public key and salt.
        /// If the hash is not known, a new empty mutable item is created.
        /// </summary>
        public DHTData GetOrGenerateData(byte[] publicKey, byte[] salt)
        {
            var hash = DHTData.ComputeHash(publicKey, salt);
            lock (data) {
                DHTData value;
                if (!data.TryGetValue(hash, out value))
                    data[hash] = value = new DHTData(publicKey, salt);
                return value;
            }
        }

        /// <summary>
        /// Updates the value with the specified hash.
        /// An exception is thrown if the new value is rejected.
        /// </summary>
        /// <param name="expectedSeqNo">If non-null, the existing value (if any) is checked against this value. The new value is then rejected on mismatch.</param>
        public void UpdateData(BigInt hash, long? expectedSeqNo, DHTData newValue)
        {
            DHTData existingData;

            lock (data) {
                if (!data.TryGetValue(hash, out existingData)) {
                    data[hash] = newValue;
                    return;
                }
            }

            if (expectedSeqNo.HasValue && expectedSeqNo != existingData.SequenceNumber)
                throw new Exception("compare-and-set failed");

            existingData.Apply(newValue);
        }

        /// <summary>
        /// This can be called to offer the local storage the possibility to execute peers or data inquiry for the specified hash.
        /// </summary>
        /// <param name="hash">The hash for which an inquiry was offered</param>
        /// <param name="inquirePeers">This function is invoked if the local host is interested in peers for this hash. It should return any peers that are found.</param>
        /// <param name="inquireData">This action is invoked if the local host is interested in data for this hash. It should apply the newly found data to the provided data object.</param>
        public void Inquire(BigInt hash, Func<IPEndPoint[]> inquirePeers, Action<DHTData> inquireData)
        {
            bool interestInPeers;
            DynamicSet<IPEndPoint> relevantPeerSet;

            lock (peers) {
                interestInPeers = peers.TryGetValue(hash, out relevantPeerSet);
            }
                    
            if (interestInPeers) {
                var newPeers = inquirePeers();
                for (int peer = 0; peer < newPeers.Count(); peer++)
                    relevantPeerSet.Add(newPeers[peer], peer < newPeers.Count() - 1);
            }

            bool interestInData;
            DHTData relevantData;

            lock (data) {
               interestInData = data.TryGetValue(hash, out relevantData);
            }

            if (interestInData) {
                inquireData(relevantData);
            }
        }
    }
}
