using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using Foundation;
using UIKit;
using CoreGraphics;
using AppInstall.Framework;


namespace AppInstall.UI
{
    public class EnhancedUITableView : UITableView
    {
        public event Action EditingStarted;
        public EnhancedUITableView()
            : this(true)
        {
        }
        public EnhancedUITableView(bool grouped)
            : base(new RectangleF(), grouped ? UITableViewStyle.Grouped : UITableViewStyle.Plain)
        {
        }
        public override void SetEditing(bool editing, bool animated)
        {
            EditingStarted.SafeInvoke();
            base.SetEditing(editing, animated);
        }
    }


    public abstract class BaseListView : View<EnhancedUITableView>
    {
        //private static Dictionary<Type, Tuple<Type, Guid>> dataPresenters = new Dictionary<Type, Tuple<Type, Guid>>();
        //
        ///// <summary>
        ///// Adds the capability to display a new type data in all list views in the application
        ///// </summary>
        //public static void UseDataPresenter(Type dataPresenter, Type inputType)
        //{
        //    if (!(typeof(ListViewItem).IsAssignableFrom(dataPresenter))) throw new InvalidCastException("the data presenter type must inherit from [ListViewItem]");
        //    dataPresenters[inputType] = new Tuple<Type, Guid>(dataPresenter, new Guid());
        //}

        public float VerticalPadding { get; set; }
        public bool Editing { get { return nativeView.Editing; } }

        private int animationMod = 1; // 0: temporarily disable animations to move a row, 1: normal, 2: alternative delete animation

        protected abstract int GetNumberOfSections();
        protected abstract IListViewSection GetSection(int section);

        
        class ListViewSource : UITableViewSource
        {
            private BaseListView parent;
            private List<ListViewItem> items = new List<ListViewItem>();

            public ListViewSource(BaseListView parent)
            {
                this.parent = parent;
            }

            public override nint NumberOfSections(UITableView tableView)
            {
                return parent.GetNumberOfSections();
            }

            private bool IsSpecialCell(NSIndexPath indexPath)
            {
                return indexPath.Row >= parent.GetSection(indexPath.Section).NumberOfRows;
            }

            public override nint RowsInSection(UITableView tableView, nint section)
            {
                var s = parent.GetSection((int)section);
                return s.NumberOfRows + s.SpecialItems.Count();
            }

            public override string TitleForHeader(UITableView tableView, nint section)
            {
                return parent.GetSection((int)section).Header;
            }

            public override string TitleForFooter(UITableView tableView, nint section)
            {
                return parent.GetSection((int)section).Footer;
            }

            private ListViewItem GetCell(NSIndexPath indexPath)
            {
                var normalCells = parent.GetSection(indexPath.Section).NumberOfRows;
                if (indexPath.Row >= normalCells)
                    return parent.GetSection(indexPath.Section).SpecialItems[indexPath.Row - normalCells];
                return parent.GetSection(indexPath.Section).GetItem(indexPath.Row);
            }

            public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
            {
                return GetCell(indexPath);
            }

            public override nfloat GetHeightForRow(UITableView tableView, NSIndexPath indexPath)
            {
                return GetCell(indexPath).Height;
            }

            public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
            {
                var cell = GetCell(indexPath);
                cell.PerformSelection(parent);
                if (!cell.PersistentSelection)
                    parent.nativeView.DeselectRow(NSIndexPath.FromRowSection(indexPath.Row - (cell == GetCell(indexPath) ? 0 : 1), indexPath.Section), true); // note: a preceding accessory cell might have been removed meanwhile
            }
            public override void RowDeselected(UITableView tableView, NSIndexPath indexPath)
            {
                GetCell(indexPath).PerformDeselection(parent);
            }
            public override void AccessoryButtonTapped(UITableView tableView, NSIndexPath indexPath)
            {
                GetCell(indexPath).PerformInfoButtonClick(parent);
            }

            public override bool CanEditRow(UITableView tableView, NSIndexPath indexPath)
            {
                return parent.GetSection(indexPath.Section).CanEdit || IsSpecialCell(indexPath);
            }

