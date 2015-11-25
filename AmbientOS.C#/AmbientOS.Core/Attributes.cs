using System;
using System.Collections.Generic;
using System.Linq;
using AmbientOS.Utils;

namespace AmbientOS
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class AOSInterfaceAttribute : Attribute
    {
        public string TypeName { get; private set; }
        public Type ImplementationInterface { get; private set; }
        public Type ReferenceClass { get; private set; }

        public AOSInterfaceAttribute(string typeName, Type implIf, Type refClass)
        {
            TypeName = typeName;
            ImplementationInterface = implIf;
            ReferenceClass = refClass;
        }
    }

    /// <summary>
    /// Used to tag a class as an AmbientOS application.
    /// If an application is installed, this information is given to the application registry.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AOSApplicationAttribute : Attribute
    {
        /// <summary>
        /// A human-readable application name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Describes what the application or service can do.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// true: this application is incapable to cope with more than one action per instance,
        /// false: this application cannot be run more than once concurrently,
        /// null: let the OS decide
        /// </summary>
        public bool? MultipleInstances { get; set; }

        public AOSApplicationAttribute(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Use this to tag action handlers in a class that is tagged with AOSApplicationAtrribute.
    /// Methods with this attribute are registered in the application registry when the application (class) is installed.
    /// The same method can be registered multiple times. This is useful for instance if an application can open files with different extensions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class AOSActionAttribute : Attribute
    {
        /// <summary>
        /// The verb that this attribute can execute on the object.
        /// </summary>
        public string Verb { get; }

        /// <summary>
        /// The constraint that an object must satisfy for the associated action to be considered.
        /// When the OS selects an action to handle an object, it only considers the ones with constraints that apply to the object's attributes.
        /// </summary>
        public ObjectAppearance Constraints { get; }

        public AOSActionAttribute(string verb, params string[] constraints)
        {
            Verb = verb;

            var dict = constraints.Select(a => {
                var parts = a.Split('=');
                if (parts.Count() != 2)
                    throw new Exception(string.Format("invalid attribute specifier \"{0}\"", a));
                return new {
                    key = parts[0],
                    value = parts[1].UnescapeFromURL()
                };
            }).ToDictionary(a => a.key, a => a.value);
            Constraints = new ObjectAppearance(dict);
        }
    }


    /// <summary>
    /// If a method of an interface is marked with this attribute, the method is automatically invoked
    /// when the object appearance is queried.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
    public class AOSAttributeAttribute : Attribute
    {
        public string Name { get; }
        public string Method { get; }
        public string Field { get; set; }
        public AOSAttributeAttribute(string name, string method)
        {
            Name = name;
            Method = method;
        }
    }
}
