using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.Utils
{
    /// <summary>
    /// Represents a set that enables clients to listen for add and remove events.
    /// Objects in a set are unordered and unique.
    /// Objects in the set are reference counted, which makes the set reference counted as well.
    /// </summary>
    public class DynamicSet<T> : IRefCounted
        where T : IRefCounted
    {
        private HashSet<T> content = new HashSet<T>();
        private List<Action<T, bool>> addedItemHandlers = new List<Action<T, bool>>();
        private List<Action<T, bool>> removedItemHandlers = new List<Action<T, bool>>();

        public DynamicSet(params T[] initialItems)
        {
            foreach (var item in initialItems)
                content.Add(item);
        }

        /// <summary>
        /// Can be used to directly access the current content.
        /// The returned set must not be modified directly and must be locked for any read access.
        /// For a given instance, this always returns the same value.
        /// </summary>
        internal HashSet<T> GetContent()
        {
            return content;
        }

        /// <summary>
        /// Shall decide for a given item, if it should be included in this set.
        /// By default this returns always true.
        /// </summary>
        protected virtual bool ShouldAdd(T item)
        {
            return true;
        }

        /// <summary>
        /// Shall decide for a given item, if it should be removed from this set.
        /// By default, this returns always true.
        /// </summary>
        protected virtual bool ShouldRemove(T item)
        {
            return true;
        }


        T lastItemAdded, lastItemRemoved;
        bool lastItemAddedValid = false, lastItemRemovedValid = false;

        /// <summary>
        /// Adds an item to the set if the filter applies and if it's not already in the set.
        /// If moreToFollow is true, the item will not be added immediately, but only on the next Add call.
        /// If the item is removed again during exactly that time period, it will never truly be added.
        /// This delay is neccessary because the set must tell the listeners if there will really follow more items.
        /// If the item was not yet in the set, its reference count is incremented (except if this set has reference count of zero) and the function returns true.
        /// </summary>
        /// <param name="moreToFollow">Set to true if more items will be added immediately after this call. This is useful to suppress UI updates.</param>
        public bool Add(T item, bool moreToFollow)
        {
            lock (content) {
                bool add = ShouldAdd(item);

                if (add)
                    if ((add = content.Add(item)))
                        this.DoIfReferenced(() => { item.Retain(); });

                if (add && lastItemRemovedValid && item.Equals(lastItemRemoved)) {
                    lastItemRemovedValid = false;
                    add = false;
                }

                if (!add && moreToFollow)
                    return false;

                if (lastItemAddedValid) {
                    foreach (var listener in addedItemHandlers)
                        listener(lastItemAdded, add);
                    lastItemAddedValid = false;
                }

                if (add) {
                    if (moreToFollow) {
                        lastItemAdded = item;
                        lastItemAddedValid = true;
                    } else {
                        foreach (var listener in addedItemHandlers)
                            listener(item, false);
                    }
                }

                return add;
            }
        }

        /// <summary>
        /// Removes an item from the set if it was in the set previously.
        /// If the item was in the set, its reference count is decremented (except if this set has reference count of zero) and the function returns true.
        /// </summary>
        /// <param name="moreToFollow">Set to true if more items will be removed immediately after this call. This is useful to suppress UI updates.</param>
        public bool Remove(T item, bool moreToFollow)
        {
            lock (content) {
                bool remove = ShouldRemove(item);

                if (remove)
                    if ((remove = content.Remove(item)))
                        this.DoIfReferenced(() => { item.Release(); });

                if (remove && lastItemAddedValid && item.Equals(lastItemAdded)) {
                    lastItemAddedValid = false;
                    remove = false;
                }

                if (!remove && moreToFollow)
                    return false;

                if (lastItemRemovedValid) {
                    foreach (var listener in removedItemHandlers)
                        listener(lastItemRemoved, remove);
                    lastItemRemovedValid = false;
                }

                if (remove) {
                    if (moreToFollow) {
                        lastItemRemoved = item;
                        lastItemRemovedValid = true;
                    } else {
                        foreach (var listener in removedItemHandlers)
                            listener(item, false);
                    }
                }

                return remove;
            }
        }


        /// <summary>
        /// Subscribes this set to another dynamic set.
        /// This will immediately add all of the other set's items to this set (as long as they pass this set's filter).
        /// </summary>
        /// <param name="converter">Shall convert an item from the input type to the output type. If called multiple times for any given item, this should always return items that are equal by their default equality comparer.</param>
        public void Subscribe<TIn>(DynamicSet<TIn> set, Func<TIn, T> converter)
            where TIn : IRefCounted
        {
            Action<TIn, bool> add = (item, moreToFollow) => {
                using (var converted = converter(item))
                    Add(converted, moreToFollow);
            };
            Action<TIn, bool> remove = (item, moreToFollow) => {
                using (var converted = converter(item))
                    Remove(converted, moreToFollow);
            };
            set.AddListeners(add, remove);
        }

        /// <summary>
        /// Subscribes this set to another dynamic set of which the item type is not known at compile time.
        /// This will immediately add all of the other set's items to this set (as long as they pass this set's filter).
        /// </summary>
        /// <param name="converter">Shall convert an item from the input type to the output type. If called multiple times for any given item, this should always return items that are equal by their default equality comparer.</param>
        public void Subscribe(object set, Func<object, T> converter)
        {
            if (set.GetType().GetGenericTypeDefinition() != typeof(DynamicSet<>))
                throw new ArgumentException("expected argument of type DynamicSet", $"{set}");

            Action<object, bool> add = (item, moreToFollow) => {
                using (var converted = converter(item))
                    Add(converted, moreToFollow);
            };
            Action<object, bool> remove = (item, moreToFollow) => {
                using (var converted = converter(item))
                    Remove(converted, moreToFollow);
            };

            set.GetType().GetMethod("AddListeners", new Type[] { typeof(Action<object, bool>), typeof(Action<object, bool>) }).Invoke(set, new object[] { add, remove });
        }

        /// <summary>
        /// Subscribes this set to another dynamic set.
        /// This will immediately add all of the other set's items to this set (as long as they pass this set's filter).
        /// </summary>
        public void Subscribe(DynamicSet<T> set)
        {
            Subscribe(set, (item) => item);
        }


        /// <summary>
        /// Adds the specified listeners to the set.
        /// </summary>
        /// <param name="addedItemHandler">Invoked when a new item is added to the set. This is guaranteed to be called only for objects that aren't already in the set. Can be null. Else, the handler is guaranteed to be invoked for each item that is already in the list.</param>
        /// <param name="removedItemHandler">Invoked when an item is removed to the set. This is guaranteed to be called only for objects that are in the set. Can be null.</param>
        public void AddListeners(Action<T, bool> addedItemHandler, Action<T, bool> removedItemHandler)
        {
            if (addedItemHandler == null && removedItemHandler == null)
                return;

            lock (content) {
                if (removedItemHandler != null)
                    removedItemHandlers.Add(removedItemHandler);

                if (addedItemHandler != null) {
                    addedItemHandlers.Add(addedItemHandler);

                    var enumerator = content.GetEnumerator();
                    var hasNext = enumerator.MoveNext();
                    while (hasNext) {
                        var item = enumerator.Current;
                        hasNext = enumerator.MoveNext();
                        addedItemHandler(item, hasNext);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the specified weakly typed listeners to the set.
        /// </summary>
        private void AddListeners(Action<object, bool> addedItemHandler, Action<object, bool> removedItemHandler)
        {
            Action<T, bool> add = (item, moreToFollow) => addedItemHandler(item, moreToFollow);
            Action<T, bool> remove = (item, moreToFollow) => removedItemHandler(item, moreToFollow);
            AddListeners(add, remove);
        }

        /// <summary>
        /// Indicates whether the set contains a particular item.
        /// This should be used with caution, since the item could be removed or added immediately after the call, so the caller will have an outdated result.
        /// </summary>
        public bool Contains(T item)
        {
            lock (content)
                return content.Contains(item);
        }

        /// <summary>
        /// Returns the single item of this set.
        /// Returns the default value if the number of elements is zero or larger than one.
        /// </summary>
        public T AsSingle()
        {
            lock (content) {
                var e = content.GetEnumerator();
                if (!e.MoveNext())
                    return default(T);
                var first = e.Current;
                return e.MoveNext() ? default(T) : first.Retain();
            }
        }

        /// <summary>
        /// Returns a snapshot of the dynamic set.
        /// The reference count of each item is incremented.
        /// </summary>
        public T[] Snapshot()
        {
            lock (content)
                return content.Select(item => item.Retain()).ToArray();
        }

        public void Alloc()
        {
            lock (content)
                foreach (var item in content)
                    item.Retain();
        }

        public void Free()
        {
            lock (content)
                foreach (var item in content)
                    item.Release();
        }

        public void Dispose()
        {
            this.Release();
        }
    }


    /// <summary>
    /// Creates a dynamic set that represents the union of two other dynamic sets.
    /// </summary>
    public sealed class DynamicUnionSet<T> : DynamicSet<T>
        where T : IRefCounted
    {
        private DynamicSet<T>[] sets;

        public DynamicUnionSet(params DynamicSet<T>[] sets)
        {
            this.sets = sets;
            foreach (var set in sets)
                Subscribe(set);
        }

        protected override bool ShouldAdd(T item)
        {
            return sets.Any(set => set.Contains(item));
        }

        protected override bool ShouldRemove(T item)
        {
            return !ShouldAdd(item);
        }
    }


    /// <summary>
    /// Creates a dynamic set that represents the intersection of two other dynamic sets.
    /// If an item is removed from one set and then quickly added to the other set, there is the possibility of the item to shortly appear in this intersection.
    /// </summary>
    public sealed class DynamicIntersectionSet<T> : DynamicSet<T>
        where T : IRefCounted
    {
        private DynamicSet<T>[] sets;

        public DynamicIntersectionSet(params DynamicSet<T>[] sets)
        {
            this.sets = sets;
            foreach (var set in sets)
                Subscribe(set);
        }

        protected override bool ShouldAdd(T item)
        {
            return sets.All(set => set.Contains(item));
        }

        protected override bool ShouldRemove(T item)
        {
            return !ShouldAdd(item);
        }
    }
}
