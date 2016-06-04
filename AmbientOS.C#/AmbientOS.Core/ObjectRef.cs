using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using AmbientOS.Environment;
using AmbientOS.Utils;
using AmbientOS.FileSystem;
using static AmbientOS.TaskController;

namespace AmbientOS
{
    /// <summary>
    /// This is the base type for all AmbientOS types.
    /// It represents a reference that points to a local implementation or to another, remote reference.
    /// </summary>
    public interface IObjectRef : IRefCounted
    {
        IObjectImpl Implementation { get; }
        //string GetTypeName();
        IObjectRef Cast(Type type);
        //bool Implements<T>() where T : IObjectRef;
    }

    public interface IObjectRef<TImpl> : IObjectRef
        where TImpl : IObjectImpl
    {
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
        protected TImpl implementation;
        protected IObjectRef<TImpl> reference;

        protected IObjectRef[] baseInterfaces;

        /// <summary>
        /// Should be asserted when there is new data to be delivered to the implementation.
        /// A data request counts as outgoing too.
        /// </summary>
        protected AutoResetEvent communicationSignal = new AutoResetEvent(false);

        //protected abstract void FetchPropertiesFrom(TImpl implementation);
        //protected abstract void DeliverPropertiesTo(TImpl implementation);


        public IObjectImpl Implementation { get { return implementation; } }

        public ObjectRef(TImpl implementation)
        {
            this.implementation = implementation;

            new CancelableThread(() => {
                while (true) {
                    Wait(communicationSignal);
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
                //DeliverPropertiesTo(implementation);
                //FetchPropertiesFrom(implementation);
            });
        }

        //public string GetTypeName()
        //{
        //    return ObjectStore.GetTypeName<TImpl>();
        //}

        /// <summary>
        /// Creates a new reference to the same implementation as this reference, but associates the new reference with a different interface.
        /// If the implementation does not implement the requested interface, the method returns null.
        /// </summary>
        /// <param name="type">The reference type that should be created.</param>
        public IObjectRef Cast(Type type)
        {
            if (implementation != null) {
                var attr = type.GetCustomAttribute<AOSInterfaceAttribute>();
                var implIf = attr.ImplementationInterface;
                if (!implIf.IsAssignableFrom(implementation.GetType()))
                    return null;
                var property = implIf.GetProperty(attr.ReferenceClass.Name);
                var objRef = (IObjectRef)property.GetValue(implementation);
                return objRef.Retain();
            } else {
                return reference.Cast(type);
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
            foreach (var i in baseInterfaces)
                i.Retain();
        }

        public void Free()
        {
            foreach (var i in baseInterfaces)
                i.Release();
        }

        void IDisposable.Dispose()
        {
            this.Release();
        }
    }
}
