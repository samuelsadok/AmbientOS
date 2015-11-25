using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AmbientOS
{
    public static class Extensions
    {
        /*public static ObjectRef<T> AsRef<T>(this T obj)
            where T : IObjectRef
        {
            var objRef = obj as ObjectRef<T>;
            if (obj == null)
                throw new Exception("Not a valid object reference");
            return objRef;
        }*/

        /*
    public static IObjectRef Reference<T>(this IObjectImpl implementation)
        where T : IObjectRef
    {
        var type = typeof(T);
        var attr = type.GetCustomAttribute<AOSInterfaceAttribute>();
        IObjectRef objRef = (IObjectRef)Activator.CreateInstance(attr.ReferenceClass, new object[] { implementation });
        lock(implementation.References)
            implementation.References.Add(objRef);
        return objRef;
    }
    */

        public static T AsImplementation<T>(this IObjectRef objRef)
            where T : IObjectImpl
        {
            var impl = objRef.Implementation;
            if (impl == null) // todo: consider cases where the object implementation is actually local but still connected through message passing (or prevent this from happening)
                throw new Exception("The object implementation is not local.");
            if (!typeof(T).IsAssignableFrom(impl.GetType()))
                throw new Exception(string.Format("The object reference {0} does not have an implementation of type {1} (have {2})", objRef.ToString(), typeof(T).ToString(), impl.GetType().ToString()));
            return (T)impl;
        }

        /*
        public static T GetDecremented<T>(this T reference)
            where T : IObjectRef
        {
            reference.Release();
            return reference;
        }
        */
    }
}
