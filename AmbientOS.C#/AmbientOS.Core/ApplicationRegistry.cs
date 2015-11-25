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
            ApplicationStub app;
            MethodInfo method;
            AOSActionAttribute attr;
            Type inputType;
            Type outputType;

            public ActionStub(ApplicationStub app, MethodInfo method, AOSActionAttribute attr)
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
                var set = method.Invoke(app.GetInstance(), new object[] { obj, context });

                if (outputType == typeof(void))
                    return new DynamicSet<IObjectRef>().Retain();

                var result = new DynamicSet<IObjectRef>().Retain();
                result.Subscribe(set, o => (IObjectRef)o);
                return result;
            }

            public void AddCustomAppearance(Dictionary<string, string> dict, Type type)
            {
                if (type == typeof(IAction))
                    foreach (var constraint in attr.Constraints.attributes)
                        dict["obj." + constraint.Key] = constraint.Value;
            }
        }



        private abstract class ApplicationStub : IApplicationImpl
        {
            public abstract IApplication ApplicationRef { get; }
            public abstract void Load(Context context);
            public abstract object GetInstance();
        }


        private class ApplicationStub<T> : ApplicationStub
        {
            public override IApplication ApplicationRef { get; }
            private AOSApplicationAttribute info;
            private Func<T> instanceConstructor;
            private List<T> runningInstances = new List<T>();

            public ApplicationStub(Func<T> instanceConstructor)
            {
                ApplicationRef = new ApplicationRef(this);
                info = (AOSApplicationAttribute)typeof(T).GetCustomAttribute(typeof(AOSApplicationAttribute));
                this.instanceConstructor = instanceConstructor;
            }

            /*public IAOSObject Invoke(IAOSObject obj, string verb, string returnType, IShell shell, bool deliberate, LogContext logContext)
            {
                MethodInfo action = null;
                foreach (var type in ObjectStore.GetTypeNames(obj))
                    if (actions.TryGetValue(new Tuple<string, string>(verb, type), out action)) // first we look for the more specific action
                        break;

                if (action == null)
                    if (!actions.TryGetValue(new Tuple<string, string>(verb, null), out action)) // then for the more general (that accepts all types)
                        throw new AOSRejectException("The application \"" + this + "\" can't execute the verb " + verb + " on any interfaces of " + obj, verb, obj);


                return (IAOSObject)action.Invoke(instance, new object[] { obj, shell, deliberate, logContext });
            }*/

            public override object GetInstance()
            {
                lock (runningInstances) { // todo: select application instance heuristically based on proximity
                    if ((info.MultipleInstances.HasValue ? info.MultipleInstances.Value : false) || !runningInstances.Any()) {
                        var instance = instanceConstructor();
                        runningInstances.Add(instance);
                    }
                    return runningInstances.First();
                }
            }

            public override void Load(Context context)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Installs a class as an application.
        /// When another application on the system decides to make use of the application, a new instance is created (if neccessary) and the specified intent is sent to a suitable instance.
        /// </summary>
        /// <typeparam name="T">The class that should be registered as an application.</typeparam>
        /// <param name="constructor">A function that constructs instances of the application.</param>
        public static void InstallApp<T>(Func<T> constructor)
        {
            // Extract all method-attribute pairs in T, for which the attribute is an AOSActionAttribute.
            // If a method has multiple attributes, a tuple is returned for each of them.
            var actions = typeof(T).GetMethods().SelectMany(
                m => m.GetCustomAttributes(typeof(AOSActionAttribute), true)
                .Cast<AOSActionAttribute>()
                .Select(a => new { method = m, attribute = a })
                ).ToArray();

            // publish the app so that it can be found
            var app = new ApplicationStub<T>(constructor);
            ObjectStore.PublishObject(app.ApplicationRef.Retain());

            foreach (var a in actions) {
                var action = new ActionStub(app, a.method, a.attribute);
                ObjectStore.PublishObject(action.ActionRef.Retain());
            }

            // todo: add object listeners for auto-start actions
            //foreach (var a in distinctActions) {
            //    ObjectStore.FindObjects(a.attribute.ObjType, a.attribute.Constraints);
            //}
        }

        public static void InstallApp<T>()
        {
            var constructor = typeof(T).GetConstructor(new Type[0]);
            InstallApp(() => (T)constructor.Invoke(new object[0]));
        }
    }
}
