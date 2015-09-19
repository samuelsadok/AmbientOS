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
    /// Delegates of this type generate a view controller for a specific data item.
    /// </summary>
    /// <typeparam name="T">The type of the data for which the controller will be contructed</typeparam>
    /// <param name="parent">The view controller that is requesting the detail view. The constructed view can use this to perform actions on the parent such as performing updates.</param>
    /// <param name="data">The data for which the controller will be contructed</param>
    /// <param name="isNew">Indicates whether the data item was just created. This can be used to slightly adapt the appearance of the detail view.</param>
    public delegate ViewController DetailViewConstructorDelegate<TItem, TData>(DetailViewController<TItem, TData> parent, TItem item, bool isNew) where TData : DataSource;

    /// <summary>
    /// A combination of a main view and a detail view for some data. In large layouts,
    /// both may be displayed at the same time. In small layouts, navigating
    /// forward or backward may be required to switch between the two views.
    /// A deriving class must provide a constructor for a main view and must
    /// call ShowDetail() for some data when appropriate.
    /// </summary>
    public abstract partial class DetailViewController<TItem, TData> : DataViewController<TData>
        where TData : DataSource
    {
        /// <summary>
        /// Shall construct a view controller that displays a detail view based on a data instance.
        /// </summary>
        public DetailViewConstructorDelegate<TItem, TData> DetailViewConstructor { get; set; }

        /// <summary>
        /// A string that should be displayed if no detail view is being displayed.
        /// </summary>
        public string PlaceholderText { get; set; }

        /// <summary>
        /// Invoked by the detail UI after a data item has been updated.
        /// An implementation should use this function to update the view used.
        /// </summary>
        public abstract void DidUpdate(TItem item);

        /// <summary>
        /// Invoked by the detail UI if the user chooses to discard a data item.
        /// An implementation should use this function to remove the item from the underlying data.
        /// </summary>
        public abstract void Discard(TItem item);
    }
}
