using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace AmbientOS
{

    public abstract class DynamicProperty
    {
        /// <summary>
        /// Fetches multiple dynamic properties at the same time.
        /// This is preferred over fetching them serially, because it prevents passing multiple messages where one would be sufficient.
        /// </summary>
        public static void GetValues<T1, T2>(DynamicProperty<T1> property1, out T1 result1, DynamicProperty<T2> property2, out T2 result2)
        {
            result1 = property1.GetValue();
            result2 = property2.GetValue();
        }

        public abstract object GetValueAsObject();
    }




    /// <summary>
    /// Represents a read-write property of a local or remote object, taking into account possible unreliabilities of the network.
    /// Reading from the property may return an outdated value, but if it succeeds, it is guaranteed that the value was actually reported by the object in the past.
    /// Writing to the property may not immediately have an effect, but some effort is made that the value arrives eventually.
    /// It is important to note that reading will not necessarily return the value that was just written, even if only one client uses the object.
    /// </summary>
    public class DynamicProperty<T> : DynamicProperty
    {
        // incoming value from endpoint
        Tuple<T, long> inValue = null; // null if there is no valid value available
        bool inIsRequested = false; // true if the next message should include a request for this value
        ManualResetEvent validInput = new ManualResetEvent(false); // triggered when an input becomes available

        // outgoing value to endpoint
        Tuple<T, long?> outValue = null; // null if there is no value to be delivered

        EventWaitHandle communicationSignal; // Signaled when communication with the object becomes neccessary. This may be due to a value request or transmission.

        internal DynamicProperty(EventWaitHandle communicationSignal)
        {
            this.communicationSignal = communicationSignal;
        }

        /// <summary>
        /// Returns this property instance.
        /// If this property instance has not yet cached any value, calling this method will enlist this property for retrieval.
        /// This does not initiate a message to the object, but if messages are exchanged due to other reasons, this property is included.
        /// </summary>
        internal DynamicProperty<T> Get()
        {
            // todo: If a value is only updated (not read), there is the overhead of fetching it when using this approach. Think about how this can be solved gracefully.
            inIsRequested = true;
            validInput.Reset();
            return this;
        }

        /// <summary>
        /// Fetches the latest property value from the specified endpoint (if it was requested).
        /// This must not be called multiple times simultaneously. It is only meant to be called from internal AmbientOS library code.
        /// 
        /// todo: implement analogous version that adds a request to a message
        /// </summary>
        internal void FetchFrom(DynamicEndpoint<T> endpoint)
        {
            if (!inIsRequested)
                return;
            inIsRequested = false;
            
            try {
                // todo: should we reject decrementing sequence numbers here?
                long seqNo;
                var value = endpoint.Get(out seqNo);
                inValue = new Tuple<T, long>(value, seqNo);
            } catch (System.Net.Sockets.SocketException) { // todo: settle on a custom exception that leads to a retransmission
                inIsRequested = true;
                communicationSignal.Set();
                return;
            }

            validInput.Set();
        }

        /// <summary>
        /// Delivers the latest property value to the specified endpoint.
        /// This must not be called multiple times simultaneously. It is only meant to be called from internal AmbientOS library code.
        /// 
        /// todo: implement analogous version that adds the property to a message
        /// </summary>
        internal void DeliverTo(DynamicEndpoint<T> endpoint)
        {
            var value = Interlocked.Exchange(ref outValue, null);
            if (value == null)
                return;

            try {
                endpoint.Set(value.Item1, value.Item2);
            } catch (System.Net.Sockets.SocketException) { // todo: settle on a custom exception that leads to a retransmission
                Interlocked.CompareExchange(ref outValue, value, null); // if a new value has been provided meanwhile, don't overwrite it
            }
        }

        /// <summary>
        /// Returns the most recent cached value of this property.
        /// If a value has been read previously, that value is returned immediately.
        /// If no value has been read yet, the method blocks until a value is available
        /// or fails if the connection to the object breaks down in the meantime.
        /// </summary>
        public T GetValue(out long sequenceNumber)
        {
            if (!validInput.WaitOne(0)) {
                inIsRequested = true;
                communicationSignal.Set();
                validInput.WaitOne(); // todo: respect task controller
            }

            var value = inValue;
            sequenceNumber = value.Item2;
            return value.Item1;
        }

        /// <summary>
        /// See description for overload
        /// </summary>
        public T GetValue()
        {
            long seqNo;
            return GetValue(out seqNo);
        }

        public override object GetValueAsObject()
        {
            return GetValue();
        }

        /// <summary>
        /// Sets value of the property lazily. This method stages the new value for transmission and returns immediately.
        /// It does neither guarantee that the value is sent to the object successfully,
        /// nor that the underlying object will actually accept the value.
        /// It will be attempted to transmit the value to the object until it is either acknowledged, rejected, updated to another value or the connection breaks down.
        /// In the corner case where the value is constantly updated locally we have yet to examine the guarantees we want to make.
        /// </summary>
        /// <param name="value">The value to set</param>
        /// <param name="expectedSeqNo">If not null, specifies the sequence number that the property must have at the object. If there is a mismatch, the value is rejected. This allows atomic read-modify-write operations.</param>
        public void SetValue(T value, long? expectedSeqNo = null)
        {
            outValue = new Tuple<T, long?>(value, expectedSeqNo);
            communicationSignal.Set();
        }


        public override string ToString()
        {
            return GetValue().ToString();
        }
    }


    public enum PropertyAccess
    {
        ReadOnly = 1,
        ReadWrite = 3
    }

    /// <summary>
    /// Represents an endpoint that is defined by a getter and setter method.
    /// Calls to the getter and setter methods are serialized.
    /// Intuitively, a get call should return the same value as the last set call, but this is not a requirement.
    /// The endpoint has a sequence number that is incremented on every set-call.
    /// </summary>
    public class DynamicEndpoint<T>
    {
        readonly Func<T> getter;
        readonly Action<T> setter;

        readonly object lockRef = new object();

        long sequenceNumber = 0;

        /// <summary>
        /// Creates a read-write endpoint.
        /// </summary>
        public DynamicEndpoint(Func<T> getter, Action<T> setter)
        {
            this.getter = getter;
            this.setter = setter;
        }

        /// <summary>
        /// Creates a read-only endpoint.
        /// </summary>
        public DynamicEndpoint(Func<T> getter)
            : this(getter, null)
        {
        }

        public DynamicEndpoint(T initialValue, PropertyAccess access)
            : this(() => initialValue, access == PropertyAccess.ReadWrite ? val => initialValue = val : (Action<T>)null)
        {
        }

        /// <summary>
        /// Fetches the value from the endpoint by calling its getter.
        /// </summary>
        /// <param name="sequenceNumber">The current sequence number. This is only incremented on a set call, NOT if the value changes on its own.</param>
        public T Get(out long sequenceNumber)
        {
            if (getter == null)
                throw new Exception("This property cannot be read from");
            
            lock (lockRef) {
                sequenceNumber = this.sequenceNumber;
                return getter();
            }
        }

        /// <summary>
        /// Fetches the value from the endpoint by calling its getter.
        /// </summary>
        public T Get()
        {
            long seqNo;
            return Get(out seqNo);
        }

        /// <summary>
        /// Delivers the specified value to the endpoint by calling its setter and increments the sequence number.
        /// This method blocks until the setter returns.
        /// </summary>
        /// <param name="expectedSequenceNumber">If not null, specifies the sequence number that the endpoint must have in order to accept the new value.</param>
        public void Set(T value, long? expectedSequenceNumber = null)
        {
            if (setter == null)
                throw new Exception("This is a read-only property");

            lock (lockRef) {
                if (expectedSequenceNumber != null)
                    if (expectedSequenceNumber.Value != sequenceNumber)
                        throw new Exception("invalid sequence number");
                sequenceNumber++;
                setter(value);
            }
        }
    }

}
