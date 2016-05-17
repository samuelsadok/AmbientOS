using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static AmbientOS.TaskController;

namespace AmbientOS
{
    /// <summary>
    /// Represents a queue that is specifically designed for multithreading requirements.
    /// If can for instance be used as a producer-consumer queue.
    /// The queue has a maximum size.
    /// All public members of this class are thread-safe.
    /// </summary>
    public class DynamicQueue<T>
    {
        private object lockRef = new object();

        private T[] items = new T[0];

        /// <summary>
        /// Points to the next index to read from minus 1 (may be outside array bounds).
        /// If this is equal to the write pointer, the queue is full.
        /// If it's equal to the write pointer minus one, the queue is empty.
        /// </summary>
        private int readPtr = -1;

        /// <summary>
        /// Points to the next index to write to.
        /// </summary>
        private int writePtr = 0;

        /// <summary>
        /// Returns the maximum number of elements the queue can hold.
        /// The actual allocated size of the queue may be less.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// Keeps track of the number of free slots in the queue.
        /// </summary>
        private readonly Semaphore freeSlots;

        /// <summary>
        /// Keeps track of the number of valid items in the queue.
        /// </summary>
        private readonly Semaphore usedSlots;

        /// <summary>
        /// Creates a producer-consumer queue with the specified capacity.
        /// Enqueue attempts will block while the queue is full.
        /// </summary>
        /// <param name="allocatedSize">The initially allocated size of the queue. If the queue exceeds this size, it grows automatically up to the capacity. Must not be larger than capacity.</param>
        public DynamicQueue(int capacity, int allocatedSize)
        {
            if (allocatedSize > capacity)
                throw new ArgumentException("The allocated size must not be larger than the capacity.", $"{allocatedSize}");

            freeSlots = new Semaphore(capacity, capacity);
            usedSlots = new Semaphore(0, capacity);

            Capacity = capacity;
            items = new T[allocatedSize];
        }

        /// <summary>
        /// Creates a producer-consumer queue with the specified capacity.
        /// The entire queue will be allocated on construction.
        /// </summary>
        public DynamicQueue(int capacity)
            : this(capacity, capacity)
        {
        }

        /// <summary>
        /// Creates a producer-consumer queue of arbitrary capacity.
        /// </summary>
        public DynamicQueue()
            : this(int.MaxValue, 0)
        {
        }


        /// <summary>
        /// Returns the current number of elements in the queue.
        /// This method is not thread-safe.
        /// </summary>
        private int CountUnsafe()
        {
            if (readPtr + 1 > writePtr)
                return writePtr + Capacity - readPtr;
            else
                return writePtr - readPtr - 1;
        }

        /// <summary>
        /// Returns the current number of elements in the queue.
        /// </summary>
        public int Count()
        {
            lock (lockRef) {
                return CountUnsafe();
            }
        }

        /// <summary>
        /// Determines whether the specified item is contained in the queue.
        /// This method is not thread-safe.
        /// </summary>
        private bool ContainsUnsafe(T item)
        {
            var count = CountUnsafe();
            for (int i = 0; i < count; i++)
                if (Equals(items[(readPtr + 1 + i) % Capacity], item))
                    return true;
            return false;
        }

        /// <summary>
        /// Determines whether the specified item is contained in the queue.
        /// </summary>
        public bool Contains(T item)
        {
            lock (lockRef) {
                return ContainsUnsafe(item);
            }
        }

        /// <summary>
        /// Returns the number of distinct elements in the queue.
        /// </summary>
        public int DistinctCount()
        {
            lock (lockRef) {
                return GetItems().Distinct().Count();
            }
        }

        /// <summary>
        /// Returns true if every element in the queue is different.
        /// </summary>
        private bool IsSet()
        {
            var items = GetItems().ToArray();
            var count1 = items.Count();
            var count2 = items.Distinct().Count();
            return items.Count() == items.Distinct().Count();
        }

        /// <summary>
        /// Returns an array that contains all of the items that are currently in the queue.
        /// The first item represents the oldest item.
        /// Neither the method nor its lazily evaluation is not thread-safe.
        /// </summary>
        private IEnumerable<T> GetItems()
        {
            var count = CountUnsafe();
            for (int i = 0; i < count; i++)
                yield return items[(readPtr + 1 + i) % Capacity];
        }

        /// <summary>
        /// Renews the queue by filling it with the specified items.
        /// This method is not thread-safe and does not update metrics of the queue.
        /// </summary>
        /// <param name="count">The number of items that should be set. This will become the effective length of the list.</param>
        /// <param name="itemProvider">Shall return an item for each position in the list. Index 0 denotes the oldest item.</param>
        private void Fill(int count, Func<int, T> itemProvider)
        {
            if (count > items.Count())
                items = new T[count];

            for (int i = 0; i < count; i++)
                items[i] = itemProvider(i);

            readPtr = -1;
            writePtr = count % Capacity;
        }

