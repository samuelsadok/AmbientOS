using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;

namespace AmbientOS
{

    /// <summary>
    /// Adds reference counting support to an object.
    /// This attaches an additional field to the object which will be used for reference counting.
    /// A class implementing this interface must also implement IDisposable.Dispose by simply calling this.Release().
    /// </summary>
    public interface IRefCounted : IDisposable
    {
        /// <summary>
        /// Shall initialize any resources required by this object.
        /// This is called whenever the reference count is incremented from 0.
        /// It is never called concurrently with Free.
        /// If a reference counted object has a reference count greater than 0, it is guaranteed that the Alloc method was called (and completed) previously (and more recently than Free).
        /// </summary>
        void Alloc();

        /// <summary>
        /// Shall free any resources associated with this object.
        /// This is called whenever the reference count is decremented to 0.
        /// It is never called concurrently with Alloc.
        /// If a reference counted object has a reference count of 0, it is guaranteed that the Free method was called (and completed) previously (and more recently than Alloc).
        /// </summary>
        void Free();
    }


    class RefCount
    {
        /// <summary>
        /// Counts the references to an IRefCounted object.
        /// </summary>
        public long count = 0;

        // These signals are required to synchronize calls to Alloc and Free.
        public ManualResetEvent isFreeSignal = new ManualResetEvent(true);
        public ManualResetEvent isAllocateSignal = new ManualResetEvent(false);
        // todo: add list of stack traces for debugging
    }

    public static class RefCounting
    {
        /// <summary>
        /// Records reference counts of each IRefCounted object.
        /// Any access to this field is thread safe.
        /// </summary>
        static readonly ConditionalWeakTable<IRefCounted, RefCount> references = new ConditionalWeakTable<IRefCounted, RefCount>();

        /// <summary>
        /// Increments the reference count of an IRefCounted object.
        /// If the reference count was 0 previously, the Alloc method is invoked.
        /// This method is guaranteed to block until the Alloc method returns, even if it's called multiple times concurrently.
        /// </summary>
        public static T Retain<T>(this T reference)
            where T : IRefCounted
        {
            var r = references.GetOrCreateValue(reference);
            var i = Interlocked.Increment(ref r.count);
            if (i <= 0) {
                throw new OverflowException("reference count overflow");
            } else if (i == 1) {
                r.isFreeSignal.WaitOne();
                r.isFreeSignal.Reset();
                reference.Alloc();
                r.isAllocateSignal.Set();
            } else {
                r.isAllocateSignal.WaitOne();
            }
            return reference;
        }

        /// <summary>
        /// Increments the reference count of an IRefCounted object.
        /// If the reference count reaches 0, the Free method is invoked.
        /// If the reference count becomes negative, the method throws an exception.
        /// This method is guaranteed to block until the Free method returns.
        /// </summary>
        public static void Release<T>(this T reference)
            where T : IRefCounted
        {
            var r = references.GetOrCreateValue(reference);
            var i = Interlocked.Decrement(ref r.count);
            if (i < 0) {
                throw new OverflowException("reference count cannot be negative");
            } else if (i == 0) {
                r.isAllocateSignal.WaitOne();
                r.isAllocateSignal.Reset();
                reference.Free();
                r.isFreeSignal.Set();
            }
        }

        /// <summary>
        /// Executes the specified action only if the reference count is non-zero.
        /// The caller is responsible of ensuring that the specified does not conflict with concurrent Alloc and Free calls.
        /// We can't provide this guarantee through this method as it may lead to deadlocks.
        /// </summary>
        public static void DoIfReferenced<T>(this T reference, Action action)
            where T : IRefCounted
        {
            var r = references.GetOrCreateValue(reference);
            if (Interlocked.Read(ref r.count) != 0)
                action();
        }

        public static T[] RetainAll<T>(this T[] array)
            where T : IRefCounted
        {
            foreach (var item in array)
                item.Retain();
            return array;
        }

        public static void ReleaseAll<T>(this T[] array)
            where T : IRefCounted
        {
            foreach (var item in array)
                item.Release();
        }
    }
}
