using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using static AmbientOS.LogContext;
using static AmbientOS.TaskController;

namespace AmbientOS.Net.KRPC
{
    public abstract class KRPCSocket
    {
        public abstract void Start();

        /// <summary>
        /// Issues an RPC to a remote peer.
        /// Returns a dictionary of named response values.
        /// Returns null if the server does not respond.
        /// On any other error, an exception is thrown.
        /// </summary>
        internal abstract BDict Call(string method, BDict args, IPEndPoint endpoint);
    }

    public class SingleEndpointSocket : KRPCSocket
    {
        /// <summary>
        /// I could not find any place where this timespan is defined in relation to BitTorrent.
        /// </summary>
        private static TimeSpan TIMEOUT = TimeSpan.FromSeconds(5);

        private readonly UdpClient udp;
        private readonly ManualResetEvent canReceive = new ManualResetEvent(false);
        private readonly Dictionary<ushort, Transaction> transactions = new Dictionary<ushort, Transaction>();
        private readonly Dictionary<string, Func<BDict, IPEndPoint, BDict>> methods = new Dictionary<string, Func<BDict, IPEndPoint, BDict>>();

        /// <summary>
        /// If we're sitting behind a NAT, we have to rely on external peers to find out the local IP address.
        /// This queue holds the last few IPv4 addresses that were attributed to the local host.
        /// </summary>
        private readonly DynamicQueue<IPEndPoint> reportedEndpoints = new DynamicQueue<IPEndPoint>(8);

        /// <summary>
        /// Triggered when either the public or the local endpoint changes or becomes known.
        /// The first argument is the local address, the second is the public address.
        /// If either is not known, it is null.
        /// </summary>
        public event Action<IPEndPoint, IPEndPoint> IPEndpointChanged;

        public IPEndPoint PublicEndpoint { get { IPEndPoint endpoint; reportedEndpoints.TryGetMostCommonItem(out endpoint); return endpoint; } }
        public IPEndPoint LocalEndpoint { get; private set; } // todo: make thread-safe
        private readonly AddressFamily addressFamily;

        private class Transaction
        {
            private static int currentTransactionID = 0;
            public ushort ID { get; }
            public ManualResetEvent WaitHandle { get; }
            public Message Result { get; set; }
            public Transaction()
            {
                var id = Interlocked.Increment(ref currentTransactionID);
                ID = (ushort)(id & 0xFFFF);
                WaitHandle = new ManualResetEvent(false);
            }
        }

        /// <summary>
        /// Creates a new socket with a single local endpoint at the specified address and port.
        /// </summary>
        /// <param name="localEndpoint">The local endpoint to use. Set port to 0 to let the socket decide.</param>
        public SingleEndpointSocket(IPEndPoint localEndpoint)
        {
            if (localEndpoint.Port != 0) {
                LocalEndpoint = localEndpoint;
                IPEndpointChanged.SafeInvoke(LocalEndpoint, PublicEndpoint);
            } else {
                LocalEndpoint = null;
            }

            addressFamily = localEndpoint.Address.AddressFamily;

            udp = new UdpClient(localEndpoint);
            //udp.AllowNatTraversal(true); // problematic with Xamarin
        }


        /// <summary>
        /// Launches the KRPC socket.
        /// </summary>
        public override void Start()
        {
            reportedEndpoints.MostCommonElementChanged += (newEndpoint) => {
                DebugLog("public endpoint for socket at {0} updated: {1}", LocalEndpoint, newEndpoint);
                IPEndpointChanged.SafeInvoke(LocalEndpoint, newEndpoint);
            };

            var thread = new CancelableThread(() => {
                byte[] buffer;

                Wait(canReceive);

                while (true) {
                    ThrowIfCancellationRequested();

                    var endpoint = new IPEndPoint(IPAddress.Any, 0);

                    try {
                        buffer = udp.Receive(ref endpoint);
                    } catch (Exception ex) {
                        ThrowIfCancellationRequested();
                        Log(string.Format("UDP error in socket at {0}: {1}", LocalEndpoint, ex), LogType.Warning); // what to do? maybe log
                        Thread.Sleep(500);
                        continue;
                    }

                    try {
                        DebugLog("received message from {0}", endpoint);

                        var msg = Message.FromBytes(buffer);

                        if (msg.IPAddress != null) {
                            try {
                                var addr = new IPAddress(msg.IPAddress.Take(msg.IPAddress.Count() - 2).ToArray());
                                var port = msg.IPAddress.ReadUInt16(msg.IPAddress.Count() - 2, Endianness.NetworkByteOrder);
                                reportedEndpoints.StrongEnqueue(new IPEndPoint(addr, port));
                            } catch (Exception ex) {
                                Log(string.Format("failed to parse reported local IP address: {0}", ex.Message), LogType.Warning);
                            }
                        }

                        if (msg is QueryMessage) {
                            new CancelableThread(() => Handle(udp, (QueryMessage)msg, endpoint)).Start();
                        } else {
                            lock (transactions) {
                                Transaction t;
                                if (transactions.TryGetValue(msg.TransactionID, out t)) {
                                    t.Result = msg;
                                    t.WaitHandle.Set();
                                } // else ignore
                            }
                        }
                    } catch (Exception) {
                        Log("unknown KRPC error", LogType.Warning);
                    }
                }
            });
            
            thread.Start();

            thread.Context.Controller.OnCancellation(() => {
                lock (udp) {
                    udp.Close();
                }
            });
        }