            public override bool CanMoveRow(UITableView tableView, NSIndexPath indexPath)
            {
                return parent.GetSection(indexPath.Section).CanEdit && !IsSpecialCell(indexPath);
            }

            public override bool ShouldHighlightRow(UITableView tableView, NSIndexPath indexPath)
            {
                return GetCell(indexPath).IsSelectable || IsSpecialCell(indexPath);
            }

            public override void WillBeginEditing(UITableView tableView, NSIndexPath indexPath)
            {
                parent.GetSection(indexPath.Section).StartEditing();
            }

            public override UITableViewCellEditingStyle EditingStyleForRow(UITableView tableView, NSIndexPath indexPath)
            {
                if (IsSpecialCell(indexPath)) return UITableViewCellEditingStyle.Insert;
                return (parent.GetSection(indexPath.Section).CanEdit ? UITableViewCellEditingStyle.Delete : UITableViewCellEditingStyle.None);
            }

            public override void MoveRow(UITableView tableView, NSIndexPath sourceIndexPath, NSIndexPath destinationIndexPath)
            {
                //var cell = parent.GetSection(sourceIndexPath.Section).GetItem(sourceIndexPath.Row);
                //parent.animationMod = 0;
                //parent.GetSection(sourceIndexPath.Section).RemoveItem(sourceIndexPath.Row);
                parent.GetSection(sourceIndexPath.Section).MoveItem(sourceIndexPath.Row, destinationIndexPath.Row);
                //parent.GetSection(destinationIndexPath.Section).AddItem(destinationIndexPath.Row, cell);
                //parent.animationMod = 1;
            }

            public override NSIndexPath CustomizeMoveTarget(UITableView tableView, NSIndexPath sourceIndexPath, NSIndexPath proposedIndexPath)
            {
                int row = (proposedIndexPath.Section < sourceIndexPath.Section ? 0 : proposedIndexPath.Row);
                if (proposedIndexPath.Section > sourceIndexPath.Section) row = int.MaxValue;
                var result = NSIndexPath.FromRowSection(Math.Min(row, parent.GetSection(sourceIndexPath.Section).NumberOfRows - 1), sourceIndexPath.Section);
                return result;
            }

            public override void CommitEditingStyle(UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
            {
                switch (editingStyle) {
                    case UITableViewCellEditingStyle.Insert: // a cell was selected while in the "add"-mode
                        GetCell(indexPath).PerformSelection(parent);
                        break;
                    case UITableViewCellEditingStyle.Delete: // a cell was selected while in the "remove"-mode
                        parent.animationMod = 2;
                        parent.GetSection(indexPath.Section).RemoveItem(indexPath.Row);
                        parent.animationMod = 1;
                        break;
                }
            }


            public override void Scrolled(UIScrollView scrollView)
            {
                parent.UpdateSpecialViews();
            }

            public override void DraggingStarted(UIScrollView scrollView)
            {
                foreach (var header in parent.headers)
                    header.DraggingStarted(scrollView);
            }

            public override void DraggingEnded(UIScrollView scrollView, bool willDecelerate)
            {
                foreach (var header in parent.headers)
                    header.DraggingEnded(scrollView);
            }
        }

        private bool grouped;

        private IEnumerable<ScrollViewHeader> headers;

        public BaseListView(bool grouped)
            : base(new EnhancedUITableView(grouped), true)
        {
            this.grouped = grouped;
            nativeView.Source = new ListViewSource(this);
            nativeView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.Interactive;
            nativeView.EditingStarted += () => {
                for (int i = 0; i < GetNumberOfSections(); i++)
                    GetSection(i).StartEditing();
            };
            //if (!content.Layer.v)
            //ScrollToTop(false);


            nativeView.TableHeaderView = new UIView(); // required to control header height

            headers = new ScrollViewHeader[] {
                //new ListViewHeader(20f) { BackgroundColor = Color.Red.ToUIColor(), blatest = true },
                //new ListViewHeader(30f)  { BackgroundColor = Color.Orange.ToUIColor(), blatest = false },
                //new ListViewHeader(10f)  { BackgroundColor = Color.Yellow.ToUIColor(), blatest = true },
                //new ListViewHeader(50f)  { BackgroundColor = Color.Green.ToUIColor(), blatest = false },
                StatusHeader = new StatusHeader() { Visible = false },
                RefreshControl = new RefreshControl() { Visible = false },
            };

            foreach (var header in headers.Reverse()) {
                nativeView.AddSubview(header);
                header.LayoutChanged += () => UpdateLayout();
            }
            UpdateSpecialViews();
        }


