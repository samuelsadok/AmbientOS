using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmbientOS.Utils;

namespace AmbientOS.Environment
{
    public class ObjectProvider : IObjectProviderImpl
    {
        public DynamicValue<Type> InputInterface { get; }
        public DynamicValue<Type> OutputInterface { get; }

        readonly ObjectConstraints inputConstraints;
        readonly ObjectConstraints outputAppearance;

        readonly MethodInfo method;

        public ObjectProvider(MethodInfo method)
        {
            this.method = method;

            if (method.GetParameters().Count() != 1)
                throw new Exception("A method must take a single AmbientOS object of some type as an input and return and AmbientOS object of some other type.");

            var input = method.GetParameters().First();

            var inputType = input.ParameterType;
            var outputType = method.ReturnType;

            if (!inputType.IsAmbientOSInterface())
                throw new Exception("The input parameter of an object provider method must be an AmbientOS interface type (have " + inputType + ")");
            if (!outputType.IsAmbientOSInterface())
                throw new Exception("The return value of an object provider method must be an AmbientOS interface type (have " + outputType + ")");

            InputInterface = new LocalValue<Type>(inputType);
            OutputInterface = new LocalValue<Type>(outputType);

            inputConstraints = new ObjectConstraints(input);
            outputAppearance = new ObjectConstraints(method.ReturnTypeCustomAttributes);
        }

        public DynamicSet GetInputConstraints(string property)
        {
            object[] values;
            if (!inputConstraints.properties.TryGetValue(property, out values))
                return null;
            return new DynamicSet<object>(values);
        }

        public DynamicSet GetOuputAppearance(string property)
        {
            object[] values;
            if (!outputAppearance.properties.TryGetValue(property, out values))
                return null;
            return new DynamicSet<object>(values);
        }

        public IObjectRef Invoke(IObjectRef obj)
        {
            return (IObjectRef)method.Invoke(null, new object[] { obj.Cast(OutputInterface.Get()) });
        }

        public void Install(MethodInfo method)
        {
            ObjectStore.PublishObject(new ObjectProvider(method));
        }

        public void Install(Type type)
        {
            var types = type
                .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(t => t.GetCustomAttribute<AOSObjectProviderAttribute>() != null);

            foreach (var t in types) {
                if (!t.IsPublic)
                    throw new Exception("The type " + t + " contains AmbientOS object providers, so it must be public.");
                Install(t);
            }

            var methods = type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<AOSObjectProviderAttribute>() != null);

            foreach (var m in methods) {
                if (!m.IsPublic || !m.IsStatic)
                    throw new Exception("The method " + m + " is used as an AmbientOS object provider, so it must be public and static.");
                Install(m);
            }
        }
    }

    public class ObjectProvider<TIn, TOut> : ObjectProvider
    {
        public ObjectProvider(Func<TIn, TOut> method)
            : base(method.GetMethodInfo())
        {
        }
    }
}
