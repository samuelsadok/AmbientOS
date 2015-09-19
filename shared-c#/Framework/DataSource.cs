using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.OS;

namespace AppInstall.Framework
{
    //public interface IDataSource<T> : IList<T>
    //{
    //    bool CanConstructItems { get; }
    //    bool CanMoveItems { get; }
    //    bool CanDeleteItems { get; }
    //    T AddItem();
    //    void AddItem(T item);
    //    void AddItem(T item, int index);
    //    void MoveItems(int source, int destination, int count);
    //    void DeleteItem(int index);
    //};

    public abstract class DataSource
    {
        protected List<DataSource> dependencies = new List<DataSource>();

        protected SlowAction refreshAction;
        protected SlowAction submitAction;
        public ActivityTracker RefreshTracker { get { return CanRefresh ? refreshAction.Tracker : null; } }
        public ActivityTracker SubmitTracker { get { return CanSubmit ? submitAction.Tracker : null; } }
        public bool CanRefresh { get { return refreshAction != null; } }
        public bool CanSubmit { get { return submitAction != null; } }
        //public bool IsRefreshing { get; private set; }
        //public bool IsSubmitting { get; private set; }
        public bool IsLoaded { get; protected set; }

        /// <summary>
        /// Uses the underlying item provider to update the data associated with this source.
        /// A refresh may take some time. Only one refresh can happen at a time.
        /// Multiple concurrent refresh calls will result in less refreshes than requested.
        /// </summary>
        public async Task Refresh(CancellationToken cancellationToken)
        {
            Platform.DefaultLog.Log("refresh triggered in data source " + this.GetHashCode());
            await refreshAction.TriggerAndWait(cancellationToken);
        }

        /// <summary>
        /// Like a normal refresh, but a new refresh is not enqueued if one is already in progress.
        /// </summary>
        public async Task SoftRefresh(CancellationToken cancellationToken)
        {
            Platform.DefaultLog.Log("soft refresh triggered in data source " + this.GetHashCode());
            await refreshAction.SoftTriggerAndWait(cancellationToken);
        }

        /// <summary>
        /// Uses the underlying sumbission action to submit the data associated with this source.
        /// A submission may take some time. Only one submission can happen at a time.
        /// Multiple concurrent submit calls will result in less submissions than requested.
        /// </summary>
        public async Task Submit(CancellationToken cancellationToken)
        {
            await submitAction.TriggerAndWait(cancellationToken);
        }

        public void AddDependency(DataSource dependency)
        {
            dependencies.Add(dependency);
        }
    }


    public class DataSource<T> : DataSource
    {
        protected T data;

        /// <summary>
        /// Returns the raw data of this source.
        /// This may trigger a refresh, in which case only the current data (before the refresh) is returned.
        /// </summary>
        public T Data
        {
            get
            {
                if (!IsLoaded) Refresh(ApplicationControl.ShutdownToken).Run();
                return data;
            }
        }

        public bool RefreshOnMainThread { get; private set; }

        /// <summary>
        /// Creates a data source that cannot be refreshed.
        /// </summary>
        public DataSource(T data)
        {
            this.data = data;
            IsLoaded = true;
        }

        /// <summary>
        /// Creates a data source that can be refreshed
        /// </summary>
        /// <param name="refreshPeriod">Set to -1 to disable automatic refreshing</param>
        public DataSource(T initialData, Func<CancellationToken, T> dataProvider, Action<T, CancellationToken> submitAction, bool refreshOnMainThread)
        {
            this.data = initialData;
            IsLoaded = dataProvider == null;

            RefreshOnMainThread = refreshOnMainThread;

            if (dataProvider != null)
                this.refreshAction = new SlowAction((cancellationToken) => {
                    // refresh all dependencies that aren't loaded yet or that previously failed to load.
                    var toRefresh = dependencies.Where((d) => !d.IsLoaded || d.RefreshTracker.Status == ActivityStatus.Failed).ToArray();
                    Task.WaitAll(toRefresh.Select((d) => d.Refresh(cancellationToken)).ToArray());
                    var exceptions = (from d in toRefresh where d.RefreshTracker.Status == ActivityStatus.Failed select d.RefreshTracker.LastException);

                    if (exceptions.Any())
                        throw new AggregateException("failed to refresh dependencies", exceptions);

                    var newItems = dataProvider(cancellationToken);
                    if (refreshOnMainThread)
                        Platform.InvokeMainThread(() => Refresh(newItems));
                    else
                        Refresh(newItems);

                    IsLoaded = true;
                });

            if (submitAction != null)
                this.submitAction = new SlowAction((cancellationToken) => {
                    submitAction(data, cancellationToken);
                });
        }