        /// <summary>
        /// Enqueues a new item and dequeues the oldest item if the limit is exceeded.
        /// This method is not thread-safe.
        /// </summary>
        /// <param name="adjustSlots">If set to false, this does not adjust the slot counting semaphores.</param>
        private void StrongEnqueueUnsafe(T item, bool adjustSlots = true)
        {
            if (adjustSlots)
                adjustSlots = freeSlots.WaitOne(0); // if the queue is full, this will not block, else it will decrement the number of free slots

            if (Capacity == 0)
                return;

            while (writePtr >= items.Count())
                Array.Resize(ref items, Math.Max(items.Count() * 2, 1));

            items[writePtr] = item;
            writePtr = (writePtr + 1) % Capacity;

            if ((readPtr + 1) % Capacity == writePtr)
                readPtr = writePtr;

            UpdateMostCommonItemIfNotEqual(item);

            if (adjustSlots)
                usedSlots.Release();
        }

        /// <summary>
        /// Enqueues a new item and dequeues the oldest item if the limit is exceeded.
        /// </summary>
        public void StrongEnqueue(T item)
        {
            lock (lockRef) {
                StrongEnqueueUnsafe(item);
            }
        }

        /// <summary>
        /// Enqueues a new item only if it's not already in the queue.
        /// If the item is enqueued and the queue was full, the last item is dequeued.
        /// Returns true iif the item was newly added.
        /// </summary>
        public bool StrongEnqueueDistinct(T item)
        {
            lock (lockRef) {
                if (ContainsUnsafe(item))
                    return false;

                StrongEnqueueUnsafe(item);
                return true;
            }
        }

        /// <summary>
        /// Enqueues an item and signals one of the blocked consumers (if any).
        /// This method blocks while the queue is full.
        /// </summary>
        public void WaitEnqueue(T item)
        {
            Wait(freeSlots);
            StrongEnqueueUnsafe(item, false);
            usedSlots.Release();
        }

        /// <summary>
        /// Enqueues a new item only if it's not already in the queue, and if the queue is not full.
        /// Returns true if the item is in the queue after the call.
        /// This method is not thread-safe.
        /// </summary>
        private bool WeakEnqueueUnsafe(T item)
        {
            if (ContainsUnsafe(item)) {
                return true;
            } else if (CountUnsafe() < Capacity) {
                StrongEnqueueUnsafe(item);
                return true;
            } else {
                return false;
            }
        }

        /// <summary>
        /// Enqueues a new item only if it's not already in the queue, and if the queue is not full.
        /// Returns true if the item is in the queue after the call.
        /// </summary>
        public bool WeakEnqueue(T item)
        {
            lock (lockRef) {
                return WeakEnqueueUnsafe(item);
            }
        }

