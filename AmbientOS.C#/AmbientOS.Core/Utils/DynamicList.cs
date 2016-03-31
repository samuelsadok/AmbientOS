using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS
{
    /// <summary>
    /// Represents a list that is specifically designed for multithreading requirements.
    /// A list is a collection where the order is specified and the same element can occur multiple times.
    /// All public members of this class are thread-safe.
    /// </summary>
    public abstract class DynamicList<T>
    {
        /// <summary>
        /// Adds an item to the list at the specified index.
        /// </summary>
        /// <param name="index">If -1, the item is added to the end.</param>
        public abstract void Add(T item, int index);
        public abstract void RemoveAt(int index);
        public abstract void Move(int oldIndex, int newIndex);
    }
}
