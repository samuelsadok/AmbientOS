using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace AmbientOS
{
    /// <summary>
    /// The event handler type that is used to signal updates to the dynamic set.
    /// </summary>
    /// <param name="item">The item that was added/removed.</param>
    /// <param name="moreToFollow">If true, the handler should defer any expensive updates (such as UI refresh), as another call to the same handler will follow shortly.</param>
    public delegate void ItemHandler<T>(T item, bool moreToFollow);

    /// <summary>
    /// For a description, see the strongly typed class.
    /// </summary>
    public abstract class DynamicSet
    {
        public abstract void Add(object item, bool moreToFollow);
        public abstract void Remove(object item, bool moreToFollow);
        internal abstract void Flush();

        public abstract void Subscribe<T>(DynamicSet<T> subscriber);


        /// <summary>
        /// Creates a new dynamic set that represents the union of multiple other dynamic sets.
        /// </summary>
        public static TSet Union<TSet, TItem>(DynamicSet<TItem>[] sets)
            where TSet : DynamicSet<TItem>, new()
        {
            var setCount = sets.Count();

            var union = new TSet();

            // counts for each item the number of sets in which it is contained
            var relevantItems = new Dictionary<TItem, int>();

            ItemHandler<TItem> addedHandler = (item, moreToFollow) => {
                lock (relevantItems) {
                    if (relevantItems.Increment(item, 0, setCount) == 1)
                        union.Add(item, moreToFollow);
                }
            };

            ItemHandler<TItem> removeHandler = (item, moreToFollow) => {
                lock (relevantItems) {
                    if (relevantItems.Decrement(item, 0) == 0)
                        union.Remove(item, moreToFollow);
                }
            };

            Action flushHandler = union.Flush;

            foreach (var set in sets)
                set.AddSyncListeners(addedHandler, removeHandler, flushHandler);

            return union;
        }
    }


    /// <summary>
    /// Represents a set that enables clients to listen for "add" and "remove" events.
    /// Objects in a set are unordered and unique.
    /// Objects in the set are reference counted, which makes the set itself reference counted as well.
    /// All public instance members of this class are thread-safe.
    /// </summary>
    public class DynamicSet<T> : DynamicSet, IRefCounted
    {
        private readonly HashSet<T> content = new HashSet<T>();
        private readonly List<T> justAdded = new List<T>();
        private readonly List<T> justRemoved = new List<T>();

        private readonly AutoResetEvent dataChanged = new AutoResetEvent(false);

        private bool asyncThreadRunning = false;
        private readonly ItemHandler<T> asyncAddedListener;
        private readonly ItemHandler<T> asyncRemovedListener;

        private readonly List<ItemHandler<T>> syncAddedListeners = new List<ItemHandler<T>>();
        private readonly List<ItemHandler<T>> syncRemovedListeners = new List<ItemHandler<T>>();
        private readonly List<Action> syncFlushListeners = new List<Action>();


        public DynamicSet()
        {
        }

        /// <summary>
        /// Creates a new dynamic set and fills it with the specified items.
        /// </summary>
        public DynamicSet(params T[] initialItems)
        {
            foreach (var item in initialItems)
                content.Add(item);
        }

        /// <summary>
        /// Creates a new dynamic set and adds the specified async event listeners.
        /// These event listeners will never be called simultaneously.
        /// Instead, they are executed in a safe context in a separate thread while no lock is held, so they are free to take a long time and acquire any locks.
        /// If the handlers take a long time, they may miss items that are only in (or out of) the set for a short time.
        /// </summary>
        /// <param name="addedItemHandler">Called exactly once for every item that is added to the set. Can be null.</param>
        /// <param name="removedItemHandler">Called exactly once for every item that is removed from the set. Can be null.</param>
        /// <param name="controller">Can be used to terminate the event handler thread.</param>
        public DynamicSet(ItemHandler<T> addedItemHandler, ItemHandler<T> removedItemHandler, TaskController controller, params T[] initialItems)
            : this(initialItems)
        {
            asyncAddedListener = addedItemHandler;
            asyncRemovedListener = removedItemHandler;

            StartSerializer(controller);
        }


        /// <summary>
        /// Starts the serializer thread that invokes the async events.
        /// This does not have to be called if async events are not used.
        /// </summary>
        private void StartSerializer(TaskController controller)
        {
            var thread = new Thread(() => {
                while (true) {
                    T item = default(T);
                    bool remove = false, add = false, moreToFollow = false;

                    controller.WaitOne(dataChanged);

                    lock (content) {
                        if (add = justAdded.Any()) {
                            item = justAdded[0];
                            justAdded.RemoveAt(0);
                            moreToFollow = justAdded.Any();
                            dataChanged.Set();
                        } else if (remove = justRemoved.Any()) {
                            item = justRemoved[0];
                            justRemoved.RemoveAt(0);
                            moreToFollow = justRemoved.Any();
                            dataChanged.Set();
                        }
                    }

                    if (add) {
                        if (asyncAddedListener != null)
                            asyncAddedListener(item, moreToFollow);
                    } else if (remove) {
                        try {
                            if (asyncRemovedListener != null)
                                asyncRemovedListener(item, moreToFollow);
                        } finally {
                            this.DoIfReferenced(() => { (item as IRefCounted)?.Release(); });
                        }
                    }
                }
            });

            lock (content) {
                if (asyncThreadRunning)
                    throw new Exception("async event handler thread already running");

                foreach (var item in content)
                    justAdded.Add(item);

                thread.Start();
                asyncThreadRunning = true;
            }
        }

        /// <summary>
        /// Shall decide for a given item, if it should be included in this set.
        /// By default this always returns true.
        /// </summary>
        protected virtual bool ShouldAdd(T item)
        {
            return true;
        }

        /// <summary>
        /// Shall decide for a given item, if it should be removed from this set.
        /// By default, this always returns true.
        /// </summary>
        protected virtual bool ShouldRemove(T item)
        {
            return true;
        }

        /// <summary>
        /// Adds an item to the set (if it wasn't already added and if it passes the add-filter).
        /// If the item type implements IRefCounted and the item was not yet in the set, its reference count is incremented (except if this set itself has reference count of zero).
        /// </summary>
        /// <param name="item">The item to be added.</param>
        /// <param name="moreToFollow">Set to true if more items will be added immediately after this call. This is useful to defer expensive UI updates.</param>
        /// <returns>True if the item was not yet in the set, false otherwise.</returns>
        public bool Add(T item, bool moreToFollow)
        {
            lock (content) {
                bool add = ShouldAdd(item) ? content.Add(item) : false;

                if (add) {
                    if (asyncThreadRunning) {
                        justAdded.Add(item);
                        if (justRemoved.Remove(item))
                            this.DoIfReferenced(() => { (item as IRefCounted)?.Release(); });
                    }

                    this.DoIfReferenced(() => { (item as IRefCounted)?.Retain(); });

                    foreach (var subscriber in syncAddedListeners)
                        subscriber(item, moreToFollow);
                }

                if (!moreToFollow)
                    Flush();

                return add;
            }
        }

        /// <summary>
        /// Removes an item from the set (if it is in the set currently and if it passes the remove-filter).
        /// If the item type implements IRefCounted and the item was in the set, its reference count is usually decremented (except if this set itself has reference count of zero).
        /// However, if async events are enabled, the reference count decrement is deferred until after the async event.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        /// <param name="moreToFollow">Set to true if more items will be added immediately after this call. This is useful to defer expensive UI updates.</param>
        /// <returns>True if the item was in the set, false otherwise.</returns>
        public bool Remove(T item, bool moreToFollow)
        {
            lock (content) {
                bool remove = ShouldRemove(item) ? content.Remove(item) : false;

                if (remove) {
                    if (asyncThreadRunning) {
                        justRemoved.Add(item);
                        justAdded.Remove(item);
                    } else {
                        this.DoIfReferenced(() => { (item as IRefCounted)?.Release(); });
                    }

                    foreach (var subscriber in syncRemovedListeners)
                        subscriber(item, moreToFollow);
                }

                if (!moreToFollow)
                    Flush();

                return remove;
            }
        }

        /// <summary>
        /// For a description, see the strongly typed overload.
        /// </summary>
        public sealed override void Add(object item, bool moreToFollow)
        {
            Add((T)item, moreToFollow);
        }

        /// <summary>
        /// For a description, see the strongly typed overload.
        /// </summary>
        public sealed override void Remove(object item, bool moreToFollow)
        {
            Remove((T)item, moreToFollow);
        }

        /// <summary>
        /// Adds multiple items to the set.
        /// </summary>
        /// <param name="items">The collection of items to be added. Each item of the collection is evaluated lazily while no lock is held, so it's fine for the item generation to take a long time.</param>
        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
                Add(item, true);

            Flush();
        }

        /// <summary>
        /// Removes multiple items from the set.
        /// </summary>
        /// <param name="items">The collection of items to be removed. Each item of the collection is evaluated lazily while no lock is held, so it's fine for the item generation to take a long time.</param>
        public void RemoveRange(IEnumerable<T> items)
        {
            foreach (var item in items)
                Remove(item, true);

            Flush();
        }

        /// <summary>
        /// Makes up for updates that were erroneously deferred by setting "moreToFollow" to true even though there were no more items.
        /// </summary>
        internal sealed override void Flush()
        {
            lock (content) {
                foreach (var subscriber in syncFlushListeners)
                    subscriber();
                dataChanged.Set();
            }
        }

        /// <summary>
        /// Adds the specified subscriber to the set.
        /// This will immediately add all items in this set to the new subscriber (as long as it passes the filter).
        /// </summary>
        public override void Subscribe<T2>(DynamicSet<T2> subscriber)
        {
            if (subscriber == null)
                throw new ArgumentNullException($"{subscriber}");

            AddSyncListeners(
                (item, moreToFollow) => subscriber.Add(item, moreToFollow),
                (item, moreToFollow) => subscriber.Remove(item, moreToFollow),
                subscriber.Flush
                );
        }

        /// <summary>
        /// Adds the specified synchronous event listeners.
        /// These listeners are executed immediately when the event occurs.
        /// As such, they are dangerous because a private lock is held while they are invoked.
        /// When in doubt, use Subscribe instead.
        /// </summary>
        /// <param name="addedListener">Triggered for each item that is added to the dynamic set. Can be null.</param>
        /// <param name="removedListener">Triggered for each item that is removed from the dynamic set. Can be null.</param>
        /// <param name="flushListener">Triggered when there are no more items even though "moreToFollow" was true previously. Can be null.</param>
        internal void AddSyncListeners(ItemHandler<T> addedListener, ItemHandler<T> removedListener, Action flushListener)
        {
            lock (content) {
                if (addedListener != null) {
                    syncAddedListeners.Add(addedListener);

                    var enumerator = content.GetEnumerator();
                    var hasNext = enumerator.MoveNext();
                    while (hasNext) {
                        var item = enumerator.Current;
                        hasNext = enumerator.MoveNext();
                        addedListener(item, hasNext);
                    }
                }

                if (removedListener != null)
                    syncRemovedListeners.Add(removedListener);

                if (flushListener != null)
                    syncFlushListeners.Add(flushListener);
            }
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
                var result = e.MoveNext() ? default(T) : first;
                (result as IRefCounted)?.Retain();
                return result;
            }
        }

        /// <summary>
        /// Returns a snapshot of the dynamic set.
        /// If the item type implements IRefCounted, the reference count of each item is incremented.
        /// </summary>
        public T[] Snapshot()
        {
            T[] array;
            lock (content)
                array = content.ToArray();

            if (typeof(T).IsRefCounted())
                foreach (var item in array.Select(item => (IRefCounted)item))
                    item.Retain();

            return array;
        }

        public void Alloc()
        {
            if (typeof(T).IsRefCounted()) {
                lock (content) {
                    foreach (var item in content.Concat(justRemoved).Select(item => (IRefCounted)item))
                        item.Retain();
                }
            }
        }

        public void Free()
        {
            if (typeof(T).IsRefCounted()) {
                lock (content) {
                    foreach (var item in content.Concat(justRemoved).Select(item => (IRefCounted)item))
                        item.Release();
                }
            }
        }

        public void Dispose()
        {
            this.Release();
        }
    }



    public static class DynamicSetExtensions
    {
        public static int Increment<T>(this Dictionary<T, int> dict, T item, int minVal, int maxVal)
        {
            int value;
            if (!dict.TryGetValue(item, out value))
                value = minVal;

            if (++value > maxVal)
                throw new InvalidOperationException("value exceeded " + maxVal);

            return dict[item] = value;
        }

        public static int Decrement<T>(this Dictionary<T, int> dict, T item, int minVal)
        {
            int value;
            if (!dict.TryGetValue(item, out value))
                value = minVal;

            if (--value < minVal)
                throw new InvalidOperationException("value below " + minVal);

            if (value == minVal)
                dict.Remove(item);
            else
                dict[item] = value;

            return value;
        }

        public static TSet Union<TSet, TItem>(this DynamicSet<TItem> set, params DynamicSet<TItem>[] sets)
            where TSet : DynamicSet<TItem>, new()
        {
            return DynamicSet.Union<TSet, TItem>(sets.Concat(new DynamicSet<TItem>[] { set }).ToArray());
        }
    }
}
