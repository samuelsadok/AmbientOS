using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using AmbientOS.Environment;
using AmbientOS.Utils;
using AmbientOS.FileSystem;

namespace AmbientOS
{

    /// <summary>
    /// Thrown by a service when it finds that it cannot operate object it was given.
    /// If this is an action such as open with no specific application, the next compatible application is used.
    /// If the user specifies a specific application and it cannot use the object, the user is notified.
    /// </summary>
    public class AOSRejectException : Exception
    {
        public AOSRejectException(string message, string verb, IObjectRef obj)
            : base(obj + " is not compatible: " + message)
        {
        }
    }

    /// <summary>
    /// Thrown when no suitable application was found that could execute the specified action on the specifide object.
    /// </summary>
    public class AOSAppNotFoundException : Exception
    {
        public AOSAppNotFoundException(string action, IObjectRef obj)
            : base("No application was found that could " + action + " " + obj)
        {
        }

        public AOSAppNotFoundException(string action, IObjectRef obj, IEnumerable<AOSRejectException> rejections)
            : base("None of the available applications could " + action + " " + obj, new AggregateException(rejections))
        {
        }
    }

    public class AOSLockException : Exception
    {
        public AOSLockException(bool exclusive, IObjectRef obj)
            : base("Failed to aquire exclusive access rights to " + obj)
        {
        }
    }

    /// <summary>
    /// Represents the base interface for object references.
    /// All reference interfaces inherit from this interface.
    /// Services that consume objects from other services should use the interfaces derived from this one.
    /// </summary>
    public interface IObjectRef : IRefCounted
    {
        IObjectImpl Implementation { get; }
        Dictionary<string, object> GetExtensionProperties();
        string GetTypeName();
        T Cast<T>() where T : IObjectRef;
        //bool Implements<T>() where T : IObjectRef;
    }

    public interface IObjectRef<TImpl> : IObjectRef
        where TImpl : IObjectImpl
    {
        //IObjectRef<TImpl> GetIncremented();
        //IObjectRef<TImpl> GetDecremented();
    }

    /// <summary>
    /// Represents the base interface for object implementations.
    /// All implementation interfaces inherit from this interface.
    /// Services that provide objects to other services should use the "I[...]Impl" interfaces for the object implementations.
    /// </summary>
    public interface IObjectImpl
    {
        //IObjectRef Reference { get; }
    }

    public abstract class ObjectRef<TImpl> : IObjectRef<TImpl>
        where TImpl : IObjectImpl
    {
        private readonly object lockRef = new object();
        private int refCount = 0;

        protected bool localValue;
        protected TImpl implementation;
        protected IObjectRef<TImpl> reference;

        protected IObjectRef[] baseReferences;

        /// <summary>
        /// Should be asserted when there is new data to be delivered to the implementation.
        /// A data request counts as outgoing too.
        /// </summary>
        protected AutoResetEvent communicationSignal = new AutoResetEvent(false);

        protected abstract void FetchPropertiesFrom(TImpl implementation);
        protected abstract void DeliverPropertiesTo(TImpl implementation);


        public IObjectImpl Implementation { get { return implementation; } }

        public ObjectRef(TImpl implementation)
        {
            this.implementation = implementation;

            new Thread(() => {
                while (true) { // todo: what about shutdown?
                    communicationSignal.WaitOne();
                    Barrier();
                }
            }).Start(); 
        }

        public ObjectRef(IObjectRef<TImpl> reference)
        {
            this.reference = reference;
        }
        
        SynchronizedAction synchronize = new SynchronizedAction();

        /// <summary>
        /// Blocks until all property writes and method calls that were issued before are completed.
        /// </summary>
        public void Barrier()
        {
            synchronize.Run(() => {
                DeliverPropertiesTo(implementation);
                FetchPropertiesFrom(implementation);
            });
        }

        public Dictionary<string, object> GetExtensionProperties()
        {
            if (implementation != null) {
                var customAppearance = implementation as IExtensionProperties;
                if (customAppearance != null)
                    return customAppearance.GetExtensionProperties(typeof(TImpl));
                return new Dictionary<string, object>();
            } else {
                return reference.GetExtensionProperties();
            }
        }

        public string GetTypeName()
        {
            return ObjectStore.GetTypeName<TImpl>();
        }

        /// <summary>
        /// Creates a new reference to the same implementation as this reference, but associates the new reference with a different interface.
        /// If the implementation does not implement the requested interface, the method returns null.
        /// </summary>
        /// <typeparam name="T">The reference type that should be created.</typeparam>
        public T Cast<T>()
            where T : IObjectRef
        {
            if (implementation != null) {
                var attr = typeof(T).GetCustomAttribute<AOSInterfaceAttribute>();
                var implIf = attr.ImplementationInterface;
                if (!implIf.IsAssignableFrom(implementation.GetType()))
                    return default(T);
                var property = implIf.GetProperty(attr.ReferenceClass.Name);
                var objRef = property.GetValue(implementation);
                return ((T)objRef).Retain();
            } else {
                return reference.Cast<T>();
            }
        }

        /*public bool Implements<T>()
            where T : IObjectRef
        {
            if (implementation != null) {
                var attr = typeof(T).GetCustomAttribute<AOSInterfaceAttribute>();
                var implIf = attr.ImplementationInterface;
                return implIf.IsAssignableFrom(implementation.GetType());
            } else {
                return reference.Implements<T>();
            }
        }*/

        public void Alloc()
        {
            foreach (var i in baseReferences)
                i.Retain();
        }

        public void Free()
        {
            foreach (var i in baseReferences)
                i.Release();
        }

        void IDisposable.Dispose()
        {
            this.Release();
        }
    }

    /*
    string Name { get; }
    IAOSObject Parent { get; }


    public class AOSObjectLock : IDisposable
    {
        private AOSObject parent;
        public AOSObjectLock(AOSObject parent, bool exclusive)
        {
            parent.Lock(exclusive);
        }

        public void Dispose()
        {
            lock (this) {
                if (parent == null)
                    throw new ObjectDisposedException("the lock was already released");
                parent.Unlock(this);
                parent = null;
            }
        }
    }


    object lockRef = new object();
    List<AOSObjectLock> locks; // we track the locks so we can warn or kill the lockers if the object is about to disappear
    bool locked = false;
    bool exclusive = false;

    private void Lock(AOSObjectLock objLock, bool exclusive)
    {
        lock (lockRef) {
            if (locked && (exclusive || this.exclusive))
                throw new AOSLockException(exclusive, this); // todo: give more information about the culprit(s)
            locked = true;
            this.exclusive = exclusive;

            locks.Add(objLock);
        }
    }

    private void Unlock(AOSObjectLock objLock)
    {
        lock (lockRef) {
            locks.Remove(objLock);
            locked = false;
        }
    }

    public AOSObjectLock Lock(bool exclusive)
    {
        return new AOSObjectLock(this, exclusive);
    }


    /// <summary>
    /// Shall return the value of the attribute with the specified name.
    /// If this fails (unavailable, unauthorized, not implemented, ...), it's in most cases appropriate to return null.
    /// </summary>
    public abstract string GetAttribute(string name);

    /// <summary>
    /// Same as GetAttribute, but catches any exception (in which case null is returned).
    /// </summary>
    public string TryGetAttribute(string name)
    {
        try {
            return GetAttribute(name);
        } catch (Exception) {
            return null;
        }
    }

    public AOSService Service { get; private set; }

    protected AOSObject(AOSService publisher)
    {
        Service = publisher;
    }*/

    public interface IExtensionProperties
    {
        Dictionary<string, object> GetExtensionProperties(Type type);
    }
}