        /// <summary>
        /// Updates the current data with the new data.
        /// If not redefined, this simply replaces the old data with the new data.
        /// </summary>
        public virtual void Refresh(T data)
        {
            this.data = data;
        }
    }



    public class CollectionSource<T> : DataSource<ObservableCollection<T>>, IList<T>, IList //ObservableCollection<T>
    {
        public Func<T> ItemConstructor { get; set; }
        public bool CanConstructItems { get { return ItemConstructor != null; } }
        public bool CanMoveItems { get; set; }
        public bool CanDeleteItems { get; set; }
        public Func<T, Guid> PrimaryKeyGetter { get; private set; }


        /// <summary>
        /// Triggered after a new item has been added or replaced.
        /// </summary>
        public event Action<T> DidAddItem;

        /// <summary>
        /// Triggered after an item has been removed or replaced.
        /// </summary>
        public event Action<T> DidRemoveItem;

        /// <summary>
        /// Triggered after an item has changed.
        /// </summary>
        public event Action<T> DidUpdateItem;

        /// <summary>
        /// Triggered if adding a new item led to the creation of a new category.
        /// </summary>
        public event Action<Category> DidAddCategory;

        /// <summary>
        /// Triggered if removing an item left an empty category.
        /// </summary>
        public event Action<Category> DidRemoveCategory;


        /// <summary>
        /// Returns the item of which the primary key matches the specified key, or null if it doesn't exits.
        /// PrimaryKeyGetter must be set to use this index.
        /// </summary>
        public T this[Guid primaryKey]
        {
            get { return data.SingleOrDefault((obj) => PrimaryKeyGetter(obj) == primaryKey); }
        }

        /// <summary>
        /// Creates an empty data source
        /// </summary>
        public CollectionSource()
            : base(new ObservableCollection<T>())
        {
            SetupHandlers();
        }

        /// <summary>
        /// Creates a data source from the specified list
        /// </summary>
        public CollectionSource(IEnumerable<T> data)
            : base(new ObservableCollection<T>(data))
        {
            SetupHandlers();
        }

        /// <summary>
        /// Creates a collection source that can be refreshed. The collection may refresh itself when it is first accessed.
        /// </summary>
        public CollectionSource(IEnumerable<T> initialData, Func<CancellationToken, IEnumerable<T>> dataProvider, Action<IEnumerable<T>, CancellationToken> submitAction, bool refreshOnMainThread)
            : base(new ObservableCollection<T>(initialData), (c) => new ObservableCollection<T>(dataProvider(c)), submitAction, refreshOnMainThread)
        {
            SetupHandlers();
        }

        /// <summary>
        /// Creates a collection source that can be refreshed. The collection may refresh itself when it is first accessed.
        /// </summary>
        public CollectionSource(Func<CancellationToken, IEnumerable<T>> dataProvider, Action<IEnumerable<T>, CancellationToken> submitAction, bool refreshOnMainThread, Func<T, Guid> primaryKeyGetter)
            : base(new ObservableCollection<T>(), (c) => new ObservableCollection<T>(dataProvider(c)), submitAction, refreshOnMainThread)
        {
            PrimaryKeyGetter = primaryKeyGetter;
            SetupHandlers();
        }


        /// <summary>
        /// Replaces the current data with the new data.
        /// </summary>
        public override void Refresh(ObservableCollection<T> data)
        {
            var newItems = data.Except(this.data).ToArray();
            var oldItems = this.data.Except(data).ToArray();
            foreach (var i in oldItems)
                this.data.Remove(i);
            foreach (var i in newItems)
                this.data.Add(i);
        }


        /// <summary>
        /// Generates a new item from the instance's item factory and adds it to the list.
        /// </summary>
        /// <returns>the new item</returns>
        public T AddNewItem()
        {
            var item = ItemConstructor();
            Add(item);
            return item;
        }

        /// <summary>
        /// Triggers the DidUpdateItem event.
        /// </summary>
        public void UpdateItem(T item)
        {
            DidUpdateItem.SafeInvoke(item);
        }