        private float HeaderHeight
        {
            get
            {
                return headers.Sum((header) => (header.Collapsed || !header.Visible) ? 0f : header.Height);
            }
        }

        private void UpdateSpecialViews()
        {
            var totalHeaderHeight = HeaderHeight;
            var totalExpandedHeight = headers.Sum((header) => header.Visible ? header.Height : 0f);
            var offset = Math.Min(Padding.Top + (float)nativeView.ContentOffset.Y + totalExpandedHeight - totalHeaderHeight, 0f);
            var pull = -Math.Min(Padding.Top + (float)nativeView.ContentOffset.Y, 0f);

            offset += totalHeaderHeight;

            foreach (var header in headers.Where((header) => header.Visible)) {
                var headerHeight = header.Height;
                bool collapsed = header.Collapsed;
                var delta = collapsed ? Math.Min(headerHeight, pull) : headerHeight;
                offset -= delta;

                header.PullChanged(pull / headerHeight);
                header.Frame = new CGRect(0, offset, nativeView.Frame.Width, header.Frame.Height);

                if (collapsed)
                    pull -= delta;
            }

            var oldHeight = nativeView.TableHeaderView.Frame.Height;
            var newHeight = totalHeaderHeight + VerticalPadding;
            if (oldHeight != newHeight) {
                nativeView.TableHeaderView.Frame = new CGRect(0, 0, 0, newHeight);
                nativeView.TableHeaderView = nativeView.TableHeaderView;
            }
        }


        
        protected override void UpdateContentLayout()
        {
            var oldInset = nativeView.ContentInset.Top;
            var oldOffset = nativeView.ContentOffset.Y;

            nativeView.BackgroundColor = grouped ? UIColor.FromHSB(0, 0, 0.9f) : UIColor.White; // todo: replace with system color

            nativeView.ScrollIndicatorInsets = new UIEdgeInsets(Padding.Top, Padding.Left, Padding.Bottom, Padding.Right);
            nativeView.ContentInset = new UIEdgeInsets(Padding.Top + VerticalPadding, 0, Padding.Bottom + VerticalPadding, 0); // todo: respect side padding

            UpdateSpecialViews();

            // make sure that the scroll position stays the same when padding changes
            if (!nativeView.Dragging)
                nativeView.ContentOffset = new CGPoint(nativeView.ContentOffset.X, oldOffset + oldInset - nativeView.ContentInset.Top);
            
            base.UpdateContentLayout();
        }


        public void SetEditingMode(bool editing, bool animated)
        {
            if (nativeView.Editing)
                nativeView.SetEditing(false, animated);
            if (editing)
                nativeView.SetEditing(true, animated);
        }

        protected void ReloadAndAdd(int section, bool animated) {
            nativeView.InsertSections(new NSIndexSet((uint)section), (animated ? UITableViewRowAnimation.Middle : UITableViewRowAnimation.None));
            nativeView.ReloadData();
        }
        protected void ReloadAndAdd(int section, int row, bool animated) {
            if (animationMod != 0) {
                nativeView.InsertRows(new NSIndexPath[] { NSIndexPath.FromRowSection(row, section) }, (animated ? UITableViewRowAnimation.Middle : UITableViewRowAnimation.None));
                //nativeView.LayoutSubviews();
            }
        }
        protected void ReloadAndRemove(int section, bool animated) {
            nativeView.DeleteSections(new NSIndexSet((uint)section), (animated ? UITableViewRowAnimation.Middle : UITableViewRowAnimation.None));
        }
        protected void ReloadAndRemove(int section, int row, bool animated) {
            if (animationMod != 0)
                nativeView.DeleteRows(new NSIndexPath[] { NSIndexPath.FromRowSection(row, section) }, (animated ? (animationMod == 2 ? UITableViewRowAnimation.Left : UITableViewRowAnimation.Middle) : UITableViewRowAnimation.None));
        }
        public void ReloadData()
        {
            nativeView.ReloadData();
        }

