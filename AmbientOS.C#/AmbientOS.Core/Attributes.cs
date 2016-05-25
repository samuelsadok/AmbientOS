using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace AmbientOS
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class AOSInterfaceAttribute : Attribute
    {
        public string TypeName { get; }
        public Type ImplementationInterface { get; }
        public Type ReferenceClass { get; }

        private ObjectStore store = null;
        public ObjectStore Store
        {
            get
            {
                // In case of concurrent fetches, the worst thing that can happen is that the same field is retrieved more than once
                if (store == null)
                    store = (ObjectStore)ReferenceClass.GetField("store", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                return store;
            }
        }

        public AOSInterfaceAttribute(string typeName, Type implementationInterface, Type referenceClass)
        {
            TypeName = typeName;
            ImplementationInterface = implementationInterface;
            ReferenceClass = referenceClass;
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
    /// Use this to tag methods that can be used as object provider or classes that contain such methods.
    /// Methods with this attribute are automatically registered as object providers the object store when the assembly is loaded (are they?).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class AOSObjectProviderAttribute : Attribute
    {
    }

    /// <summary>
    /// Use this on the input or output of object provider methods.
    /// If this attribute is used, this gives the system more detailed information about what kind of objects the object provider accepts or generates.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = true)]
    public class AOSObjectConstraintAttribute : Attribute
    {
        /// <summary>
        /// The name of the property that this constraint refers to.
        /// This should be a valid property of the associated object type.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// The list of possible values for the specified object property.
        /// </summary>
        public string[] Values { get; }

        public AOSObjectConstraintAttribute(string propertyName, params string[] values)
        {
            PropertyName = propertyName;
            Values = values;
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
