using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public partial class EditorViewController<T> : DataViewController<DataSource<T>>
    {
        /// <summary>
        /// If the editor consists of a a single plain list view item, a navigation page containing only this view is returned,
        /// else the base class implementation is called to construct the navigation page from the listview sections.
        /// </summary>
        protected override NavigationPage ConstructNavigationPageEx(NavigationView nav)
        {
            var items = Fields.Select((field) => field.ConstructListViewItem(Data.Data) as PlainListViewItem).Where(x => x != null).ToArray();
            if (items.Count() != 1)
                return base.ConstructNavigationPageEx(nav);

            var page = new NavigationPage() {
                Title = Title,
                View = items.Single().View
            };

            var features = new FeatureList(GetFeatures());
            AddFeatures(features, page);
            features.AssertEmpty();

            return page;
        }

        /// <summary>
        /// Constructs one or multiple listview sections that contain a list view item for each field.
        /// Some special fields (e.g. large text fields) get their own separate section for a more compelling visual appearance.
        /// </summary>
        protected override IEnumerable<IListViewSection> ConstructListViewSectionsEx(NavigationView nav, NavigationPage page, ListView listView, FeatureList features, bool complete)
        {
            var groupedItems = (from field in Fields where !(field.HasLargeEditor && field.EditorShowsValue) select field.ConstructListViewItem(Data.Data)).ToArray();
            var standaloneItems = (from field in Fields where field.HasLargeEditor && field.EditorShowsValue select field.ConstructListViewItem(Data.Data)).ToArray();

            if (groupedItems.Any()) {
                var section = new ListViewSection(false);
                if (complete)
                    section.Header = Title;

                section.AddItems(groupedItems);

                yield return section;
            }

            foreach (var item in standaloneItems) {
                var section = new ListViewSection(false);
                section.AddItem(item);
                yield return section;
            }
        }
    }
}
