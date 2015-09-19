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
    /// A view controller on iOS may display its content in different modes depending on the situation in which it is shown.
    /// </summary>
    public abstract partial class ViewController
    {


        /// <summary>
        /// Adds a list of features to the navigation page in the following manner:
        /// If there are only a few buttons, they are added to the navigation bar.
        /// If there are more buttons, a toolbar is inserted at the bottom.
        /// If there's still not enough space, the a special "action"-button is added with a popup list of available commands.
        /// todo: comply with this description
        /// The features that could be added (i.e. all of them) are removed from the list.
        /// </summary>
        protected static void AddFeatures(FeatureList features, NavigationPage page)
        {
            var f = features.Features.ToArray();
            var relevantItems = f.Where((feature) => (feature.SupportedModes & FeatureController.DisplayMode.ToolbarItem) != FeatureController.DisplayMode.None).ToArray();
            features.Features = f.Except(relevantItems).ToArray();

            page.NavigationBarItems = relevantItems.Select((feature) => feature.ConstructToolbarButton()).ToArray();
            page.AlternativeNavigationBarItems = relevantItems.Select((feature) => feature.ConstructAlternativeToolbarButton()).Where((item) => item != null).ToArray();
        }

        /// <summary>
        /// Adds a list of features to the list view.
        /// Any feature that supports the "ListFeature"-mode is added to the list view and removed from the feature list.
        /// </summary>
        /// <param name="features"></param>
        /// <param name="listView"></param>
        protected static void AddFeatures(FeatureList features, ListView listView)
        {
            var f = features.Features.ToArray();
            var relevantItems = f.Where((feature) => (feature.SupportedModes & FeatureController.DisplayMode.ListFeature) != FeatureController.DisplayMode.None).ToArray();
            features.Features = f.Except(relevantItems).ToArray();

            foreach (var feature in relevantItems)
                feature.AddToList(listView);
        }

        /// <summary>
        /// Adds a list of features to a list view section in the following manner:
        /// For each feature that has no alternative mode, a list view item is returned.
        /// This excludes for instance the edit button.
        /// All features that could be displayed are removed from the list.
        /// </summary>
        protected static ListViewItem[] AddFeatures(FeatureList features)
        {
            var f = features.Features.ToArray();
            var relevantItems = f.Where((feature) => (feature.SupportedModes & FeatureController.DisplayMode.ListViewItem) != FeatureController.DisplayMode.None).ToArray();
            features.Features = f.Except(relevantItems).ToArray();

            return relevantItems.Select((item) => item.ConstructListViewItem()).ToArray();
        }




        /// <summary>
        /// Shall construct a view that represents the content of this view controller.
        /// If this function is not overridden, a navigation view will be constructed with the page returned by ConstructNavigationPageEx.
        /// This function is always invoked on the UI thread.
        /// </summary>
        protected virtual View ConstructViewEx()
        {
            NavigationView nav = new NavigationView();
            nav.NavigateForward(ConstructNavigationPageEx(nav));
            return nav;
        }

        /// <summary>
        /// Returns a view that represents the content of this view controller.
        /// This function is thread-safe.
        /// </summary>
        public View ConstructView()
        {
            return Platform.EvaluateOnMainThread(ConstructViewEx);
        }

        /// <summary>
        /// Shall construct a navigation page that represents the content of this view controller.
        /// If this function is not overridden, a navigation page will be constructed containing a list view with the sections returned by ConstructListViewSectionsEx.
        /// This function is always invoked on the UI thread.
        /// </summary>
        /// <exception cref="NotSupportedException">This view controller cannot be displayed as a navigation page</exception>
        protected virtual NavigationPage ConstructNavigationPageEx(NavigationView nav)
        {
            ListView listView = new ListView();

            NavigationPage page = new NavigationPage() {
                Title = Title,
                Subtitle = Subtitle,
                View = listView
            };

            if (TitleSource != null) {
                TitleSource.ValueChanged += str => page.Title = str;
                page.Title = TitleSource.Get(); // todo: check what update action is required (also for subtitle)
            }

            if (SubtitleSource != null) {
                SubtitleSource.ValueChanged += str => page.Subtitle = str;
                page.Subtitle = SubtitleSource.Get();
            }

            var features = new FeatureList(GetFeatures());

            foreach (var s in ConstructListViewSectionsEx(nav, page, listView, features, false))
                listView.AddSection(s);

            AddFeatures(features, listView);
            AddFeatures(features, page);

            features.AssertEmpty();

            return page;
        }

        /// <summary>
        /// Returns a navigation page that represents the content of this view controller.
        /// This function is thread-safe.
        /// </summary>
        public NavigationPage ConstructNavigationPage(NavigationView nav)
        {
            return Platform.EvaluateOnMainThread(() => ConstructNavigationPageEx(nav));
        }

        /// <summary>
        /// Shall construct a set of list view sections that represent the content of this view controller.
        /// In most cases this will be a single section.
        /// This function is always invoked on the UI thread.
        /// </summary>
        /// <param name="complete">If false, the list view sections should not include header and commands</param>
        /// <exception cref="NotSupportedException">This view controller cannot be displayed as a list view section</exception>
        protected abstract IEnumerable<IListViewSection> ConstructListViewSectionsEx(NavigationView nav, NavigationPage page, ListView listView, FeatureList features, bool complete);
        
        /// <summary>
        /// Returns a set of list view sections that represent the content of this view controller.
        /// In most cases this will be a single section.
        /// This function is thread-safe.
        /// </summary>
        public IEnumerable<IListViewSection> ConstructListViewSections(NavigationView nav, NavigationPage page, ListView listView, FeatureList features)
        {
            return Platform.EvaluateOnMainThread(() => ConstructListViewSectionsEx(nav, page, listView, features, true));
        }

        /// <summary>
        /// Constructs a single list view item that represents this view controller.
        /// When selected, the navigation view will be used to display the actual view.
        /// </summary>
        public ListViewItem ConstructListViewItem(NavigationView nav)
        {
            return Platform.EvaluateOnMainThread(() => {
                ListViewItem item = new ListViewItem(true, false, Subtitle != null || SubtitleSource != null) {
                    Text = Title,
                    Subtitle = Subtitle,
                    IsSelectable = true,
                    PersistentSelection = false
                };

                if (TitleSource != null) {
                    TitleSource.ValueChanged += str => item.Text = str;
                    item.Text = TitleSource.Get(); // todo: check what update action is required (also for subtitle)
                }

                if (SubtitleSource != null) {
                    SubtitleSource.ValueChanged += str => item.Subtitle = str;
                    item.Subtitle = SubtitleSource.Get();
                }

                item.Selected += (o, e) => nav.NavigateForward(ConstructNavigationPage(nav), true, true);
                return item;
            });
        }
    }
}