using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmbientOS.Environment;
using AmbientOS.Utils;

namespace AmbientOS
{

    /// <summary>
    /// Represents a domain of objects.
    /// </summary>
    public struct ObjectDomain
    {
        public readonly Guid Guid;
        public ObjectDomain(Guid guid)
        {
            Guid = guid;
        }

        /// <summary>
        /// The domain that contains only the local peer.
        /// </summary>
        public static ObjectDomain LocalDomain { get { return new ObjectDomain(new Guid()); } }
    }





    
    public class ObjectSet : DynamicSet<IObjectRef>
    {
        readonly ObjectAppearance constraints;

        public ObjectSet(ObjectAppearance constraints)
        {
            this.constraints = constraints;
        }

        protected override bool ShouldAdd(IObjectRef item)
        {
            return item.GetAppearance().CompliesTo(constraints);
        }
    }












    public class ObjectAppearance
    {
        /// <summary>
        /// A dictionary with attributes and their values.
        /// A value of null means is equivalent to a wildcard, e.g. all values apply.
        /// </summary>
        public Dictionary<string, string> attributes;

        public ObjectAppearance(Dictionary<string, string> attributes)
        {
            this.attributes = attributes;
        }

        public override string ToString()
        {
            return string.Join(", ", attributes.Select(a => a.Key + ": " + a.Value));
        }

        public bool CompliesTo(ObjectAppearance constraints)
        {
            return constraints.attributes.All(constraint => {
                string value;
                if (constraint.Value == null)
                    return true;
                if (!attributes.TryGetValue(constraint.Key, out value))
                    return true;
                return value == constraint.Value;
            });
        }
    }



    /// <summary>
    /// Emulates actions that should be taken by the OS
    /// </summary>
    public static partial class ObjectStore
    {
        /// <summary>
        /// Holds all objects of a specific type and keeps track of their attributes.
        /// </summary>
        private class AOSTypeEntry
        {
            //public List<AOSObject> objects;
            //public Dictionary<IObjectRef, List<ObjectAppearance>> objects = new Dictionary<IObjectRef, List<ObjectAppearance>>();
            public DynamicSet<IObjectRef> set = new DynamicSet<IObjectRef>().Retain();
            //public Dictionary<string, Tuple<List<AOSObject>, Dictionary<string, List<AOSObject>>>> attributes;
        }

        //static Dictionary<IAOSObject, AOSObjectEntry> objects = new Dictionary<IAOSObject, AOSObjectEntry>();
        static Dictionary<string, AOSTypeEntry> types = new Dictionary<string, AOSTypeEntry>();

        /// <summary>
        /// Publishes an object on the system, associating it with the specified attribute set.
        /// Note that the same object can have multiple attribute sets, so you can call this function on the same object multiple times.
        /// For now, please be nice and don't add the same object with the same type and appearance twice (it will still work though, just waste memory).
        /// </summary>
        public static void PublishObject(IObjectRef obj, ObjectAppearance appearance)
        {
            var type = obj.GetTypeName();

            AOSTypeEntry typeEntry;

            lock (types)
                if (!types.TryGetValue(type, out typeEntry))
                    types[type] = typeEntry = new AOSTypeEntry();

            lock (typeEntry)
                typeEntry.set.Add(obj, false);
        }

        //public static void PublishObject(IAOSObject obj, Dictionary<string, string> attributes)
        //{
        //    foreach (var type in GetTypes(obj))
        //        PublishObject(obj, type, new ObjectAppearance(attributes.Where(kv => kv.Key.StartsWith(type + ".")).ToDictionary(kv => kv.Key.Substring(type.Length + 1), kv => kv.Value)));
        //}


        /*
    public static void PublishObject<T>(IObjectImpl obj)
        where T : IObjectRef
    {
        var objRef = obj.Reference<T>();
        var appearance = objRef.GetAppearance();
        PublishObject(objRef, appearance);
    }
    */
        public static void PublishObject(IObjectRef objRef)
        {
            var appearance = objRef.GetAppearance();
            PublishObject(objRef, appearance);
        }

        /*
        /// <summary>
        /// Returns all appearances associated with a specific object and interface.
        /// Caution must be taken with the result, as it may be outdated immediately after it is returned.
        /// </summary>
        public static ObjectAppearance[] QueryAppearances(IObjectRef obj, Type type)
        {
            var result = QueryAppearances(obj, GetTypeName(type));
            if (result == null) // todo: remove this hack
                return new ObjectAppearance[] { obj.GetAppearance(type) };
            return result;
        }*/