        public void ScrollToTop(bool animated)
        {
            nativeView.ScrollRectToVisible(new RectangleF(0f, 0f, 1f, 1f), animated);
        }

        public void MakeVisible(int section, int row, bool animated)
        {
            var visible = nativeView.IndexPathsForVisibleRows;
            if (!visible.Any())
                nativeView.ScrollToRow(NSIndexPath.FromRowSection(row, section), UITableViewScrollPosition.Middle, animated);
            else if (IsBefore(section, row, visible.First().Section, visible.First().Row))
                nativeView.ScrollToRow(NSIndexPath.FromRowSection(row, section), UITableViewScrollPosition.Top, animated);
            else if (!IsBefore(section, row, visible.Last().Section, visible.Last().Row))
                nativeView.ScrollToRow(NSIndexPath.FromRowSection(row, section), UITableViewScrollPosition.Bottom, animated);
        }
        public void MakeVisible(ListViewItem item, bool animated)
        {
            var frame = new CGRect(item.Frame.Location, new CGSize(item.Frame.Width, Size.Y - Padding.Top - Padding.Bottom));
            nativeView.ScrollRectToVisible(frame, animated);
            //if (IsBefore(section, row, visible.First().Section, visible.First().Row))
            //    list.ScrollToRow(NSIndexPath.FromRowSection(row, section), UITableViewScrollPosition.Top, animated);
            //else if (!IsBefore(section, row, visible.Last().Section, visible.Last().Row))
            //    list.ScrollToRow(NSIndexPath.FromRowSection(row, section), UITableViewScrollPosition.Bottom, animated);
        }

        /// <summary>
        /// Returns true if cell 1 is before cell 2
        /// </summary>
        private bool IsBefore(int section1, int row1, int section2, int row2)
        {
            if (section2 < section1) return false;
            return (section1 < section2) || (row1 < row2);
        }


        public RefreshControl RefreshControl { get; private set; }
        public StatusHeader StatusHeader { get; private set; }


    }



    public interface IListViewSection
    {
        int NumberOfRows { get; }
        /// <summary>
        /// True if the section has items that can be removed or rearranged.
        /// </summary>
        bool CanEdit { get; }
        string Header { get; }
        string Footer { get; }
        ListViewItem[] SpecialItems { get; }

        /// <summary>
        /// Shall trigger when an item is added to the section.
        /// This corresponds to visual items (including special items), not logical ones.
        /// </summary>
        event EventHandler<int> DidAddItem;

        /// <summary>
        /// Shall trigger when an item is removed from the section.
        /// This corresponds to visual items (including special items), not logical ones.
        /// </summary>
        event EventHandler<int> DidRemoveItem;

        ListViewItem GetItem(int row);
        void MoveItem(int oldRow, int newRow); // only required if editable
        void RemoveItem(int row); // only required if editable
        void StartEditing(); // only required if the section must do something before editing is possible
    }



    public class ListViewSection<T> : IListViewSection, IEnumerable<T>
    {
        private List<T> content = new List<T>();
        private List<ListViewItem> cells = new List<ListViewItem>();
        private Converter<T, ListViewItem> dataItemConstructor;
        private int accessoryItemRow = -1;
        private ListViewItem accessoryItem;
        private string headerPrototype;

        public int NumberOfRows { get { return content.Count() + (accessoryItemRow == -1 ? 0 : 1); } }
        public bool CanEdit { get; private set; }
        public string Header { get { return string.IsNullOrEmpty(headerPrototype) ? null : string.Format(headerPrototype, content.Count()); } set { headerPrototype = value; } }
        public string Footer { get; set; }
        public ListViewItem[] SpecialItems { get; private set; }

        public event EventHandler<int> DidAddItem;
        public event EventHandler<int> DidRemoveItem;

