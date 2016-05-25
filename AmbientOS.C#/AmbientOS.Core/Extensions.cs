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

        /// <summary>
        /// Retrieves an object reference that points to the specified interface on the specified implementation.
        /// </summary>
        public static IObjectRef AsReference(this IObjectImpl implementation, Type type)
        {
            return type.GetCustomAttribute<AOSInterfaceAttribute>().Store.GetReference(implementation);
        }

        /// <summary>
        /// Retrieves an object reference that points to the specified interface on the specified implementation.
        /// </summary>
        public static T AsReference<T>(this IObjectImpl implementation)
            where T : IObjectRef
        {
            return (T)implementation.AsReference(typeof(T));
        }

        public static T Cast<T>(this IObjectRef objRef)
            where T : IObjectRef
        {
            return (T)objRef.Cast(typeof(T));
        }

        /// <summary>
        /// Checks if the object matches the specified constraints.
        /// </summary>
        public static bool CompliesTo(this IObjectRef obj, ObjectConstraints constraints)
        {
            // we prefetch all relevant properties so that they are retrieved in one query
            var constraintsArray = constraints.properties
                .Where(kv => kv.Value != null)
                .Select(kv => new {
                    name = kv.Key,
                    acceptedValues = kv.Value,
                    property = obj.GetType().GetProperty(kv.Key)?.GetValue(obj) as DynamicProperty
                }).ToArray();

            return constraintsArray.All(constraint => {
                object value;
                if (constraint.property == null)
                    return true;
                value = constraint.property.GetValueAsObject();
                return constraint.acceptedValues.Contains(value);
            });
        }

        public static bool IsAmbientOSInterface(this Type type)
        {
            return type.GetCustomAttribute<AOSInterfaceAttribute>() != null;
        }

        public static string GetAmbientOSTypeName(this Type type)
        {
            if (type == typeof(void))
                return "void";

            if (!IsAmbientOSInterface(type))
                throw new Exception(string.Format("The type {0} is not an AmbientOS interface.", type));
            return type.GetCustomAttribute<AOSInterfaceAttribute>().TypeName;
        }


        /*
        /// <summary>
        /// Returns the constraints that a handler must comply to to be able to handle this object.
        /// </summary>
        public static ObjectConstraints GetHandlerConstraints(this IObjectRef obj)
        {
            // prefetch properties to prevent unneccessary message passing
            var properties = obj.GetType().GetProperties()
                .Where(p => typeof(DynamicProperty).IsAssignableFrom(p.PropertyType))
                .Select(p => new KeyValuePair<string, object>(p.Name, p.GetValue(obj)))
                .Where(p => p.Value != null)
                .ToArray();

            var constraints = obj.GetExtensionProperties().Concat(properties).ToDictionary(kv => "obj." + kv.Key, kv => kv.Value);
            constraints["InputTypeName"] = obj.GetTypeName();
            return new ObjectConstraints(constraints);
        }
        */

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