        /// <summary>
        /// Replaces the oldest item in the queue that is larger by some metric (if neccessary).
        /// If the queue is not full, the item is enqueued unconditionally (except if it's already in the queue).
        /// If there are no larger items, the new item is ignored.
        /// Returns true if the item is in the queue after the call.
        /// </summary>
        /// <param name="item">The new item</param>
        /// <param name="comparator">Should return a negative value if the first argument smaller than the second value, a positive value in the opposite case and zero if they are equal.</param>
        public bool ReplaceLarger(T item, Func<T, T, int> comparator)
        {
            lock (lockRef) {
                if (WeakEnqueueUnsafe(item))
                    return true;

                var validItems = GetItems();

                foreach (var itemAndIndex in GetItems().Select((it, index) => new { item = it, index = index })) {
                    if (comparator(item, itemAndIndex.item) < 0) {
                        items[(readPtr + 1 + itemAndIndex.index) % Capacity] = item;
                        UpdateMostCommonItemIfNotEqual(item);
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Replaces the largest item in the queue if it's larger than the new item by some metric (if neccessary).
        /// If the queue is not full, the new item is enqueued unconditionally (except if it's already in the queue).
        /// If there are no larger items, the new item is ignored.
        /// Returns true if the item is in the queue after the call.
        /// </summary>
        /// <param name="item">The new item</param>
        /// <param name="comparator">Should return a negative value if the first argument smaller than the second value, a positive value in the opposite case and zero if they are equal.</param>
        public bool ReplaceLargest(T item, Func<T, T, int> comparator)
        {
            lock (lockRef) {
                if (WeakEnqueueUnsafe(item))
                    return true;

                var validItems = GetItems().ToArray();

                int largestIndex = -1;
                for (int i = 0; i < validItems.Count(); i++)
                    if (comparator(largestIndex == -1 ? item : validItems[largestIndex], validItems[i]) < 0)
                        largestIndex = i;

                if (largestIndex != -1) {
                    items[(readPtr + 1 + largestIndex) % Capacity] = item;
                    UpdateMostCommonItemIfNotEqual(item);
                    return true;
                } else {
                    return false;
                }
            }
        }

        /// <summary>
        /// Dequeues the oldest item from the queue.
        /// </summary>
        private T DequeueUnsafe()
        {
            if (readPtr + 1 == writePtr)
                throw new InvalidOperationException("The queue is empty");
            var item = items[++readPtr];
            if (readPtr + 1 >= Capacity)
                readPtr -= Capacity;
            return item;
        }

        /// <summary>
        /// Dequeues the oldest item from the queue.
        /// If the queue is empty, the method returns false and the item is set to the default value.
        /// </summary>
        public bool TryDequeue(out T item)
        {
            if (!usedSlots.WaitOne(0)) {
                item = default(T);
                return false;
            }

            lock (lockRef) {
                item = DequeueUnsafe();
            }

            freeSlots.Release();
            return true;
        }

        /// <summary>
        /// Dequeues the oldest item from the queue.
        /// If the queue is empty, the method throws an exception.
        /// </summary>
        public T Dequeue()
        {
            T item;
            if (!TryDequeue(out item))
                throw new InvalidOperationException("The queue is empty");
            return item;
        }

        /// <summary>
        /// Dequeues the oldest item from the queue.
        /// If neccessary, the call blocks until an item becomes available.
        /// </summary>
        public T WaitDequeue()
        {
            Wait(usedSlots);

            T item;
            lock (lockRef) {
                item = DequeueUnsafe();
            }

            freeSlots.Release();
            return item;
        }

        /// <summary>
        /// Tries to remove a specific item from the queue.
        /// If the item occurs multiple times, only the oldest one is removed.
        /// Returns true if the item was in the queue.
        /// </summary>
        public bool Dequeue(T item)
        {
            lock (lockRef) {
                var oldItems = GetItems();
                if (!oldItems.Contains(item))
                    return false;

                var oldItemsArray = oldItems.ToArray();
                var obsoleteIndex = Array.IndexOf(oldItemsArray, item);

                Fill(oldItemsArray.Count() - 1, i => oldItemsArray[i < obsoleteIndex ? i : (i + 1)]);
                UpdateMostCommonItemIfEqual(oldItemsArray[obsoleteIndex]);

                usedSlots.WaitOne(0); // this should always succeed at this point
                freeSlots.Release();

                return true;
            }
        }


        // todo: maybe use a separate class to calculate the metrics
        #region Queue Metrics

        /// <summary>
        /// Stores the element that currently occurs most often in the queue.
        /// </summary>
        private T lastMostCommonElement = default(T);

        /// <summary>
        /// Triggered when the most common element in the queue changed.
        /// If the queue is empty, the default value of T is the most common element.
        /// </summary>
        public event Action<T> MostCommonElementChanged;

        /// <summary>
        /// Refreshes the most common item information.
        /// Call this whenever the queue changed in such a way that this value might have changed.
        /// This method is not thread-safe.
        /// </summary>
        private void UpdateMostCommonItem()
        {
            T newMostCommonElement;
            if (!TryGetMostCommonItem(out newMostCommonElement))
                newMostCommonElement = default(T);
            if (newMostCommonElement == null ? lastMostCommonElement != null : !newMostCommonElement.Equals(lastMostCommonElement))
                MostCommonElementChanged.SafeInvoke(lastMostCommonElement = newMostCommonElement);
        }

        private void UpdateMostCommonItemIfEqual(T removedItem)
        {
            if (Equals(removedItem, lastMostCommonElement))
                UpdateMostCommonItem();
        }

        private void UpdateMostCommonItemIfNotEqual(T addedItem)
        {
            if (!Equals(addedItem, lastMostCommonElement))
                UpdateMostCommonItem();
        }

        /// <summary>
        /// Checks for the object that occurs most often in the queue.
        /// </summary>
        public bool TryGetMostCommonItem(out T result)
        {
            T[] query;
            lock (lockRef) {
                query = (from item in GetItems()
                         group item by item into g
                         orderby g.Count() descending
                         select g.Key).Take(1).ToArray();
            }

            if (query.Any()) {
                result = query.First();
                return true;
            } else {
                result = default(T);
                return false;
            }
        }

        #endregion
    }
}
