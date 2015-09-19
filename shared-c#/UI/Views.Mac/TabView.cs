using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Foundation;
using UIKit;
using AppInstall.Framework;

namespace AppInstall.UI
{

    public enum TabItemStyle
    {
        Contacts,
        History,
        More,
        Search,
        Custom
    }

    public class TabPage
    {
        public string Label { get; set; }
        public TabItemStyle Style { get; set; }
        public string Icon { get; set; }

        /// <summary>
        /// Either View or PageConstructor must be null
        /// </summary>
        public View View { get; set; }

        /// <summary>
        /// Either View or PageConstructor must be null
        /// </summary>
        public Func<NavigationView, NavigationPage> PageConstructor { get; set; }
    }

    public class TabView : LayerLayout
    {

        /*public class TabViewController : UITabBarController
        {
            public TabViewController()
            {
                UIViewController v = new UIViewController();
                v.View = bla;
                v.tab
            }
        }*/


        private class TabBar : View<UITabBar>
        {
            private TabPage[] pages;
            private UITabBarItem[] items;

            public event Action<TabPage, int> SelectedItem;

            public TabBar()
                : base(true)
            {
            }

            private UITabBarItem ConstructItem(TabPage page, int tag)
            {
                switch (page.Style) {
                    case TabItemStyle.Contacts: return new UITabBarItem(UITabBarSystemItem.Contacts, tag);
                    case TabItemStyle.History: return new UITabBarItem(UITabBarSystemItem.History, tag);
                    case TabItemStyle.More: return new UITabBarItem(UITabBarSystemItem.More, tag);
                    case TabItemStyle.Search: return new UITabBarItem(UITabBarSystemItem.Search, tag);
                    case TabItemStyle.Custom: return new UITabBarItem(page.Label, UIImage.FromFile(page.Icon), tag);
                    default: throw new NotImplementedException();
                }
            }

            public void SetItems(TabPage[] pages, int selectedItem)
            {
                //this.nativeView.BarStyle = UIBarStyle.Default;
                //this.nativeView.ItemPositioning = UITabBarItemPositioning.Centered;
                //this.nativeView.ItemSpacing = 10;
                //this.nativeView.ItemWidth = 50;


                this.nativeView.ItemSelected += (o, e) =>
                    SelectedItem.SafeInvoke(pages[(int)e.Item.Tag], (int)e.Item.Tag);

                items = new UITabBarItem[pages.Count()];
                for (int i = 0; i < pages.Count(); i++) {
                    var item = ConstructItem(pages[i], i);
                    item.Title = pages[i].Label;
                    items[i] = item;
                }
                this.nativeView.SetItems(items, false);

                this.pages = pages;

                // apply initial tab choice
                this.nativeView.SelectedItem = items[selectedItem];
            }

            /// <summary>
            /// Sets the selected item without triggering an event.
            /// </summary>
            public void SetSelectedItem(int item)
            {
                this.nativeView.SelectedItem = items[item];
            }
        }

        public TabView(TabPage[] pages)
        {
            var bars = new TabBar[pages.Count()];
            var selectedItem = 0;

            // modify each page to contain a tab bar
            for (int i = 0; i < pages.Count(); i++) {
                var page = pages[i];
                var bar = bars[i] = new TabBar();
                Action navigateToRoot = null;

                if ((page.View == null) == (page.PageConstructor == null)) {
                    throw new InvalidOperationException("a tab page must either have the View or Page property set (but not both)");
                } else if (page.View == null) {
                    NavigationView nav = new NavigationView();
                    NavigationPage navPage = page.PageConstructor(nav);
                    navPage.View = new FramedLayout() { Content = navPage.View, BottomBar = bar };
                    nav.NavigateForward(navPage);
                    page.View = nav;
                    //page.View = new FramedLayout() { Content = new Button() { Text = "bla" }, BottomBar = new TabBar() };

                    // when the tab bar item is selected twice, navigate to root page
                    navigateToRoot = () => nav.NavigateBack(navPage, true);
                } else {
                    page.View = new FramedLayout() { Content = page.View, BottomBar = bar };
                }

                bar.SelectedItem += (p, idx) => {
                    if (selectedItem != idx) {
                        this.Replace(p.View);
                        selectedItem = idx;
                        foreach (var b in bars)
                            b.SetSelectedItem(selectedItem);
                    } else if (navigateToRoot != null) {
                        navigateToRoot();
                    }
                };
                bar.SetItems(pages, selectedItem);
            }

            this.Replace(pages[selectedItem].View);
            // todo: "more"-functionality
        }
    }
}