        /*
        /// <summary>
        /// Returns all appearances associated with a specific object and interface.
        /// Caution must be taken with the result, as it may be outdated immediately after it is returned.
        /// </summary>
        public static ObjectAppearance[] QueryAppearances(IObjectRef obj, string type)
        {
            AOSTypeEntry typeEntry;
            List<ObjectAppearance> appearances;

            lock (types)
                if (!types.TryGetValue(type, out typeEntry))
                    return null;

            lock (typeEntry) {
                if (!typeEntry.objects.TryGetValue(obj, out appearances))
                    return null;
                return appearances.ToArray();
            }
        }
        */

        public static ObjectSet FindObjects(Type type, ObjectAppearance constraints)
        {
            var typeName = GetTypeName(type);
            var set = new ObjectSet(constraints);

            AOSTypeEntry typeEntry;

            lock (types)
                if (!types.TryGetValue(typeName, out typeEntry))
                    types[typeName] = typeEntry = new AOSTypeEntry();

            lock (typeEntry)
                set.Subscribe(typeEntry.set);

            return set.Retain();
        }



        /*
        /// <summary>
        /// Returns a list of objects that satisfy a list of attribute constraints.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="constraints">Specifies a list of attribute constraints. An object must satisfy all of these constraints to be included in the result. Constraints with a value of null are ignored. Can be null.</param>
        public static IEnumerable<IAOSObject> FindObjects(string type, Dictionary<string, string> constraints)
        {
            AOSTypeEntry typeEntry;

            lock (types) {
                if (!types.TryGetValue(type, out typeEntry))
                    return Enumerable.Empty<IAOSObject>();
            }

            if (constraints == null)
                constraints = new Dictionary<string, string>();

            lock (typeEntry) {
                return (from o in typeEntry.objects
                        where constraints.All(specifier => {
                            string value;
                            if (specifier.Value == null)
                                return true;
                            if (!o.Item2.attributes.TryGetValue(specifier.Key, out value))
                                return true;
                            return value == specifier.Value;
                        })
                        select o.Item1).Distinct().ToArray();
            }
        }

        public static IEnumerable<T> FindObjects<T>(Dictionary<string, string> constraints) where T : class
        {
            var type = typeof(T).GetCustomAttribute<AOSTypeAttribute>(true).TypeName;
            return FindObjects(type, constraints).Select(obj => (obj as T)).Where(obj => obj != null);
        }

        /// <summary>
        /// This function allows to search for objects while specifying multiple sets of constraints.
        /// An object is returned, if any one of these sets apply.
        /// </summary>
        public static IEnumerable<T> FindObjects<T>(IEnumerable<Dictionary<string, string>> constraints) where T : class
        {
            return constraints.SelectMany(c => FindObjects<T>(c)).Distinct();
        } */

        /// <summary>
        /// Dumps the object and type registry contents.
        /// </summary>
        public static void Dump(LogContext logContext)
        {
            logContext.Log("types:");
            lock (types) {
                foreach (var type in types) {
                    foreach (var obj in type.Value.set.Snapshot()) {
                        foreach (var appearance in obj.GetAppearance().attributes)
                            logContext.Log("[" + obj + "." + type.Key + "] " + appearance);
                    }
                }
            }
        }
    }

    public static partial class ObjectStore
    {
        static DynamicGraph<string, IObjectRef> graph =
            new DynamicGraph<string, IObjectRef>(
                FindObjects(typeof(IAction), new ObjectAppearance(new Dictionary<string, string>())),
                edge => ((IAction)edge).GetInputTypeName(),
                edge => ((IAction)edge).GetOutputTypeName());


        //public static string[] GetTypeNames<T>()
        //{
        //    var ifs = typeof(T).GetInterfaces().ToArray();
        //    var ats = ifs.Select(i => i.GetCustomAttribute<AOSTypeAttribute>()).ToArray();
        //    ats = ats.Where(attr => attr != null).ToArray();
        //    var sts = ats.Select(attr => attr.TypeName).ToArray();
        //
        //    return typeof(T).GetInterfaces().Select(i => i.GetCustomAttribute<AOSTypeAttribute>()).Where(attr => attr != null).Select(attr => attr.TypeName).ToArray();
        //}

        public static bool IsAOSType(Type type)
        {
            return type.GetCustomAttribute<AOSInterfaceAttribute>() != null;
        }

        public static string GetTypeName<T>()
        {
            return GetTypeName(typeof(T));
        }