        private void SetupHandlers()
        {
            data.CollectionChanged += (o, e) => {

                // note: the move action is ignored
                
                // handle old items being removed
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove
                    | e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace
                    | e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) {

                    // in case of a reset, no other properties of e are valid
                    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) {
                        foreach (var cat in categories)
                            DidRemoveCategory.SafeInvoke(cat);
                        categories.Clear();
                        throw new NotImplementedException("list was reset - there may be a problem with not knowing which items were removed");
                    }

                    // remove categories as neccessary and trigger item-removed-event for each old item
                    if (e.OldItems != null) {
                        foreach (var item in e.OldItems) {
                            if (CategoryFactory != null) {
                                string cat = CategoryFactory((T)item);
                                foreach (var category in categories.Where((c) => c.Name == cat))
                                    category.MemberCount--;
                                categories.RemoveAll((c) => c.MemberCount <= 0);
                            }

                            DidRemoveItem.SafeInvoke((T)item);
                        }
                    }
                }

                // handle new items being added
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                    | e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace
                    | e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) {

                    var newItems = e.NewItems;

                    // in case of a reset, no other properties of e are valid
                    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                        newItems = data;

                    // add categories as neccessary and trigger item-added-event for each new item
                    if (newItems != null) {
                        foreach (var item in newItems) {
                            if (CategoryFactory != null) {
                                string cat = CategoryFactory((T)item);
                                var category = categories.FirstOrDefault((c) => c.Name == cat);
                                if (category != null)
                                    category.MemberCount++;
                                else
                                    categories.Add(new Category() { Name = cat, MemberCount = 1 });
                            }
                            DidAddItem.SafeInvoke((T)item);
                        }
                    }
                }
            };
        }

#region "Categories"


        /// <summary>
        /// //////// TODO: IMPLEMENT CATEGORIES (+ UPDATES)
        /// then use them in a new ListView implementation on iOS
        /// </summary>

        public class Category {
            public string Name { get; set; }
            public int MemberCount { get; set; }
        }

        private List<Category> categories;

        private Func<T, string> categoryFactory;

        /// <summary>
        /// This function shall generate a category label for each item that it is being passed.
        /// Can be null if categories are not used for this collection.
        /// </summary>
        public Func<T, string> CategoryFactory {
            get { return categoryFactory; }
            set {
                if (data.Any())
                    throw new NotImplementedException("the list must be empty for the category factory to be set");
                categoryFactory = value;
                categories = new List<Category>();
                // todo: rebuild category list and remove empty-list constraint
            }
        }
        
