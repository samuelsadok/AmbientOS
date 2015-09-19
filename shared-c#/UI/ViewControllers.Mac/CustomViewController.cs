using System;
using System.Collections.Generic;
using AppInstall.Framework;

namespace AppInstall.UI
{
    partial class CustomViewController<T> : DataViewController<DataSource<T>>
    {
        protected override NavigationPage ConstructNavigationPageEx(NavigationView nav)
        {
            var view = ViewConstructor();

            NavigationPage page = new NavigationPage() {
                Title = Title,
                View = view
            };

            var features = new FeatureList(GetFeatures());
            AddFeatures(features, page);
            features.AssertEmpty();

            return page;
        }

        protected override IEnumerable<IListViewSection> ConstructListViewSectionsEx(NavigationView nav, NavigationPage page, ListView listView, FeatureList features, bool complete)
        {
            var section = new ListViewSection(false);
            section.AddItem(ConstructListViewItem(nav));
            yield return section;
        }
    }
}