        /// <summary>
        /// Handles a remote procedure call to the local host.
        /// </summary>
        private void Handle(UdpClient client, QueryMessage query, IPEndPoint endpoint)
        {
            try {
                Message response;
                try {
                    DebugLog("incoming KRPC query from {0}: {1}", endpoint, query.Method);

                    Func<BDict, IPEndPoint, BDict> method;
                    lock (methods) {
                        if (!methods.TryGetValue(query.Method, out method))
                            throw new ServerException(ServerException.ErrorCode.MethodUnknown);
                    }

                    response = new ResponseMessage() {
                        ReturnValues = method(query.Arguments, endpoint)
                    };
                } catch (Exception ex) {
                    response = new ErrorMessage(ex);
                    Log(string.Format("KRPC error in handling {0}: {1}", query.Method, ex), LogType.Warning);
                }

                response.IPAddress = endpoint.Address.GetAddressBytes().Concat(ByteConverter.WriteVal((ushort)endpoint.Port, Endianness.NetworkByteOrder)).ToArray();
                response.TransactionID = query.TransactionID;

                var buffer = response.Serialize();
                lock (client) {
                    ThrowIfCancellationRequested();
                    client.Send(buffer, buffer.Length, endpoint);
                }
            } catch (Exception ex) {
                Log(string.Format("error in sending KRPC response: {0}", ex.Message), LogType.Warning);
            }
        }

        public bool CanSendTo(IPEndPoint endpoint)
        {
            return addressFamily == endpoint.Address.AddressFamily;
        }


        /// <summary>
        /// Registers the specified handler under the specified method name.
        /// </summary>
        /// <param name="handler">A callback handler for the method. The handler will be provided with the named arguments of the procedure call and shall return the named return values of the call.</param>
        internal void Register(string name, Func<BDict, IPEndPoint, BDict> handler)
        {
            lock (methods)
                methods.Add(name, handler);
        }

        
        internal override BDict Call(string method, BDict args, IPEndPoint endpoint)
        {
            var t = new Transaction();
            lock (transactions)
                transactions[t.ID] = t;

            var query = new QueryMessage(method, args) { TransactionID = t.ID };
            var buffer = query.Serialize();

            // Thread-safety is questionnable when using send and receive simultaneously.
            // I can't see any obvious workaround to this (as closing and re-opening may change the port).
            // At least we ensure exclusive send access and exclusive receive access. So far it worked.

            lock (udp) {
                ThrowIfCancellationRequested();
                udp.Send(buffer, buffer.Length, endpoint);
                if (LocalEndpoint == null) {
                    LocalEndpoint = (IPEndPoint)udp.Client.LocalEndPoint;
                    IPEndpointChanged.SafeInvoke(LocalEndpoint, PublicEndpoint);
                }
            }
            canReceive.Set();

            DebugLog("started KRPC call \"{0}\" on {1}", method, endpoint);

            // wait for result
            var success = Wait(t.WaitHandle, TIMEOUT);
            lock (transactions)
                if (transactions[t.ID] == t)
                    transactions.Remove(t.ID);

            ThrowIfCancellationRequested();
            if (!success)
                return null;

            if (t.Result is ResponseMessage)
                return ((ResponseMessage)t.Result).ReturnValues;

            if (t.Result is ErrorMessage)
                throw ((ErrorMessage)t.Result).ToException();

            throw new Exception("Invalid response message type");
        }
    }

    public class MultiEndpointSocket : KRPCSocket
    {
        public SingleEndpointSocket[] Sockets { get; }

        public MultiEndpointSocket(params SingleEndpointSocket[] sockets)
        {
            Sockets = sockets;
        }

        public static MultiEndpointSocket FromAllLocalEndpoints(int preferredPort)
        {
            var localEndpoints = NetworkInterface.GetAllNetworkInterfaces()
                .Where(netInterface => netInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback && netInterface.OperationalStatus == OperationalStatus.Up)
                .SelectMany(netInterface => (from addr in netInterface.GetIPProperties().UnicastAddresses where !IPAddress.IsLoopback(addr.Address) select addr.Address))
                .Where(addr => !addr.IsIPv6LinkLocal);

            return new MultiEndpointSocket(localEndpoints.Select(endpoint => {
                var port = preferredPort;
                try {
                    return new SingleEndpointSocket(new IPEndPoint(endpoint, preferredPort));
                } catch (SocketException ex) {
                    if (ex.SocketErrorCode != SocketError.AddressAlreadyInUse)
                        throw;
                    if (port == 0)
                        throw;
                    return new SingleEndpointSocket(new IPEndPoint(endpoint, 0));
                }
            }).ToArray());
        }

        public override void Start()
        {
            foreach (var socket in Sockets)
                socket.Start();
        }

        /// <summary>
        /// Registers the specified handler under the specified method name.
        /// </summary>
        /// <param name="handler">A callback handler for the method. The handler will be provided with the named arguments of the procedure call and shall return the named return values of the call.</param>
        internal void Register(string name, Func<BDict, IPEndPoint, BDict> handler)
        {
            foreach (var socket in Sockets)
                socket.Register(name, handler);
        }

        internal override BDict Call(string method, BDict args, IPEndPoint endpoint)
        {
            BDict response;
            foreach (var socket in Sockets)
                if (socket.CanSendTo(endpoint))
                    if ((response = socket.Call(method, args, endpoint)) != null)
                        return response;
            return null;
        }
    }
}
