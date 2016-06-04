using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static AmbientOS.TaskController;

namespace AmbientOS
{
    public enum ValueAccess
    {
        Read = 1,
        Write = 2,
        ReadWrite = 3
    }


    public abstract class DynamicValue
    {
        /// <summary>
        /// Fetches multiple dynamic values at the same time.
        /// This is preferred over fetching them serially, because it prevents passing multiple messages where one would be sufficient.
        /// </summary>
        public static void GetValues<T1, T2>(DynamicValue<T1> property1, out T1 result1, DynamicValue<T2> property2, out T2 result2)
        {
            result1 = property1.Get();
            result2 = property2.Get();
        }

        /// <summary>
        /// Indicates the access type of the value. This must not change over the lifetime of a dynamic value.
        /// </summary>
        public abstract ValueAccess Access { get; }

        public abstract object GetAsObject();
    }

    /// <summary>
    /// Represents a dynamic local or remote value, taking into account possible unreliabilities of the network.
    /// Reading the value may return an outdated value, but if it succeeds, it is guaranteed that the value was actually reported by the object in the past.
    /// Updating the value may not immediately have an effect, but some effort is made that the value arrives eventually.
    /// It is important to keep in mind that reading will not necessarily return the value that was just written, even if only one client uses the object.
    /// </summary>
    public abstract class DynamicValue<T> : DynamicValue
    {
        public override object GetAsObject()
        {
            return Get();
        }

        /// <summary>
        /// Initiates fetching of the value if it is not already available.
        /// This method returns immediately.
        /// </summary>
        /// <returns>The object on which the call is made.</returns>
        public DynamicValue<T> Fetch()
        {
            return this;
        }

        /// <summary>
        /// Returns the last known value.
        /// If the value was already read previously, the method returns immediately and does not guarantee that the result is up-to-date.
        /// </summary>
        public T Get()
        {
            SequenceNumber seqNo;
            return Get(out seqNo);
        }

        /// <summary>
        /// Sets the specified value lazily.
        /// The method returns immediately without making a guarantee for a successful write operation.
        /// </summary>
        public void Set(T value)
        {
            SetEx(value, SequenceNumber.None);
        }

        /// <summary>
        /// Returns the last known value and it's sequence number.
        /// If the value was already read previously, the method returns immediately and does not guarantee that the result is up-to-date.
        /// </summary>
        /// <param name="seqNo">The sequence number of the returned value. For subsequent calls, this number never decreases (except in the case of an overflow).</param>
        public T Get(out SequenceNumber seqNo)
        {
            if (!Access.HasFlag(ValueAccess.Read))
                throw new InvalidOperationException("The value cannot be read.");

            return GetEx(out seqNo);
        }

        /// <summary>
        /// Sets the specified value lazily.
        /// The method returns immediately without making a guarantee for a successful write operation.
        /// </summary>
        public void Set(T value, SequenceNumber seqNo)
        {
            if (!Access.HasFlag(ValueAccess.Read))
                throw new InvalidOperationException("The value cannot be written.");

            SetEx(value, seqNo);
        }

        /// <summary>
        /// Shall initiate fetching of the value if it is not already available.
        /// This method shall return immediately.
        /// </summary>

        protected abstract void FetchEx();

        /// <summary>
        /// Shall return the last known value and it's sequence number.
        /// If the value was already read previously, the method shall return immediately and does not have to guarantee that the result is up-to-date.
        /// This method is only invoked if permitted by the access mode.
        /// This method must be thread-safe with respect to itself and to concurrent <see cref="SetEx"/> calls.
        /// </summary>
        /// <param name="seqNo">The sequence number of the returned value. For subsequent calls, this number must never decrease (except in the case of an overflow).</param>
        protected abstract T GetEx(out SequenceNumber seqNo);

        /// <summary>
        /// Shall set the specified value lazily.
        /// The method shall return immediately and does not have to guarantee a successful write operation.
        /// This method is only invoked if permitted by the access mode.
        /// This method must be thread-safe with respect to itself and to concurrent <see cref="GetEx"/> calls.
        /// </summary>
        /// <param name="seqNo">If not null, specifies the expected sequence number that the old value must have in order to be updated. In case of a mismatch, the update shall be aborted.</param>
        protected abstract void SetEx(T value, SequenceNumber seqNo);

        /// <summary>
        /// Notifies the subscribers, that a new value may have become known.
        /// This does not guarantee that the value has actually changed, however the method should not be invoked unneccessarily (for performance reasons).
        /// This method is thread-safe and returns immediately.
        /// </summary>
        protected void Notify()
        {
            // todo
        }
    }

    public class LocalValue<T> : DynamicValue<T>
    {
        public override ValueAccess Access { get; }
        
        private Tuple<T, SequenceNumber> value;

        /// <summary>
        /// Creates a local read-only value.
        /// </summary>
        public LocalValue(T value)
            : this(value, ValueAccess.Read)
        {
        }

        public LocalValue(T value, ValueAccess access)
        {
            Access = access;
            this.value = new Tuple<T, SequenceNumber>(value, SequenceNumber.Zero);
        }

        protected override void FetchEx()
        {
            // nothing to do
        }

        /// <summary>
        /// Retrieves the local value in a lock-free way.
        /// </summary>
        protected override T GetEx(out SequenceNumber seqNo)
        {
            var value = this.value;
            seqNo = value.Item2;
            return value.Item1;
        }

