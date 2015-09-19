using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.Networking
{

    /// <summary>
    /// Accepts incoming connections and handles the client requests while ensuring optimal operation under all kinds of loads.
    /// At low load, clients are accepted and handled immediately. At moderate load, clients are still guaranteed to be accepted but the delay is not limited.
    /// Every client is guaranteed to have some minimum amount of time spent on its requests. This time limit is however only enforced if the server is under high load,
    /// in which case the client will be rejected.
    /// </summary>
    /// <typeparam name="M">enumeration type for methods</typeparam>
    /// <typeparam name="S">enumeration type for status codes</typeparam>
    public abstract class Server<M, S>
        where M : struct, IConvertible
        where S : struct, IConvertible
    {

        protected class InvalidMethodException : Exception
        {
            public InvalidMethodException(string method)
                : base("the method \"" + method + "\" is not supported by this server")
            {
            }
        }

        protected class InvalidRequestException : Exception
        {
            public InvalidRequestException(string reason)
                : base("the request is invalid: " + reason)
            {
            }
        }

        protected class ResourceNotFoundException : Exception
        {
            public ResourceNotFoundException(string resource)
                : base("the resource \"" + resource + "\" was not found")
            {
            }
        }


        protected LogContext LogContext { get; private set; }
        int port;
        TcpListener listener;
        ProcessorPool<TcpClient> clientProcessorPool;
        CancellationTokenSource cancellationTokenSource;
        int clientTimeQuota;

        private bool isRunning = false;
        public bool IsRunning { get { lock (listener) return isRunning; } }

        /// <summary>
        /// Must generate an answer for the request or raise an exception.
        /// </summary>
        public abstract NetMessage<M, S> HandleRequest(NetMessage<M, S> request, IPEndPoint endpoint, CancellationToken cancellationToken);
        /// <summary>
        /// Must generate a response that reports the specified exception to the client. This routine should not fail.
        /// </summary>
        /// <param name="request">The request that caused the exception. Can be null if message parsing failed.</param>
        public abstract NetMessage<M, S> HandleException(NetMessage<M, S> request, Exception exception, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a new server instance.
        /// </summary>
        /// <param name="port">The port number on which to listen</param>
        /// <param name="clientSlots">The maximum number of concurrently handled clients</param>
        /// <param name="maxResponseDelay">The maximum delay in milliseconds for accepting the next pending connection request</param>
        public Server(int port, int clientSlots, int clientTimeQuota, LogContext logContext)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            clientProcessorPool = new ProcessorPool<TcpClient>(clientSlots);
            this.clientTimeQuota = clientTimeQuota;
            LogContext = logContext;
            this.port = port;
        }

        /// <summary>
        /// Tries to punch a hole in the firewall.
        /// </summary>
        public void Setup(string name)
        {
            try {
                if (!Firewall.OpenPort(port, Protocol.TCP, name, LogContext)) return;
            } catch (Exception ex) {
                LogContext.Log("Failed to open port " + port + " (" + name + ") in the firewall. Some services may not be available. Details: " + ex, LogType.Warning);
                return;
            }
            LogContext.Log("The port " + port + " (" + name + ") was opened in the firewall.", LogType.Info);
        }

        /// <summary>
        /// Starts listening on the specified port and processing incoming client requests by calling the abstract HandleRequest function.
        /// The server can be stopped by either triggering the cancellation token or calling Stop.
        /// This call returns immediately.
        /// </summary>
        public void Start(CancellationToken cancellationToken)
        {
            lock (listener) {
                if (isRunning)
                    throw new Exception("the server is already running");
                isRunning = true;
                this.cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                LogContext.Log("starting TCP listener...");
                listener.Start();
                LogContext.Log("TCP listener started");

                clientProcessorPool.Start(listener.AcceptTcpClient, async (client, handlerCancellationToken) => {
                    LogContext.Log("client connection accepted");
                    NetMessage<M, S> response, request = null;
                    try {
                        request = (await NetMessage<M, S>.ReadFromStream<BinaryContent>(client.GetStream(), handlerCancellationToken)).Item1;
                        response = HandleRequest(request, (IPEndPoint)client.Client.RemoteEndPoint, handlerCancellationToken);
                    } catch (Exception ex) {
                        try {
                            response = HandleException(request, ex, handlerCancellationToken);
                        } catch (Exception exc) {
                            LogContext.Log("server exception handler failed: " + exc, LogType.Error);
                            response = new NetMessage<M, S>("fatal server error", default(S));
                        }
                    }
                    try {
                        await response.WriteToStream(client.GetStream(), handlerCancellationToken);
                    } catch (Exception ex) {
                        LogContext.Log("client did not accept response: " + ex.ToString(), LogType.Warning);
                    }
                    LogContext.Log("client connection handled");
                }, clientTimeQuota, cancellationTokenSource.Token);

                LogContext.Log("TCP server ready");

                new Task(() => {
                    cancellationTokenSource.Token.WaitHandle.WaitOne();

                    lock (listener) {
                        listener.Stop();
                        isRunning = false;
                    }
                }).Start();
            }
        }

        /// <summary>
        /// Stops the server in case it is running.
        /// </summary>
        public void Stop()
        {
            lock (listener)
                if (isRunning)
                    cancellationTokenSource.Cancel();
        }
    }
}
