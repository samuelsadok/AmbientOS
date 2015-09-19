using System;
using System.Windows.Data;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{

    /*internal class DataGridCustomColumn<T> : System.Windows.Controls.DataGridColumn, IComparer
    {
        private Func<T, System.Windows.FrameworkElement> constructor;
        private Func<T, T, int> comparer;
    
        public DataGridCustomColumn(Func<T, System.Windows.FrameworkElement> constructor, Func<T, T, int> comparer)
        {
            this.constructor = constructor;
            this.comparer = comparer;
        }
    
        protected override System.Windows.FrameworkElement GenerateElement(System.Windows.Controls.DataGridCell cell, object dataItem)
        {
            if (dataItem == CollectionView.NewItemPlaceholder) return null;
            return constructor((T)dataItem);
        }
    
        protected override System.Windows.FrameworkElement GenerateEditingElement(System.Windows.Controls.DataGridCell cell, object dataItem)
        {
            throw new NotImplementedException();
        }
    
        public int Compare(object x, object y)
        {
            if (SortDirection == ListSortDirection.Ascending)
                return comparer((T)x, (T)y);
            else
                return comparer((T)y, (T)x);
        }
    }

    public class DataGrid<T> : View<System.Windows.Controls.DataGrid>
    {
        public event Action<T> ItemSelected;

        public TableView(DataSource<T> source, Field<T>[] fields)
        {

            //nativeView.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            //nativeView.VerticalAlignment = System.Windows.VerticalAlignment.Center;

            nativeView.AutoGenerateColumns = false;
            nativeView.GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.All;
            nativeView.AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(Color.LightGrey.ToMediaColor());
            nativeView.SelectionMode = System.Windows.Controls.DataGridSelectionMode.Single;
            nativeView.CanUserAddRows = false; // source.CanConstructItems // doesn't work
            nativeView.CanUserReorderColumns = source.CanMoveItems;
            nativeView.CanUserDeleteRows = source.CanDeleteItems;
            nativeView.CanUserResizeColumns = true;
            nativeView.CanUserResizeRows = false;
            nativeView.RowHeaderWidth = 25f;

            foreach (var f in fields)
                nativeView.Columns.Add(f.Column);

            var view = (System.Windows.Data.ListCollectionView)System.Windows.Data.CollectionViewSource.GetDefaultView(source);
            nativeView.ItemsSource = view;

            nativeView.SelectionChanged += (o, e) => {
                var enumerator = e.AddedItems.GetEnumerator();
                if (!enumerator.MoveNext())
                    ItemSelected.SafeInvoke(default(T));
                else if (enumerator.Current == CollectionView.NewItemPlaceholder)
                    ItemSelected.SafeInvoke(default(T));
                else
                    ItemSelected.SafeInvoke((T)enumerator.Current);
            };

            nativeView.Sorting += (o, e) => {
                if (e.Column != null) {
                    if (e.Column.SortDirection == ListSortDirection.Ascending)
                        e.Column.SortDirection = ListSortDirection.Descending;
                        else
                        e.Column.SortDirection = ListSortDirection.Ascending;
                        view.CustomSort = (DataGridCustomColumn<T>)(e.Column);
                }
                e.Handled = true;
            };

            //nativeView.LoadingRow += (o, e) => {
            //    if (e.Row.Item == CollectionView.NewItemPlaceholder)
            //        e.Row.Header = new System.Windows.Controls.TextBlock() { Text = "*", FontSize = 20f };
            //    else
            //        e.Row.Header = null;
            //    e.Row.UpdateLayout();
            //};
        }

        public void SelectItem(T item)
        {
            nativeView.SelectedItem = item;
            if (item != null)
                nativeView.Focus();
        }
    }*/



    public class TableView<T> : View<System.Windows.Controls.ListView>
    {
        public event Action<T> ItemSelected;

        public class CustomGroupDescription : GroupDescription
        {
            public Func<T, string> NameFactory;

            public CustomGroupDescription(Func<T, string> nameFactory)
            {
                NameFactory = nameFactory;
            }

            public override object GroupNameFromItem(object item, int level, System.Globalization.CultureInfo culture)
            {
                return NameFactory((T)item);
            }
        }


        public TableView(CollectionSource<T> source, Field<T>[] fields, Func<T, string> categoryNameFactory)
        {
            
            //nativeView.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            //nativeView.VerticalAlignment = System.Windows.VerticalAlignment.Center;


            var grid = new System.Windows.Controls.GridView() {

            };
            

            // nativeView.AutoGenerateColumns = false;
            // nativeView.GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.All;
            // nativeView.AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(Color.LightGrey.ToMediaColor());
            // nativeView.SelectionMode = System.Windows.Controls.DataGridSelectionMode.Single;
            // nativeView.CanUserAddRows = false; /* source.CanConstructItems // doesn't work */
            // nativeView.CanUserReorderColumns = source.CanMoveItems;
            // nativeView.CanUserDeleteRows = source.CanDeleteItems;
            // nativeView.CanUserResizeColumns = true;
            // nativeView.CanUserResizeRows = false;
            // nativeView.RowHeaderWidth = 25f;


           //var t = new System.Windows.DataTemplate(typeof(testtxt<System.Windows.Controls.TextBlock, >));
           //System.Windows.FrameworkElementFactory ff = new System.Windows.FrameworkElementFactory(typeof(testtxt));
           //t.VisualTree = ff;
           //System.Windows.Data.Binding b = new Binding("");
           //ff.SetBinding(testtxt.dp, b);
            //ff.SetValue



            foreach (var f in fields)
                foreach (var c in f.Columns)
                    grid.Columns.Add(c);
            
            nativeView.View = grid;
            
            var view = (System.Windows.Data.ListCollectionView)System.Windows.Data.CollectionViewSource.GetDefaultView(source.Data);

            if (categoryNameFactory != null)
                view.GroupDescriptions.Add(new CustomGroupDescription(categoryNameFactory));

            var template = new System.Windows.Controls.ControlTemplate();
            

            var groupStype = new System.Windows.Controls.GroupStyle();
            groupStype.ContainerStyle = new System.Windows.Style(typeof(System.Windows.Controls.GroupItem));

            var groupExpanderTemplate =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
                                 "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
                                 "TargetType='{x:Type GroupItem}'>" +
                    "<Expander IsExpanded='true'>" +
                        "<Expander.Header><ContentPresenter /></Expander.Header>" +
                        "<Expander.Content><ItemsPresenter /></Expander.Content>" +
                    "</Expander>" +
                "</ControlTemplate>";

            groupStype.ContainerStyle.Setters.Add(
                new System.Windows.Setter(
                    System.Windows.Controls.GroupItem.TemplateProperty,
                    System.Windows.Markup.XamlReader.Parse(groupExpanderTemplate)
                )
            );
            
            nativeView.GroupStyle.Add(groupStype);
            nativeView.ItemsSource = view;

            nativeView.SelectionChanged += (o, e) => {
                var enumerator = e.AddedItems.GetEnumerator();
                if (!enumerator.MoveNext())
                    ItemSelected.SafeInvoke(default(T));
                else if (enumerator.Current == CollectionView.NewItemPlaceholder)
                    ItemSelected.SafeInvoke(default(T));
                else
                    ItemSelected.SafeInvoke((T)enumerator.Current);
            };

            //nativeView.Sorting += (o, e) => {
            //    if (e.Column != null) {
            //        if (e.Column.SortDirection == ListSortDirection.Ascending)
            //            e.Column.SortDirection = ListSortDirection.Descending;
            //        else
            //            e.Column.SortDirection = ListSortDirection.Ascending;
            //        view.CustomSort = (DataGridCustomColumn<T>)(e.Column);
            //    }
            //    e.Handled = true;
            //};
        }

        public void SelectItem(T item)
        {
            nativeView.SelectedItem = item;
            if (item != null)
                nativeView.Focus();
        }
    }


    public class TreeView<TTree, TItem> : View<System.Windows.Controls.TreeView>
    {
        public event Action<TTree> TreeSelected;
        public event Action<TItem> ItemSelected;

        public TreeView(TreeSource<TTree, TItem> source, Field<TTree>[] folderFields, Field<TItem>[] itemFields)
        {
            nativeView.ItemTemplateSelector = new HierarchicalTemplateSelector<TTree, TItem>((obj) => Field<TTree>.JoinViews(obj.Data, folderFields), (obj) => Field<TItem>.JoinViews(obj, itemFields));
            nativeView.ItemsSource = source.Content;

            nativeView.SelectedItemChanged += (o, e) => {
                Application.UILog.Log("selected item type = " + e.NewValue.GetType());
                if (e.NewValue is TreeSource<TTree, TItem>)
                    TreeSelected.SafeInvoke(((TreeSource<TTree, TItem>)e.NewValue).Data);
                else
                    ItemSelected.SafeInvoke((TItem)e.NewValue);
            };
        }
    }
}