        public static string GetTypeName(Type type)
        {
            if (type == typeof(void))
                return "void";

            if (!IsAOSType(type))
                throw new Exception(string.Format("The type {0} is no AmbientOS interface descriptor.", type));
            return type.GetCustomAttribute<AOSInterfaceAttribute>().TypeName;
        }

        public static Type[] GetTypes(IObjectRef obj)
        {
            return obj.GetType().GetInterfaces().Where(i => i.CustomAttributes.Any(attr => typeof(AOSInterfaceAttribute).IsAssignableFrom(attr.AttributeType))).ToArray();
        }


        /// <summary>
        /// Returns a list of all the apps that can execute a given verb on a given object.
        /// </summary>
        public static DynamicSet<IObjectRef> GetHandlers(IObjectRef obj, string verb)
        {
            return new DynamicUnionSet<IObjectRef>(GetTypes(obj).Select(type => GetHandlers(obj, type, verb)).ToArray()).Retain();
        }


        /// <summary>
        /// Returns a list of all the actions that can handle a given verb on a given object with a given type.
        /// </summary>
        public static DynamicSet<IObjectRef> GetHandlers(IObjectRef obj, Type type, string verb)
        {
            var dict = obj.GetAppearance().attributes.ToDictionary(a => "obj." + a.Key, a => a.Value);
            dict["verb"] = verb;
            dict["input"] = obj.GetTypeName();

            return FindObjects(typeof(IAction), new ObjectAppearance(dict));
        }


        public static DynamicSet<IObjectRef> Action(IObjectRef obj, string verb, IObjectRef[] actions, Context context)
        {
            var rejections = new List<AOSRejectException>();

            foreach (var action in actions.Select(app => (IAction)app)) {
                try {
                    return action.Invoke(obj, context);
                } catch (AOSRejectException ex) {
                    rejections.Add(ex);
                    continue;
                }
            }

            if (!actions.Any())
                throw new AOSAppNotFoundException(verb, obj);

            throw new AOSAppNotFoundException(verb, obj, rejections);
        }

        /// <summary>
        /// Executes a series of actions until either we end up with a set that contains exactly one element of the requested output type or whatever.
        /// </summary>
        public static TOut Action<TIn, TOut>(TIn obj, Context context)
            where TIn : IObjectRef
            where TOut : IObjectRef
        {
            IObjectRef o = obj.Retain();

            var invocationPlan = graph.FindPath(GetTypeName<TIn>(), GetTypeName<TOut>());

            if (invocationPlan == null)
                throw new Exception(string.Format("no combination of the installed apps allows to obtain type {1} from type {0}", GetTypeName<TIn>(), GetTypeName<TOut>()));

            try {
                foreach (var step in invocationPlan.Select(edge => (IAction)edge)) {
                    if (!step.CanHandle(o)) // todo: fix (and add an "excludePath" or something to the path finder)
                        throw new NotImplementedException("I did something wrong (the object doesn't look as expected). I'd have to reconsider the route from here or take some steps back.");

                    using (var actionResult = step.Invoke(o, context)) {
                        o.Release();
                        o = actionResult.AsSingle();
                    }
                    if (o == null) // todo: fix (same as above)
                        throw new NotImplementedException("I did something wrong (the action did not return one single object). I'd have to reconsider the route from here or take some steps back.");
                }
            } finally {
                foreach (var item in invocationPlan)
                    item.Release();
            }

            return (TOut)o;

            //while (true) {
            //    var actionResult = Action(o, verb, GetHandlers(obj, oType, verb).Snapshot(), shell, logContext);
            //    o = actionResult.AsSingle();
            //    oType = o.GetType();
            //    if (o == null)
            //        return default(TOut);
            //    else if (typeof(TOut).IsAssignableFrom(oType))
            //        return (TOut)o;
            //}
        }

        /// <summary>
        /// Finds the best application for the specified action on the provided object and uses the application to execute the action.
        /// If no suitable application is found or all applications reject the action, an error message is presented on the shell (currently, only an exception is thrown).
        /// The function returns the topmost single object that is generated by this action (e.g. for a disk with a single volume, this would be the filesystem).
        /// </summary>
        public static DynamicSet<IObjectRef> Action(IObjectRef obj, string verb, Context context)
        {
            var snapshot = GetHandlers(obj, verb).Snapshot();
            var result = Action(obj, verb, snapshot, context);
            foreach (var item in snapshot)
                item.Release();
            return result;
        }
    }
}