#endregion


        #region "IList<T>, IList and ICollection implementations"


        public int Count { get { return data.Count; } }
        public bool IsReadOnly { get { return ((IList<T>)data).IsReadOnly || (IsFixedSize && !CanMoveItems); } }
        public bool IsFixedSize { get { return !(CanDeleteItems || CanConstructItems); } }
        public bool IsSynchronized { get { return false; } } // todo: synchronize
        public object SyncRoot { get { return null; } }


        public T this[int index]
        {
            get { return data[index]; }
            set { data[index] = value; }
        }
        object System.Collections.IList.this[int index]
        {
            get { return data[index]; }
            set { data[index] = (T)value; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (!IsLoaded) Refresh(ApplicationControl.ShutdownToken).Run();
            return ((IEnumerable<T>)data).GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            if (!IsLoaded) Refresh(ApplicationControl.ShutdownToken).Run();
            return data.GetEnumerator();
        }
        public int IndexOf(T item)
        {
            return data.IndexOf(item);
        }
        public int IndexOf(object item)
        {
            return data.IndexOf((T)item);
        }
        public void Insert(int index, T item)
        {
            data.Insert(index, item);
        }
        public void Insert(int index, object item)
        {
            data.Insert(index, (T)item);
        }
        public void RemoveAt(int index)
        {
            data.RemoveAt(index);
        }
        public void Add(T item)
        {
            data.Add(item);
        }
        public int Add(object item)
        {
            data.Add((T)item);
            return data.Count() - 1;
        }
        public void Clear()
        {
            data.Clear();
        }
        public bool Contains(T item)
        {
            return data.Contains(item);
        }
        public bool Contains(object item)
        {
            return data.Contains((T)item);
        }
        public void CopyTo(T[] array, int arrayIndex)
        {
            data.CopyTo(array, arrayIndex);
        }
        public void CopyTo(System.Array array, int arrayIndex)
        {
            data.Cast<object>().ToArray().CopyTo(array, arrayIndex);
        }
        public bool Remove(T item)
        {
            return data.Remove(item);
        }
        public void Remove(object item)
        {
            if (!data.Remove((T)item))
                throw new InvalidOperationException();
        }


        #endregion

    }

    



    public class MappedSource<TOrigin, TMapped> : CollectionSource<TMapped>
    {
        private CollectionSource<TOrigin> origin;
        private Func<TOrigin, TMapped> originToMapped;
        private Func<TMapped, TOrigin> mappedToOrigin;

        ObservableCollection<TMapped> asdasd;
        static MappedSource<TOrigin, TMapped> mapped;

        /// <summary>
        /// Creates a seperate data source that maps from one type from another.
        /// Subsequent operations on either collection will not affect the other collection.
        /// </summary>
        /// <param name="originToMapped">When called twice with the same input, this shall return two values that are equal (by the standard equality comparer)</param>
        /// <param name="mappedToOrigin">Converts an origin type object from a mapped type object. If null, the collection cannot be used to construct new items.</param>
        /// <param name="lazilyCoupled">If true, changes to this collection are not directly propagate to the origin and vice versa. In this case, the Commit function must be called.</param>
        /// <param name="refreshOnMainThread">Only required if the collections are tightly coupled.</param>
        public MappedSource(CollectionSource<TOrigin> origin, Func<TOrigin, TMapped> originToMapped, Func<TMapped, TOrigin> mappedToOrigin, bool lazilyCoupled)
            : base(
                origin.Select(originToMapped),
                lazilyCoupled || !origin.CanRefresh ? null : (Func<CancellationToken, IEnumerable<TMapped>>)(c => {
                    origin.Refresh(c).Wait(c);
                    return origin.Select(originToMapped);
                }),
                lazilyCoupled || !origin.CanSubmit ? null : (Action<IEnumerable<TMapped>, CancellationToken>)((items, c) => {
                    var originItems = items.Select(mappedToOrigin);
                    origin.Add(originItems.Except(origin));
                    origin.RemoveAll(i => originItems.Contains(i));
                    origin.Submit(c).Wait(c);
                }), origin.RefreshOnMainThread)
        {
            if (originToMapped == null)
                throw new ArgumentNullException("originToMapped");

            this.origin = origin;
            this.originToMapped = originToMapped;
            this.mappedToOrigin = mappedToOrigin;


            var updating = new List<TOrigin>();

            if (origin.ItemConstructor != null)
                ItemConstructor = () => originToMapped(origin.ItemConstructor());


            if (lazilyCoupled)
                return;


            // propagate changes from source to mapped collection
            origin.DidAddItem += item => {
                var i = originToMapped(item);
                if (!Contains(i))
                    Add(i);
            };
            origin.DidRemoveItem += item => Remove(originToMapped(item));

            origin.DidUpdateItem += item => {
                lock (updating) {
                    if (updating.Contains(item))
                        return;
                    updating.Add(item);
                }

                try {
                    UpdateItem(originToMapped(item));
                } finally {
                    lock (updating)
                        updating.Remove(item);
                }
            };


            // propagate changes from joint collection to sources
            this.DidAddItem += item => {
                var i = mappedToOrigin(item);
                if (!origin.Contains(i))
                    origin.Add(i);
            };
            this.DidRemoveItem += item => { origin.Remove(mappedToOrigin(item)); };

            this.DidUpdateItem += item => {
                var i = mappedToOrigin(item);
                lock (updating) {
                    if (updating.Contains(i))
                        return;
                    updating.Add(i);
                }

                try {
                    origin.UpdateItem(i);
                } finally {
                    lock (updating)
                        updating.Remove(i);
                }
            };
        }

        /// <summary>
        /// Commits the changes made to this source.
        /// </summary>
        public void Commit()
        {
            var newCommit = this.data.Except(origin.Select(originToMapped)).ToArray();
            origin.RemoveAll((item) => !this.data.Contains(originToMapped(item)));

            if (newCommit.Any() && mappedToOrigin == null)
                throw new InvalidOperationException("new items were added to the mapped collection, can't convert these items to origin type");

            origin.AddRange(newCommit.Select(mappedToOrigin));
        }
    }


    /// <summary>
    /// Makes it possible to join multiple collection sources of the same type.
    /// A possible use case is for example a document collection were some documents are stored locally and some reside on the network.
    /// </summary>
    public class JointSource<T> : CollectionSource<T>
    {
        private CollectionSource<T> primarySource;

        /// <summary>
        /// Creates a single collection source from multiple sources.
        /// The first of the sources that can construct items is considered the primary source.
        /// Items that are added to this collection are added to the underlying primary source.
        /// Issues arise when there are multiple equal items in the entire collection.
        /// </summary>
        public JointSource(params CollectionSource<T>[] sources)
            : base(
                sources.SelectMany(x => x),
                !sources.Any(s => s.CanRefresh) ? (Func<CancellationToken, IEnumerable<T>>)null : c => {
                    Task.WaitAll((from s in sources where s.CanRefresh select s.Refresh(c)).ToArray(), c);
                    return sources.SelectMany(x => x);
                }, null /* (items, c) => { // todo: implement submitting primary source
                items.Except(sources.SelectMany(x => x));
                throw new NotImplementedException();
                }*/, sources.Any(s => s.RefreshOnMainThread))
        {
            var updating = new List<T>();
            var primary = sources.FirstOrDefault(source => source.CanConstructItems);

            if (primary != null)
                ItemConstructor = primary.ItemConstructor;

            // propagate changes from sources to joint collection
            foreach (var source in sources) {
                source.DidAddItem += item => { if (!Contains(item)) Add(item); };
                source.DidRemoveItem += item => Remove(item);

                source.DidUpdateItem += item => {
                    lock (updating) {
                        if (updating.Contains(item))
                            return;
                        updating.Add(item);
                    }

                    try {
                        UpdateItem(item);
                    } finally {
                        lock (updating)
                            updating.Remove(item);
                    }
                };
            }

            // propagate changes from joint collection to sources
            this.DidAddItem += item => {
                if (primary == null)
                    throw new InvalidOperationException("can't determine where to add the item");
                primary.Add(item);
            };
            this.DidRemoveItem += item => { foreach (var source in sources) source.Remove(item); };

            this.DidUpdateItem += item => {
                lock (updating) {
                    if (updating.Contains(item))
                        return;
                    updating.Add(item);
                }

                try {
                    foreach (var source in sources.Where(s => s.Contains(item)))
                        source.UpdateItem(item);
                } finally {
                    lock (updating)
                        updating.Remove(item);
                }
            };

        }
    }


    public class TreeSource<TTree, TItem> : DataSource<TTree>
    {

        private Func<TTree, IEnumerable<TTree>> subfolderProvider;
        private Func<TTree, IEnumerable<TItem>> itemProvider;

        /// <summary>
        /// Returns the subfolders in the top-most level of this tree.
        /// </summary>
        public IEnumerable<TreeSource<TTree, TItem>> Subfolders { get { return subfolderProvider(Data).Select((f) => new TreeSource<TTree, TItem>(f, subfolderProvider, itemProvider)); } }

        /// <summary>
        /// Returns the items in the top-most level of this tree.
        /// </summary>
        public IEnumerable<TItem> Items { get { return itemProvider(Data); } }

        /// <summary>
        /// Recursively returns all items in this tree.
        /// </summary>
        public IEnumerable<TItem> AllItems { get { return Subfolders.SelectMany((f) => f.AllItems).Concat(Items); } }

        /// <summary>
        /// Returns the subfolders and items in the top-most level of this tree.
        /// </summary>
        public IEnumerable<object> Content { get { return Subfolders.Cast<object>().Concat(Items.Cast<object>()); } }

        public TreeSource(TTree value, Func<TTree, IEnumerable<TTree>> subfolderProvider, Func<TTree, IEnumerable<TItem>> itemProvider)
            : base(value)
        {
            this.subfolderProvider = subfolderProvider;
            this.itemProvider = itemProvider;
        }

        public TreeSource(TTree initialData, Func<CancellationToken, TTree> dataProvider, Action<TTree, CancellationToken> submitAction, bool refreshOnMainThread, Func<TTree, IEnumerable<TTree>> subfolderProvider, Func<TTree, IEnumerable<TItem>> itemProvider, Func<TItem, Guid> primaryKeyGetter)
            : base(initialData, dataProvider, submitAction, refreshOnMainThread)
        {
            this.subfolderProvider = subfolderProvider;
            this.itemProvider = itemProvider;
            PrimaryKeyGetter = primaryKeyGetter;
        }

        public Func<TItem, Guid> PrimaryKeyGetter { get; private set; }

        public TItem this[Guid primaryKey]
        {
            get
            {
                return AllItems.Single((obj) => PrimaryKeyGetter(obj) == primaryKey);
            }
        }
    }


    //public class UnreliableDataSource<T> : DataSource<T>
    //{
    //    Func<IEnumerable<T>> getter;
    //
    //    public UnreliableDataSource(Func<IEnumerable<T>> getter)
    //    {
    //        this.getter = getter;
    //    }
    //
    //    public void Refresh()
    //    {
    //        
    //    }
    //}

    //public class Comparer<T>
    //{
    //    
    //}


    ///// <summary>
    ///// Represents a per-item field source that can be read from and written to.
    ///// </summary>
    //public class FieldSource<TItem, TField>
    //{
    //    private Func<TItem, TField> getter;
    //    private Action<TItem, TField> setter;
    //    public event Action ValueChanged;
    //
    //    public bool IsReadOnly { get { return setter == null; } }
    //
    //    public TField Get(TItem item)
    //    {
    //        return getter(item);
    //    }
    //
    //    public void Set(TItem item, TField value)
    //    {
    //        if (IsReadOnly)
    //            throw new InvalidOperationException("the field is read-only");
    //        var changed = (!getter(item).Equals(value));
    //        setter(item, value);
    //        if (changed) ValueChanged.SafeInvoke();
    //    }
    //
    //    public void ForceUpdate()
    //    {
    //        ValueChanged.SafeInvoke();
    //    }
    //
    //    public FieldSource(Func<TItem, TField> getter, Action<TItem, TField> setter)
    //    {
    //        this.getter = getter;
    //        this.setter = setter;
    //    }
    //    public FieldSource(Func<TItem, TField> getter)
    //        : this(getter, null)
    //    {
    //    }
    //}



    /*
    public class FieldSourceUpdateEventArgs<T>
    {
        public T OldValue { get; private set; }
        public T NewValue { get; private set; }

        /// <summary>
        /// Indicates whether this change is real or just a test.
        /// </summary>
        public bool Definite { get; private set; }

        public void MakeDefinitive()
        {
            Definite = true;
        }

        public FieldSourceUpdateEventArgs(T oldValue, T newValue, bool definite)
        {
            OldValue = oldValue;
            NewValue = newValue;
            Definite = definite;
        }


        private bool approved = true;

        /// <summary>
        /// Writing false to this field cancels the proposed changes.
        /// Other callbacks may not override the decision once a change has been disapproved.
        /// Disapproving is not possible if the update is definite.
        /// </summary>
        public bool Approved {
            get { return approved; }
            set
            {
                if (!value && Definite)
                    throw new InvalidOperationException("the value change is definite and cannot be disapproved.");
                if (value && !approved)
                    throw new InvalidOperationException("the value change has been disapproved and no longer be approved");
                approved = value;
            }
        }
    }
    */

    /*
    /// <summary>
    /// Represents the result of a validity check, optionally supplemented by an explanation for the user.
    /// </summary>
    public class Approval
    {
        public bool Valid { get; private set; }
        public string Explanation { get; private set; }
        public Approval()
        {
            Valid = true;
            Explanation = null;
        }
        public Approval(bool valid, string explanation)
        {
            Valid = valid;
            Explanation = explanation;
        }
    }
    */

    /*public interface IFieldSource<T>
    {
        /// <summary>
        /// Provides a list of checks that will be performed whenever a new value is available.
        /// </summary>
        List<Func<object, T, Approval>> UpdateHandlers { get; }

        /// <summary>
        /// This will be invoked if the value is about to change.
        /// All handlers of this event have a chance to disapprove the new value.
        /// </summary>
        event EventHandler<FieldSourceUpdateEventArgs<T>> ValueWillChange;

        /// <summary>
        /// Invoked whenever the underlying value has changed.
        /// This event must not be triggerd for values that have been disapproved.
        /// </summary>
        event EventHandler<FieldSourceUpdateEventArgs<T>> ValueChanged;

        /// <summary>
        /// Returns the current value of the field source.
        /// This may include values that have not been approved.
        /// </summary>
        T Get();

        /// <summary>
        /// Tries to apply the specified value. Returns false if the field source rejects the value.
        /// This must not be invoked if the FieldSource is read-only.
        /// </summary>
        bool Set(T value);

        /// <summary>
        /// Marks the current value as valid or invalid.
        /// If the FieldSource is a UI element, this shall result in a visual indication and optionally display the explanation message.
        /// </summary>
        /// <param name="explanation">An explanation of the reason why the value could not be accepted. Can be null.</param>
        void SetValid(bool valid, string explanation);
    }*/

    /*
    public static class FieldSourceExtensions
    {
        /// <summary>
        /// Triggers the update handlers.
        /// </summary>
        public static bool PerformUpdate<T>(this IFieldSource<T> field, T newValue)
        {
            var e = new FieldSourceUpdateEventArgs<T>(field.Get(), newValue, false);
            field.ValueWillChange.SafeInvoke(field, e);
            if (!e.Approved) {

            }
            e.MakeDefinitive();
            field.ValueChanged.SafeInvoke(field, e);
        }


        /// <summary>
        /// Updates the local value and notifies all subscribers. If the update origin is the current instance,
        /// no action is taken. If this is an original update (origin = null), the update is performed even if
        /// it is invalid. In this case however, the invalid value may be indicated visually.
        /// </summary>
        public static Approval PerformUpdate<T>(this IFieldSource<T> field, object origin, T newValue)
        {
            if (origin == field)
                return new Approval();

            if (origin == null)
                origin = field;

            Approval approval = null;
            foreach (var callback in field.UpdateHandlers)
                if (!(approval = callback.Invoke(origin, newValue)).Valid)
                    break;
            if (approval == null)
                approval = new Approval();

            if (origin == field || approval.Valid) {
                indicateState(approval);
                setter(newValue);
            }

            return approval;
        }

        
        public static void PerformUpdatee<T>(this IFieldSource<T> field, object origin, T newValue) {
            if (origin == field)
                return;
            var approval = field.PerformUpdate(origin, newValue);
            if (origin == null)
                origin = field;
            else if (!approval.Valid)
                return;

            foreach (var callback in field.UpdateHandlers)
                callback.Invoke(origin, newValue);
            indicateState(approval);
        }

        /// <summary>
        /// Adds a new validity check to the field source.
        /// The current value is not re-validated when adding a new check.
        /// </summary>
        public static void AddUpdateHandler<T>(this IFieldSource<T> field, Func<object, T, Approval> check)
        {
            field.UpdateHandlers.Add(check);
        }

        /// <summary>
        /// Adds a new update handler to the FieldSource.
        /// This handler will be invoked whenever the value changes locally.
        /// </summary>
        public static void AddUpdaateHandler<T>(this IFieldSource<T> field, Action<object, T> callback)
        {
            field.UpdateHandlers.Add(callback);
        }

        /// <summary>
        /// Connects two field sources, so that updates to either of the two propagate to the other one.
        /// Both FieldSources are extended by a new validity check that depends on a successful type conversion
        /// and on the validity check of the other FieldSource.
        /// When either field source disapproves the value of the other source, the two sources may be inconsistent until
        /// the discrepancy is resolved by supplying a valid value (from either side).
        /// </summary>
        /// <param name="convert12">A conversion function from T1 to T2 (may disapprove if the conversion fails)</param>
        /// <param name="convert21">A conversion function from T2 to T1 (may disapprove if the conversion fails)</param>
        public static void Connect<T1, T2>(this IFieldSource<T1> field1, IFieldSource<T2> field2, Func<T1, Tuple<T2, Approval>> convert12, Func<T2, Tuple<T1, Approval>> convert21)
        {
            field1.AddUpdateHandler((origin, newValue) => {
                var converted = convert12(newValue);
                if (!converted.Item2.Valid)
                    return converted.Item2;
                return field2.PerformUpdate(origin, converted.Item1);
            });

            field2.AddUpdateHandler((origin, newValue) => {
                var converted = convert21(newValue);
                if (!converted.Item2.Valid)
                    return converted.Item2;
                return field1.PerformUpdate(origin, converted.Item1);
            });
        }
    }
    */

    /// <summary>
    /// Represents a per-item field source that can be read from and written to.
    /// todo: add the concept of approval (bool + explanation)
    /// </summary>
    public class FieldSource<T>
    {
        private Func<T> getter;
        private Action<T> setter;
        public event Action<T> ValueChanged;

        /// <summary>
        /// Indicates if this field is write protected
        /// </summary>
        public bool IsReadOnly { get { return setter == null; } }

        /// <summary>
        /// Returns the current value.
        /// </summary>
        public T Get()
        {
            return getter();
        }

        /// <summary>
        /// Sets the field if the argument is different from the current value.
        /// </summary>
        public void Set(T value)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("the field is read-only");
            if (getter().Equals(value)) return;
            setter(value);
            ValueChanged.SafeInvoke(value);
        }

        /// <summary>
        /// Forces the invokation of any update actions.
        /// </summary>
        public void PerformUpdate()
        {
            ValueChanged.SafeInvoke(Get());
        }

        /// <summary>
        /// Creates a read-write FieldSource using a getter and setter function.
        /// </summary>
        /// <param name="getter"></param>
        /// <param name="setter"></param>
        public FieldSource(Func<T> getter, Action<T> setter)
        {
            this.getter = getter;
            this.setter = setter;
        }

        /// <summary>
        /// Creates a read-only FieldSource using a getter function.
        /// </summary>
        /// <param name="getter"></param>
        public FieldSource(Func<T> getter)
            : this(getter, null)
        {
        }


        /// <summary>
        /// Connects two fields of different types using custom type converstion functions.
        /// Upon connection, the value of the primary field will be applied to the secondary field.
        /// </summary>
        /// <param name="field1">the primary field</param>
        /// <param name="field2">the secondary field</param>
        /// <param name="tryConvert12">a function that tries to convert the first type into the second type and reports if the conversion succeeded</param>
        /// <param name="tryConvert21">a function that tries to convert the second type into the first type and reports if the conversion succeeded</param>
        /// <param name="validateField1">if not null, this action will be triggered to provide feedback about whether the value of field1 is valid</param>
        /// <param name="validateField2">if not null, this action will be triggered to provide feedback about whether the value of field2 is valid</param>
        public static void Connect<T2>(FieldSource<T> field1, FieldSource<T2> field2, Func<T, Tuple<T2, bool>> tryConvert12, Func<T2, Tuple<T, bool>> tryConvert21, Action<bool> validateField1, Action<bool> validateField2)
        {
            if (field1 == null) throw new ArgumentNullException("field1");
            if (field2 == null) throw new ArgumentNullException("field2");
            if (tryConvert12 == null) throw new ArgumentNullException("tryConvert12");
            if (tryConvert21 == null) throw new ArgumentNullException("tryConvert21");

            field1.ValueChanged += (newVal) => {
                var castResult = tryConvert12(newVal);
                if (castResult.Item2 && !field2.IsReadOnly)
                    field2.Set(castResult.Item1);
                if (validateField1 != null)
                    validateField1(castResult.Item2);
            };

            field2.ValueChanged += (newVal) => {
                var castResult = tryConvert21(newVal);
                if (castResult.Item2 && !field1.IsReadOnly)
                    field1.Set(castResult.Item1);
                if (validateField2 != null)
                    validateField2(castResult.Item2);
            };

            field1.PerformUpdate();
        }

        /// <summary>
        /// Connects two fields of the same type.
        /// Upon connection, the value of the primary field will be applied to the secondary field.
        /// </summary>
        /// <param name="field1">the primary field</param>
        /// <param name="field2">the secondary field</param>
        /// <param name="validateValue">a function that can approve or disapprove values. Only approved values are propagated</param>
        /// <param name="validateField1">if not null, this action will be triggered to provide feedback about whether the value of field1 is valid</param>
        /// <param name="validateField2">if not null, this action will be triggered to provide feedback about whether the value of field2 is valid</param>
        public static void Connect(FieldSource<T> field1, FieldSource<T> field2, Func<T, bool> validateValue, Action<bool> validateField1, Action<bool> validateField2)
        {
            Func<T, Tuple<T, bool>> validation = (val) => new Tuple<T, bool>(val, validateValue(val));
            Connect(field1, field2, validation, validation, validateField1, validateField2);
        }

        public static int CompareObjects<TKey>(TKey x, TKey y, Func<TKey, FieldSource<T>> fieldSourceProvider)
        {
            return ((IComparable)fieldSourceProvider(x).Get()).CompareTo((IComparable)fieldSourceProvider(y).Get());
        }
    }
}