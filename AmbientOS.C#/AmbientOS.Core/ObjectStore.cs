using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS
{
    public abstract class ObjectStore
    {
        public abstract void Publish(IObjectImpl implementation);
        public abstract IObjectRef GetReference(IObjectImpl implementation);
        public abstract ObjectSet FindObjects(ObjectConstraints constraints);

        /// <summary>
        /// Publishes object references for all interfaces that the implementation implements.
        /// </summary>
        public static void PublishObject(IObjectImpl implementation)
        {
            var interfaces = implementation.GetType().GetInterfaces()
                .Select(i => i.GetCustomAttribute<AOSInterfaceAttribute>())
                .Where(t => t != null)
                .Distinct()
                .ToArray();

            foreach (var i in interfaces) {
                i.Store.Publish(implementation);
            }

            // todo: attach or initialize all root references
        }

        public static ObjectSet FindObjects(Type type, ObjectConstraints constraints)
        {
            var attr = type.GetCustomAttribute<AOSInterfaceAttribute>();
            return attr.Store.FindObjects(constraints);
        }
    }

    public class ObjectStore<TImpl, TRef> : ObjectStore
        where TImpl : class, IObjectImpl
        where TRef : class, IObjectRef
    {
        private ConditionalWeakTable<TImpl, TRef> references = new ConditionalWeakTable<TImpl, TRef>();
        private ConditionalWeakTable<TImpl, TRef>.CreateValueCallback refCtor;

        private DynamicSet<TRef> localReferences = new DynamicSet<TRef>();

        public ObjectStore(ConditionalWeakTable<TImpl, TRef>.CreateValueCallback refCtor)
        {
            this.refCtor = refCtor;
        }

        public override void Publish(IObjectImpl implementation)
        {
            localReferences.Add((TRef)GetReference(implementation), false);
        }

        public override IObjectRef GetReference(IObjectImpl implementation)
        {
            return references.GetValue((TImpl)implementation, refCtor);
        }

        public override ObjectSet FindObjects(ObjectConstraints constraints)
        {
            var result = new ObjectSet(constraints);
            localReferences.Subscribe(result);
            return result;
        }
    }
}
