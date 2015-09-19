using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{
    public abstract partial class DetailViewController<TItem, TData> : DataViewController<TData>
        where TData : DataSource
    {
        private View emptyDetail;
        private View currentDetail;

        private LayerLayout detailContainer; // null if this instance was used inside a navigation view

        private NavigationPage mainPage; // null if this instance was used as a stand-alone view
        protected NavigationView nav; // null if this instance was used as a stand-alone view


        /// <summary>
        /// Shall construct a self-contained view that displays the items of which details are to be shown.
        /// If this function is not overridden, a navigation view will be constructed with the page returned by ConstructMainNavigationPage.
        /// </summary>
        protected virtual View ConstructMainView()
        {
            NavigationView nav = new NavigationView();
            nav.NavigateForward(ConstructNavigationPageEx(nav));
            return nav;
        }

        /// <summary>
        /// Shall construct a navigation page that provides a way of requesting a detail view of some items.
        /// </summary>
        protected abstract NavigationPage ConstructMainNavigationPage(NavigationView nav);

        /// <summary>
        /// Shall construct one or multiple list view sections that contain the items of which to display details.
        /// </summary>
        protected abstract IEnumerable<IListViewSection> ConstructMainListViewSections(NavigationView nav, NavigationPage page, ListView listView, FeatureList features, bool complete);

        protected void ShowDetail(TItem item, bool isNew)
        {
            if (DetailViewConstructor == null)
                return;

            if (nav == null) {
                // update detail view container to contain the detail view
                var newView = item == null ? emptyDetail : DetailViewConstructor(this, item, isNew).ConstructView();
                detailContainer.Replace(currentDetail, newView, true, false);
                currentDetail = newView;
            } else {
                // nagivate forward to detail view
                if (item == null)
                    nav.NavigateBack(mainPage, true);
                else
                    nav.NavigateForward(DetailViewConstructor(this, item, isNew).ConstructNavigationPage(nav), true, false);
            }
        }

        /// <summary>
        /// Must be invoked by a deriving class to display details for some data instance.
        /// </summary>
        protected void ShowDetail(TItem item)
        {
            ShowDetail(item, false);
        }

        protected override View ConstructViewEx()
        {
            // todo: maybe add split view style for ipad
            /*
            var mainContainer = ConstructMainView();

            if (DetailViewConstructor == null)
                return mainContainer;

            currentDetail = emptyDetail = new Label() { Text = PlaceholderText };
            detailContainer = new LayerLayout();
            detailContainer.Insert(currentDetail, false);

            var split = new GridLayout(1, 2);
            split.RelativeColumnWidths[0] = 2f;
            split.RelativeColumnWidths[1] = 3f;
            split.RelativeRowHeights[0] = 1f;
            split[0, 0] = mainContainer;
            split[0, 1] = detailContainer;

            return split;
            */

            return base.ConstructViewEx();
        }

        protected override NavigationPage ConstructNavigationPageEx(NavigationView nav)
        {
            this.nav = nav;
            mainPage = ConstructMainNavigationPage(nav);
            return mainPage;
        }

        protected override IEnumerable<IListViewSection> ConstructListViewSectionsEx(NavigationView nav, NavigationPage page, ListView listView, FeatureList features, bool complete)
        {
            mainPage = page;
            this.nav = nav;
            return ConstructMainListViewSections(nav, page, listView, features, complete);
        }
    }
}