        /// <summary>
        /// Updates the local value in a lock-free way.
        /// </summary>
        protected override void SetEx(T value, SequenceNumber seqNo)
        {
            var oldValue = this.value;

            if (!seqNo.HasValue)
                seqNo = oldValue.Item2;

            var newValue = new Tuple<T, SequenceNumber>(value, seqNo.Increment());

            // if the old sequence number matches the expectation, update the value (except if another thread was faster)
            if (oldValue.Item2 == seqNo)
                if (Interlocked.CompareExchange(ref this.value, newValue, oldValue) == oldValue)
                    if (!oldValue.Item1.Equals(newValue.Item1))
                        Notify();
        }
    }

    public class DependentValue<TInner, TOuter> : DynamicValue<TOuter>
    {
        /// <summary>
        /// Represents a converter from the inner (source) value to the outer (presented) value.
        /// </summary>
        public delegate TOuter FromSourceConverter(TInner source);

        /// <summary>
        /// Represents a converter from the outer (presenter) value to the inner (source) value.
        /// The old inner value is provided to the converter for more flexibilty.
        /// </summary>
        public delegate TInner ToSourceConverter(TOuter newValue, TInner oldSource);

        public override ValueAccess Access { get; }

        readonly DynamicValue<TInner> source;
        readonly FromSourceConverter fromSourceConverter;
        readonly ToSourceConverter toSourceConverter;

        /// <summary>
        /// Creates a dynamic value that is based on another dynamic value.
        /// </summary>
        /// <param name="source">The source upon which this value should be based.</param>
        /// <param name="fromSourceConverter">Can be null if <see cref="toSourceConverter"/> is not null. Must be thread-safe with respect to itself and <see cref="toSourceConverter"/>.</param>
        /// <param name="toSourceConverter">Can be null if <see cref="fromSourceConverter"/> is not null. Must be thread-safe with respect to itself and <see cref="fromSourceConverter"/>.</param>
        public DependentValue(DynamicValue<TInner> source, FromSourceConverter fromSourceConverter, ToSourceConverter toSourceConverter)
        {
            if (source == null)
                throw new ArgumentNullException($"{source}");

            if (fromSourceConverter == null && toSourceConverter == null)
                throw new ArgumentException($"Either {fromSourceConverter} or {toSourceConverter} must be non-null.");

            Access = toSourceConverter == null ? ValueAccess.Read : fromSourceConverter == null ? ValueAccess.Write : source.Access;

            this.source = source;
            this.fromSourceConverter = fromSourceConverter;
            this.toSourceConverter = toSourceConverter;
        }

        protected override void FetchEx()
        {
            source.Fetch();
        }

        protected override TOuter GetEx(out SequenceNumber seqNo)
        {
            return fromSourceConverter(source.Get(out seqNo));
        }

        protected override void SetEx(TOuter value, SequenceNumber seqNo)
        {
            SequenceNumber sourceSeqNo;
            var oldValue = source.Get(out sourceSeqNo);

            if (seqNo.HasValue && seqNo != sourceSeqNo)
                return;

            var newValue = toSourceConverter(value, oldValue);
            source.Set(newValue, sourceSeqNo);
        }
    }

    public class LambdaValue<T> : DynamicValue<T>
    {
        public override ValueAccess Access { get; }

        readonly EventWaitHandle readCacheValid = new ManualResetEvent(false);
        readonly EventWaitHandle writeCacheValid = new AutoResetEvent(false);
        readonly EventWaitHandle fetchRequested = new AutoResetEvent(false);

        Tuple<T, SequenceNumber> readCache;
        Tuple<T, SequenceNumber> writeCache;

        /// <summary>
        /// Creates a dynamic value that is backed by a getter and/or setter function.
        /// The two functions are allowed to take some time and all calls to them are guaranteed to be sequential.
        /// </summary>
        public LambdaValue(Func<T> getter, Action<T> setter)
        {
            if (getter == null && setter == null)
                throw new ArgumentException($"Either {getter} or {setter} must be non-null.");

            Access = (getter == null ? 0 : ValueAccess.Read) | (setter == null ? 0 : ValueAccess.Write);

            var manager = new CancelableThread(() => {
                while (true) {
                    var write = WaitAny(fetchRequested, writeCacheValid) == 0;
                    
                    var currentSeqNo = readCache?.Item2 ?? SequenceNumber.Zero;

                    if (write) {
                        var value = writeCache;
                        if (!value.Item2.HasValue || currentSeqNo.Increment() == value.Item2)
                            setter(value.Item1); // todo: error handling
                    }

                    var newValue = getter(); // todo: error handling
                    var didChange = readCache == null ? true : Equals(newValue, readCache.Item1);
                    readCache = new Tuple<T, SequenceNumber>(newValue, currentSeqNo.Increment());
                    readCacheValid.Set();

                    if (didChange)
                        Notify();
                }
            });
            manager.Start();
        }

        public LambdaValue(Func<T> getter)
            : this(getter, null)
        {
        }

        protected override void FetchEx()
        {
            fetchRequested.Set();
        }

        protected override T GetEx(out SequenceNumber seqNo)
        {
            if (!readCacheValid.WaitOne(0)) {
                fetchRequested.Set();
                Wait(readCacheValid);
            }

            var value = readCache;
            seqNo = value.Item2;
            return readCache.Item1;
        }

        protected override void SetEx(T value, SequenceNumber seqNo)
        {
            var currentSeqNo = readCache?.Item2 ?? SequenceNumber.Zero;

            var newValue = new Tuple<T, SequenceNumber>(value, currentSeqNo.Increment());

            if (!seqNo.HasValue || currentSeqNo == seqNo) {
                writeCache = newValue;
                writeCacheValid.Set();
            }
        }
    }







    /*

    
    public class DynamicProperty<T> : DynamicValue
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

        public override object GetAsObject()
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

        public DynamicEndpoint(T initialValue, ValueAccess access)
            : this(() => initialValue, access == ValueAccess.ReadWrite ? val => initialValue = val : (Action<T>)null)
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
    */

}
