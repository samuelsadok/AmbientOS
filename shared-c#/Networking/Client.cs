using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.Networking
{
    public class Client<M, S>
        where M : struct, IConvertible
        where S : struct, IConvertible
    {

        /// <summary>
        /// Indicates that the request must be repeated, using a new message and specifies a maximum number of subsequent attempts to prevent infinite loops.
        /// </summary>
        public class RetryRequiredException : Exception
        {
            public NetMessage<M, S> NewRequest { get; private set; }
            public int MaxAttemts { get; private set; }
            public RetryRequiredException(NetMessage<M, S> newRequest, int maxAttempts)
            {
                NewRequest = newRequest;
                maxAttempts = MaxAttemts;
            }
        }




        private LogContext logContext;

        public Client(LogContext logContext)
        {
            this.logContext = logContext;
        }


        /// <summary>
        /// Gets/sets the host name. Writing to this property closes the connection and cancels any pending requests.
        /// </summary>
        public string Host { get { return host; } set { lock (disconnectLockRef) { CloseConnection(); host = value; } } }
        private string host;

        /// <summary>
        /// Gets/sets the port. Writing to this property closes the connection and cancels any pending requests.
        /// </summary>
        public int Port { get { return port; } set { lock (disconnectLockRef) { CloseConnection(); port = value; } } }
        private int port;


        /// <summary>
        /// Can be used to configure a callback that checks if the response indicates an error on the server side.
        /// The function will be called for every response that is received.
        /// If the function returns a retry required exception, the new request is sent instead of the exception being raised.
        /// Arguments: request, response, Returns: the exception to be raised or null.
        /// </summary>
        public Func<NetMessage<M, S>, NetMessage<M, S>, Exception> ResponseCheck { get; set; }





        private NetworkStream stream; // non-null while the connection is open
        private object streamLockRef = new object();

        private Action disconnectAction;
        private object disconnectLockRef = new object();

        private Mutex sendMutex = new Mutex();
        private bool receiverTaskRunning = false;
        private Queue<Tuple<NetworkStream, ResponseHandlerDelegate>> receivers = new Queue<Tuple<NetworkStream, ResponseHandlerDelegate>>(); // the handlers in this queue must not throw an exception

        private delegate void ResponseHandlerDelegate(Tuple<NetMessage<M, S>, BinaryContent> result, Exception exception);

        /// <summary>
        /// Returns a network pipe to the server.
        /// If the connection is currently not open, it will be establised.
        /// </summary>
        /// <param name="cancellationToken">cancels the connection attempt</param>
        private NetworkStream GetConnection(CancellationToken cancellationToken)
        {
            lock (streamLockRef) {
                if (stream == null) {
                    logContext.Log("creating tcp client...");
                    TcpClient client = new TcpClient();
                    logContext.Log("connecting to " + Host + ":" + Port + "...");

                    lock (disconnectLockRef)
                        disconnectAction = () => client.Close();

                    var ar = client.BeginConnect(Host, Port, null, null);

                    try {
                        ar.AsyncWaitHandle.WaitOne(cancellationToken);
                        client.EndConnect(ar);
                        stream = client.GetStream();
                    } catch {
                        CloseConnection();
                        throw;
                    }
                }

                return stream;
            }
        }


        /// <summary>
        /// Closes the connection with the server if it is open.
        /// Any attempt of establishing a new connection will also be interrupted.
        /// </summary>
        private void CloseConnection()
        {
            lock (disconnectLockRef) {
                if (stream != null) {
                    stream.Dispose();
                    stream = null;
                }

                if (disconnectAction != null)
                    disconnectAction();
                disconnectAction = null;
            }
        }


        /// <summary>
        /// Sends a message to the server and receives the response.
        /// Sends additional messages if instructed by the response check.
        /// Only the response to the last message will be returned.
        /// </summary>
        /// <param name="cancellationToken">Cancels the request by closing the connection. This also cancels any other pending requests.</param>
        public async Task<NetMessage<M, S>> SendRequest(NetMessage<M, S> request, CancellationToken cancellationToken)
        {
            int attempt = 1;

            while (true) {

                Tuple<NetMessage<M, S>, BinaryContent> response = null;
                Exception exception = null;

                var stream = GetConnection(cancellationToken);

                // create watchdog task that closes the stream on cancellation
                ManualResetEvent transferComplete = new ManualResetEvent(false);
                new Task(() => {
                    if (WaitHandle.WaitAny(new WaitHandle[] { cancellationToken.WaitHandle, transferComplete }) == 0)
                        CloseConnection();
                }).Start();


                // set up request
                logContext.Log("sending request " + request.Header);
                request["Host"] = Host;

                // send request and expect response
                sendMutex.WaitOne(cancellationToken);
                await request.WriteToStream(stream, cancellationToken);
                lock (receivers) {
                    receivers.Enqueue(new Tuple<NetworkStream, ResponseHandlerDelegate>(stream, (result, ex) => {
                        response = result;
                        exception = ex;
                        transferComplete.Set();
                    }));
                }
                sendMutex.ReleaseMutex();


                // maintain a separate task that is responsible for receiving responses in the correct order
                // this task will run as required and terminate eventually when the connection is closed
                lock (receivers) {
                    if (!receiverTaskRunning) {
                        new Task(() => {
                            Tuple<NetworkStream, ResponseHandlerDelegate> receiver;

                            while (true) {
                                lock (receivers) {
                                    if (!receivers.Any()) {
                                        receiverTaskRunning = false;
                                        return;
                                    }
                                    receiver = receivers.Dequeue();
                                }

                                Tuple<NetMessage<M, S>, BinaryContent> result = null;

                                try {
                                    result = NetMessage<M, S>.ReadFromStream<BinaryContent>(receiver.Item1, cancellationToken).WaitForResult(cancellationToken);
                                } catch (Exception ex) {
                                    receiver.Item2(null, ex);
                                    receiver = null;
                                }

                                if (receiver != null)
                                    receiver.Item2(result, null);
                            }
                        }).Start();
                    }
                }

                await transferComplete.WaitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                
                logContext.Log("received response");

                // handle transport layer errors
                if (exception != null) {
                    logContext.Log("request failed", LogType.Error);
                    throw exception;
                }

                // handle server side errors
                if (ResponseCheck != null)
                    exception = ResponseCheck(request, response.Item1);

                // there is a special kind of exception that tells the client to resend the request (e.g. redirection, credentials required, ...)
                var retry = exception as RetryRequiredException;
                if (retry != null)
                    if (retry.MaxAttemts > attempt++)
                        request = retry.NewRequest;
                    else
                        throw new Exception("too many attemts were made to complete a request, last attempt was \"" + request.Header + "\"");
                else if (exception != null)
                    throw new ServerSideException(request.Header, exception);
                else
                    return response.Item1;
            }
        }
    }


    public class ServerSideException : Exception
    {
        public ServerSideException(string request, Exception innerException)
            : base("the server reported an error for the request \"" + request + "\"", innerException)
        {
        }
    }
}
