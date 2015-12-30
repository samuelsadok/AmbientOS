using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmbientOS.Utils;

namespace AmbientOS.Environment
{
    public static class ApplicationRegistry
    {

        private class ActionStub : IActionImpl, ICustomAppearance
        {
            public IAction ActionRef { get; }
            ApplicationLifecycleManager app;
            MethodInfo method;
            AOSActionAttribute attr;
            Type inputType;
            Type outputType;

            public ActionStub(ApplicationLifecycleManager app, MethodInfo method, AOSActionAttribute attr)
            {
                ActionRef = new ActionRef(this);

                this.app = app;
                this.method = method;
                this.attr = attr;

                var refInfo = GetType().GetMethod("Invoke").GetParameters();
                var paramInfo = method.GetParameters();
                if (paramInfo.Count() != refInfo.Count())
                    throw new Exception(string.Format("method {0} has an invalid signature", method.ToString()));

                // this does not prevent later cast errors, but reduces them
                for (int i = 0; i < refInfo.Count(); i++)
                    if (!refInfo[i].ParameterType.IsAssignableFrom(paramInfo[i].ParameterType))
                        throw new Exception(string.Format("parameter {0} of method {1} must be assignable to {2}", i, method.ToString(), refInfo[i].ParameterType.ToString()));

                inputType = paramInfo[0].ParameterType;

                var returnType = method.ReturnType;

                if (returnType == typeof(void)) {
                    outputType = typeof(void);
                } else {
                    if (returnType.GetGenericTypeDefinition() != typeof(DynamicSet<>))
                        throw new Exception(string.Format("the method {0} must return a DynamicSet or void", method.ToString()));
                    if (!typeof(IObjectRef).IsAssignableFrom(returnType.GenericTypeArguments.Single()))
                        throw new Exception(string.Format("the DynamicSet returned by method {0} must consist of elements that are assignable to IObject", method.ToString()));

                    outputType = returnType.GenericTypeArguments.Single();
                }
            }

            public string GetVerb()
            {
                return attr.Verb;
            }

            public Type GetInputType()
            {
                return inputType;
            }

            public Type GetOutputType()
            {
                return outputType;
            }

            public string GetInputTypeName()
            {
                return ObjectStore.GetTypeName(inputType);
            }

            public string GetOutputTypeName()
            {
                return ObjectStore.GetTypeName(outputType);
            }

            //public IApplication GetApplication()
            //{
            //    return app;
            //}

            public DynamicSet<IObjectRef> Invoke(IObjectRef obj, Context context)
            {
                var set = (DynamicSet)method.Invoke(app.GetInstance(), new object[] { obj, context });

                if (outputType == typeof(void))
                    return new DynamicSet<IObjectRef>().Retain();

                var result = new DynamicSet<IObjectRef>().Retain();
                set.Subscribe(result);
                return result;
            }

            public void AddCustomAppearance(Dictionary<string, string> dict, Type type)
            {
                if (type == typeof(IAction))
                    foreach (var constraint in attr.Constraints.attributes)
                        dict["obj." + constraint.Key] = constraint.Value;
            }
        }


        private class ApplicationLifecycleManager
        {
            // todo: maybe we should make these kinds of classes static and then we can remove this instance creation

            private AOSServiceAttribute info;
            private Func<object> instanceConstructor;
            private List<object> runningInstances = new List<object>();

            public ApplicationLifecycleManager(Type type, Func<object> instanceConstructor)
            {
                info = (AOSServiceAttribute)type.GetCustomAttribute(typeof(AOSServiceAttribute));
                this.instanceConstructor = instanceConstructor;
            }

            public object GetInstance()
            {
                lock (runningInstances) { // todo: select application instance heuristically based on proximity
                    if ((info.MultipleInstances.HasValue ? info.MultipleInstances.Value : false) || !runningInstances.Any()) {
                        var instance = instanceConstructor();
                        runningInstances.Add(instance);
                    }
                    return runningInstances.First();
                }
            }
        }

        /// <summary>
        /// Installs the object actions available in the specified type.
        /// When another application on the system decides to make use of the service, a new instance of the type is created (if neccessary) and the specified intent is sent to a suitable instance.
        /// </summary>
        /// <param name="type">The type, of which the actions should be registered as object action.</typeparam>
        /// <param name="constructor">A function that constructs instances of the application. Maybe this will be removed if it seems sensible to make service classes static.</param>
        public static void InstallService(Type type, Func<object> constructor)
        {
            // Extract all method-attribute pairs in T, for which the attribute is an AOSActionAttribute.
            // If a method has multiple attributes, a tuple is returned for each of them.
            var actions = type.GetMethods().SelectMany(
                m => m.GetCustomAttributes(typeof(AOSActionAttribute), true)
                .Cast<AOSActionAttribute>()
                .Select(a => new { method = m, attribute = a })
                ).ToArray();

            var app = new ApplicationLifecycleManager(type, constructor);

            foreach (var a in actions) {
                var action = new ActionStub(app, a.method, a.attribute);
                ObjectStore.PublishObject(action.ActionRef.Retain());
            }

            // todo: add object listeners for auto-start actions
            //foreach (var a in distinctActions) {
            //    ObjectStore.FindObjects(a.attribute.ObjType, a.attribute.Constraints);
            //}
        }

        public static void InstallService(Type type)
        {
            var constructor = type.GetConstructor(new Type[0]);
            InstallService(type, () => constructor.Invoke(new object[0]));
        }

        public static void InstallService<T>()
        {
            InstallService(typeof(T));
        }
    }
}
