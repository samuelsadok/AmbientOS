using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Framework
{
    /// <summary>
    /// Represents a list of sets of elements.
    /// Supports quickly merging sets and finding the set ID of a particulat item.
    /// </summary>
    public class UnionFind<T>
    {
        int[] parents, ranks;
        T[] items;

        /// <param name="items">a list of items - must not be modified during the lifetime of this instance. Referencing of items in this instance will happen through indices within this list.</param>
        public UnionFind(T[] items)
        {
            this.items = items;
            parents = Enumerable.Range(0, items.Count() - 1).ToArray(); // each element is its own parent
            ranks = Enumerable.Repeat(0, items.Count()).ToArray(); // each tree has rank 0
        }

        /// <summary>
        /// Returns the identifier of the set that this item belongs to.
        /// time complexity: O(log(n)) (no trees can be higher than log(n))
        /// </summary>
        public int Find(int item)
        {
            if (parents[item] == item)
                return item;
            return (parents[item] = Find(parents[item]));
        }

        /// <summary>
        /// Merges the two sets that the specified items belong to. Returns false if the two items already belong to the same set.
        /// time complexity: O(log(n))
        /// </summary>
        public bool Union(int item1, int item2)
        {
            int set1 = Find(item1), set2 = Find(item2);
            if (set1 == set2) return false;
            if (ranks[set2] > ranks[set1]) {
                parents[set1] = set2;
            } else {
                parents[set2] = set1;
                if (ranks[set1] == ranks[set2]) ranks[set1]++;
            }
            return true;
        }
    }
}