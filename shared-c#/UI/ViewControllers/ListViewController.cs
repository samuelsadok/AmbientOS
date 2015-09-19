using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{

    /// <summary>
    /// Displays a list of items of the same kind.
    /// </summary>
    /// <typeparam name="T">the underlying data type of a single item</typeparam>
    public partial class ListViewController<T> : DetailViewController<T, CollectionSource<T>>
        where T : class
    {
        public Field<T>[] Fields { get; set; }
        public Func<T, string> CategoryNameFactory { get; set; }
        public string AddText { get; set; }

        /// <summary>
        /// Returns the category for an item. Returns null if CategoryNameFactory is null.
        /// </summary>
        private string GetCategory(T item)
        {
            return (CategoryNameFactory == null ? null : CategoryNameFactory(item));
        }

        public override void DidUpdate(T item)
        {
            Data.UpdateItem(item);
        }

        public override void Discard(T item)
        {
            Data.Remove(item);
        }
    }
}
