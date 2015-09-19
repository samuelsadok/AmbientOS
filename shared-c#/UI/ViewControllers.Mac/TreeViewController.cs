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
    /// Displays a tree of items, while a tree item may have a different
    /// appearance than a leaf item.
    /// </summary>
    public partial class TreeViewController<TTree, TItem> : DetailViewController<TItem, TreeSource<TTree, TItem>>
    {
        private ListViewItem ListViewItemConstructor(TreeSource<TTree, TItem> item, NavigationView nav)
        {
            var i = Field<TTree>.ConstructListViewItem(item.Data, (obj) => nav.NavigateForward(ConstructPage(item, nav), true, false), nav, FolderFields);
            return i;
        }

        private ListViewItem ListViewItemConstructor(TItem item, NavigationView nav)
        {
            var i = Field<TItem>.ConstructListViewItem(item, DetailViewConstructor == null ? (Action<TItem>)null : ShowDetail, nav, ItemFields);
            return i;
        }

        private NavigationPage ConstructPage(TreeSource<TTree, TItem> tree, NavigationView nav)
        {
            ListView listView = new ListView();

            var nodes = new ListViewSection<TreeSource<TTree, TItem>>(false, (item) => ListViewItemConstructor(item, nav), null);
            nodes.AddItems(tree.Subfolders);
            listView.AddSection(nodes);

            var leafs = new ListViewSection<TItem>(false, (item) => ListViewItemConstructor(item, nav), null);
            leafs.AddItems(tree.Items);
            listView.AddSection(leafs);

            NavigationPage page = new NavigationPage() {
                Title = Title,
                View = listView
            };

            var features = new FeatureList(GetFeatures());
            AddFeatures(features, listView);
            AddFeatures(features, page);
            features.AssertEmpty();

            return page;
        }

        protected override NavigationPage ConstructMainNavigationPage(NavigationView nav)
        {
            var page = ConstructPage(Data, nav);
            return page;
        }

        protected override IEnumerable<IListViewSection> ConstructMainListViewSections(NavigationView nav, NavigationPage page, ListView listView, FeatureList features, bool complete)
        {
            if (features.Features.Any()) // todo: implement
                throw new NotImplementedException("todo: add commands");

            var section = new ListViewSection(false, AddFeatures(features));
            if (complete)
                section.Header = Title;

            section.AddItems(Data.Subfolders.Select((item) => ListViewItemConstructor(item, nav)));
            section.AddItems(Data.Items.Select((item) => ListViewItemConstructor(item, nav)));

            return new IListViewSection[] { section };
        }
    }
}
