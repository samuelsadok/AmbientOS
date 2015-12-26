using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using AmbientOS.Net.KRPC;

namespace AmbientOS.Net.DHT
{
    public class PeerProvider
    {
        private readonly DHT dht;
        private readonly SecurityManager securityManager;

        private readonly HashSet<IPEndPoint> localEndpoints = new HashSet<IPEndPoint>();

        private readonly List<DHTData> dataItems = new List<DHTData>();

        public PeerProvider(DHT dht, MultiEndpointSocket sockets, SecurityManager securityManager)
        {
            this.dht = dht;
            this.securityManager = securityManager;

            foreach (var socket in sockets.Sockets) {
                socket.IPEndpointChanged += (localEndpoint, publicEndpoint) => {
                    localEndpoints.Add(localEndpoint);
                    localEndpoints.Add(publicEndpoint);
                    RefreshLocalEndpoints();
                };
                localEndpoints.Add(socket.LocalEndpoint);
                localEndpoints.Add(socket.PublicEndpoint);
            }
        }

        private void RefreshLocalEndpoints()
        {
            lock (dataItems) {
                foreach (var item in dataItems) {
                    lock (item) {
                        item.Apply(ByteConverter.WriteVal(new PeerList() {
                            version = 1,
                            peers = (from endpoint in localEndpoints where endpoint != null select new PeerElement() {
                                ipAddress = endpoint.Address.GetAddressBytes(),
                                port = (ushort)endpoint.Port,
                                lastSeen = DateTime.Now.Ticks
                            }).ToArray()
                        }), securityManager.DomainExpandedPrivateKey, null);
                    }
                }
            }
        }

        [Endianness(Endianness.NetworkByteOrder)]
        private class PeerList
        {
            public int version;

            [FieldSpecs(LengthOf = "peers")]
            protected int peerCount = 0;

            public PeerElement[] peers;

            public override string ToString()
            {
                return string.Join(", ", peers.Select(p => new IPAddress(p.ipAddress).ToString() + ":" + p.port));
            }
        }

        [Endianness(Endianness.NetworkByteOrder)]
        private class PeerElement
        {
            [FieldSpecs(LengthOf = "ipAddress")]
            protected byte ipAddressLength = 0;
            public byte[] ipAddress;
            public ushort port;
            public long lastSeen;

            public override bool Equals(object obj)
            {
                var obj2 = obj as PeerElement;
                if (obj2 == null)
                    return false;
                return ipAddress.SequenceEqual(obj2.ipAddress) && port == obj2.port;
            }

            public override int GetHashCode()
            {
                return unchecked(ipAddress.Sum(b => b) + port);
            }
        }

        private static IEnumerable<PeerElement> JoinPeerLists(PeerElement[] list1, PeerElement[] list2)
        {
            var exclusive = list1.Except(list2).Concat(list2.Except(list1)).ToArray();

            foreach (var item in exclusive)
                yield return item;

            foreach (var intersecting1 in list1.Except(exclusive)) {
                var intersecting2 = list2.First(i => i.Equals(intersecting1));
                yield return new PeerElement() {
                    ipAddress = intersecting1.ipAddress,
                    port = intersecting1.port,
                    lastSeen = Math.Max(intersecting1.lastSeen, intersecting2.lastSeen)
                };
            }
        }

        /// <summary>
        /// Starts a peer lookup for the domain specified by the SecurityManager.
        /// </summary>
        public DynamicSet<IPEndPoint> GetPeers(CancellationToken cancellationToken)
        {
            var peers = new DynamicSet<IPEndPoint>();


            var val = dht.GetValue(securityManager.DomainPublicKey, new byte[0], data => {
                // Console.WriteLine("---------FOUND DATA---------");
                // Console.WriteLine(Encoding.Unicode.GetString(data.Data));

                var list = ByteConverter.ReadObject<PeerList>(data.Data, 0);
                if (list.version != 1)
                    return;

                for (int i = 0; i < list.peers.Count(); i++)
                    peers.Add(new IPEndPoint(new IPAddress(list.peers[i].ipAddress), list.peers[i].port), i != list.peers.Count() - 1);
            }, cancellationToken);

            lock (val) {
                val.Setup(securityManager.DomainExpandedPrivateKey, (data1, data2) => {
                    var list1 = ByteConverter.ReadObject<PeerList>(data1, 0);
                    var list2 = ByteConverter.ReadObject<PeerList>(data2, 0);

                    PeerList result;
                    if (list1.version != 1) {
                        result = list2;
                    } else if (list2.version != 1) {
                        result = list1;
                    } else {
                        result = new PeerList {
                            version = 1,
                            peers = JoinPeerLists(list1.peers, list2.peers).ToArray()
                        };
                    }

                    Console.WriteLine("merge peer list 1: " + list1);
                    Console.WriteLine("merge peer list 2: " + list2);
                    Console.WriteLine("merge peer result: " + result);

                    return ByteConverter.WriteVal(result);
                });
            }

            lock (dataItems) {
                dataItems.Add(val);
            }

            RefreshLocalEndpoints();

            //newData = Encoding.Unicode.GetBytes("hello dht2");
            //val.Apply(newData, securityManager.GroupExpandedPrivateKey, 0);

            return peers;
        }
    }
}