        /// <summary>
        /// Triggered when the user removes an item from the list.
        /// </summary>
        public event EventHandler<T> DidRemoveDataItem;


        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)content).GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return content.GetEnumerator();
        }

        private int DataIndexToCellIndex(int dataIndex)
        {
            if (dataIndex < accessoryItemRow || accessoryItemRow == -1) return dataIndex;
            return dataIndex + 1;
        }

        public ListViewItem GetItem(int row)
        {
            if (row == accessoryItemRow) return accessoryItem;
            if (row > accessoryItemRow && accessoryItemRow != -1) row -= 1;
            return cells[row];
        }
        public ListViewItem GetItem(T item)
        {
            return cells[content.IndexOf(item)];
        }
        public T GetItem(Func<T, bool> predicate)
        {
            return content.First(predicate);
        }
        public void AddItem(int row, T item)
        {
            var cell = dataItemConstructor(item);
            cell.Selected += ToggleAccessoryItem;
            cells.Insert(row, cell);
            content.Insert(row, item);
            DidAddItem.SafeInvoke(this, DataIndexToCellIndex(row));
        }
        public void AddItem(T item)
        {
            AddItem(content.Count(), item);
        }
        public void AddItems(IEnumerable<T> items)
        {
            foreach (var i in items)
                AddItem(i);
        }
        public void MoveItem(int oldRow, int newRow)
        {
            if (accessoryItemRow != -1) throw new Exception("special item must be hidden");

            var item = content[oldRow];
            content.RemoveAt(oldRow);
            content.Insert(newRow, item);

            var cell = cells[oldRow];
            cells.RemoveAt(oldRow);
            cells.Insert(newRow, cell);
        }
        public void RemoveItem(int row)
        {
            cells[row].Selected -= ToggleAccessoryItem;
            cells.RemoveAt(row);
            var oldVal = content[row];
            content.RemoveAt(row);
            DidRemoveItem.SafeInvoke(this, DataIndexToCellIndex(row));
            DidRemoveDataItem.SafeInvoke(this, oldVal);
        }
        public void RemoveItems(Func<T, bool> predicate)
        {
            try {
                List<int> obsolete = new List<int>();
                for (int i = 0; i < content.Count(); i++)
                    if (predicate(content[i]))
                        obsolete.Add(i);
                for (int i = 0; i < obsolete.Count(); i++)
                    RemoveItem(obsolete[i] - i);
            } catch (Exception ex) {
                Application.UILog.Log("could not remove all: " + ex, LogType.Error);
            }
        }

        public void UpdateItem(T item)
        {
            GetItem(item).PerformUpdate();
        }

        public void StartEditing()
        {
            ToggleAccessoryItem(null, null);
        }

        /// <summary>
        /// Shows or resigns the accessory list view item for the specified cell if available
        /// </summary>
        public void ToggleAccessoryItem(object sender, BaseListView owner)
        {
            ListViewItem cell = (ListViewItem)sender;
            
            // resign old accessory view item
            int oldIndex = accessoryItemRow;
            accessoryItemRow = -1;
            accessoryItem = null;
            if (oldIndex != -1) {
                cells[oldIndex - 1].DestructAccessoryItem();
                DidRemoveItem.SafeInvoke(this, oldIndex);
            }

            if (cell != null) accessoryItemRow = cells.IndexOf(cell) + 1; // find index where to insert new item
            if (oldIndex == accessoryItemRow) accessoryItemRow = -1; // don't open if this is the same row as before
            if (accessoryItemRow != -1) // construct new item if neccessary
                if ((accessoryItem = cell.ConstructAccessoryItem()) == null)
                    accessoryItemRow = -1; // don't show if this item has no accessory item

            if (accessoryItemRow != -1) {
                DidAddItem.SafeInvoke(this, accessoryItemRow);
                accessoryItem.LayoutSubviews();
            }
        }


        public ListViewSection(bool canEdit, Converter<T, ListViewItem> dataItemConstructor, ListViewItem[] specialItems)
        {
            CanEdit = canEdit;
            this.dataItemConstructor = dataItemConstructor;
            if (specialItems == null)
                SpecialItems = new ListViewItem[0];
            else
                SpecialItems = specialItems;
            
        }
        public ListViewSection(bool canEdit, Converter<T, ListViewItem> dataItemConstructor)
            : this(canEdit, dataItemConstructor, null)
        {
        }
    }


    public class ListViewSection : ListViewSection<ListViewItem>
    {
        public ListViewSection(bool canRearrange, ListViewItem[] specialItems)
            : base(canRearrange, (i) => i, specialItems)
        {
        }
        public ListViewSection(bool canRearrange)
            : this(canRearrange, null)
        {
        }
    }


    public class ListView : BaseListView
    {
        private List<IListViewSection> sections = new List<IListViewSection>();

        public ListView(bool grouped = true)
            : base(grouped)
        {
        }

        protected override int GetNumberOfSections()
        {
            return sections.Count();
        }
        protected override IListViewSection GetSection(int section)
        {
            return sections[section];
        }

        private void AnimateAdd(object sender, int row)
        {
            int section = sections.IndexOf((IListViewSection)sender);
            ReloadAndAdd(section, row, true);
            MakeVisible(section, row, true);
        }
        private void AnimateRemove(object sender, int row)
        {
            ReloadAndRemove(sections.IndexOf((IListViewSection)sender), row, true);
        }

        /// <summary>
        /// Adds a section to the bottom of the list view
        /// </summary>
        public void AddSection(IListViewSection section)
        {
            AddSection(GetNumberOfSections(), section, false);
        }

        /// <summary>
        /// Adds a section to the list view at the specified index
        /// </summary>
        public void AddSection(int index, IListViewSection section, bool animated = false)
        {
            section.DidAddItem += AnimateAdd;
            section.DidRemoveItem += AnimateRemove;
            sections.Insert(index, section);
            ReloadAndAdd(index, animated);
        }

        /// <summary>
        /// Removes the section with the specified index from the list. The section must be empty.
        /// </summary>
        public void RemoveSection(int index, bool animated = false)
        {
            var section = sections[index];
            section.DidAddItem -= AnimateAdd;
            section.DidRemoveItem -= AnimateRemove;
            sections.RemoveAt(index);
            ReloadAndRemove(index, animated);
        }

        /// <summary>
        /// Removes the specified section from the list. The section must be empty.
        /// </summary>
        public void RemoveSection(IListViewSection section, bool animated = false)
        {
            RemoveSection(sections.IndexOf(section), animated);
        }


        /// <summary>
        /// Removes all items and resets the sections
        /// </summary>
        public void Clear()
        {
            sections.Clear();
            ReloadData();
        }
    }

    /*
    public class ListView<T> : BaseListView
    {
        private List<IListViewSection> sections = new List<IListViewSection>();

        protected override int GetNumberOfSections()
        {
            return sections.Count();
        }
        protected override IListViewSection GetSection(int section)
        {
            return sections[section];
        }

        private void AnimateAdd(object sender, int row)
        {
            int section = sections.IndexOf((IListViewSection)sender);
            ReloadAndAdd(section, row, true);
            MakeVisible(section, row, true);
        }
        private void AnimateRemove(object sender, int row)
        {
            ReloadAndRemove(sections.IndexOf((IListViewSection)sender), row, true);
        }

        /// <summary>
        /// Adds a section to the bottom of the list view
        /// </summary>
        public void AddSection(IListViewSection section)
        {
            AddSection(GetNumberOfSections(), section, false);
        }

        /// <summary>
        /// Adds a section to the list view at the specified index
        /// </summary>
        public void AddSection(int index, IListViewSection section, bool animated = false)
        {
            section.AddedItem += AnimateAdd;
            section.RemovedItem += AnimateRemove;
            sections.Insert(index, section);
            ReloadAndAdd(index, animated);
        }

        /// <summary>
        /// Removes the section with the specified index from the list. The section must be empty.
        /// </summary>
        public void RemoveSection(int index, bool animated = false)
        {
            var section = sections[index];
            section.AddedItem -= AnimateAdd;
            section.RemovedItem -= AnimateRemove;
            sections.RemoveAt(index);
            ReloadAndRemove(index, animated);
        }

        /// <summary>
        /// Removes all items and resets the sections
        /// </summary>
        public void Clear()
        {
            sections.Clear();
            ReloadData();
        }
    }
     * */

}