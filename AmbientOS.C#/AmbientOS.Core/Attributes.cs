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
    /// Used to tag a class as an AmbientOS service.
    /// Each public class that has this attribute is automatically scanned on application startup (or OS startup)
    /// and all actions contained in the class are registered.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AOSServiceAttribute : Attribute
    {
        /// <summary>
        /// A human-readable service name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Describes what the service can do.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// true: this service is incapable to cope with more than one action per instance,
        /// false: this service cannot be run more than once concurrently,
        /// null: let the OS decide
        /// </summary>
        public bool? MultipleInstances { get; set; }

        public AOSServiceAttribute(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Specifies for a service class, on which platforms the service can be run.
    /// This attribute can be used multiple times to specify multiple platforms.
    /// If the attribute is not present on a service class, the service is considered valid on all platforms.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ForPlatformAttribute : Attribute
    {
        public PlatformType Type { get; }

        public ForPlatformAttribute(PlatformType platformType)
        {
            Type = platformType;
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
        public ObjectConstraints Constraints { get; }

        public AOSActionAttribute(string verb, params string[] constraints)
        {
            Verb = verb;

            var dict = constraints.Select(a => {
                var parts = a.Split('=');
                if (parts.Count() != 2)
                    throw new Exception(string.Format("invalid attribute specifier \"{0}\"", a));
                return new {
                    key = parts[0],
                    value = (object)parts[1].UnescapeFromURL()
                };
            }).ToDictionary(a => a.key, a => a.value);
            Constraints = new ObjectConstraints(dict);
        }
    }


    /*/// <summary>
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
    }*/


    /// <summary>
    /// The class that has this attribute is considered as the main application of the containing assembly.
    /// A class with this attribute must implement IApplicationImpl and have a public parameterless constructor.
    /// When the containing assembly is executed (i.e. the *.exe file is started), the AmbientOS framework considers this as the intention to launch the specific class that has this attribute.
    /// Any executable assembly (*.exe) that represents a standalone AmbientOS app must contain exactly one public class with this attribute.
    /// An assembly that contains no main application should be built as a *.dll file.
    /// If an assembly fails to comply with the rules described here, the AmbientOS framework will fail to find the main application.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AOSMainApplicationAttribute : Attribute
    {
        public AOSMainApplicationAttribute()
        {
        }
    }
}
