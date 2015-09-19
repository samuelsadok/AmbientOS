using System;
using System.Collections.Generic;
using System.Linq;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class NavigationView : FramedLayout
    {
        private const int ANIMATION_DURATION = 300;

        private NavigationBar navBar = new NavigationBar();
        private Stack<NavigationPage> pages = new Stack<NavigationPage>();
        private Stack<bool> didReplace = new Stack<bool>();
        private LayerLayout layout = new LayerLayout();

        public NavigationView()
        {
            TopBar = navBar;
            navBar.UserNavigationBack += () => ShowPrevious(true);
            Content = layout;
        }

        public NavigationPage TopPage { get { return pages.Peek(); } }
        public EventHandler<NavigationPage> DidNavigateBack;

        /// <summary>
        /// Navigates one page forward without animation
        /// </summary>
        public void NavigateForward(NavigationPage page)
        {
            NavigateForward(page, false, false);
        }

        /// <summary>
        /// Navigates one page forward 
        /// </summary>
        public void NavigateForward(NavigationPage page, bool animated, bool replace)
        {
            navBar.NavigateForward(page.Title, animated, page.NavigationBarItems);

            if (replace)
                layout.Replace(TopPage.View, page.View, false, false, new Vector2D<float>(-1f, 0f), animated ? ANIMATION_DURATION : 0);
            else
                layout.Insert(page.View, true, new Vector2D<float>(1f, 0f), animated ? ANIMATION_DURATION : 0);

            pages.Push(page);
            didReplace.Push(replace);
        }

        public void NavigateBack(bool animated)
        {
            if (!pages.Any()) throw new InvalidOperationException("the navigation stack is empty");
            ShowPrevious(animated);
            navBar.NavigateBack(animated);
        }

        /// <summary>
        /// Navigates back to the specified page. The page must be on the stack.
        /// </summary>
        public void NavigateBack(NavigationPage page, bool animated)
        {
            while (!(TopPage == page)) {
                if (!pages.Any()) throw new ArgumentException("the navigation stack does not contain the page", "page");
                ShowPrevious(animated);
                navBar.NavigateBack((TopPage == page) && animated);
            }
        }

        private void ShowPrevious(bool animated)
        {
            var toRemove = pages.Pop();

            toRemove.WillRemoveAction.SafeInvoke();

            if (didReplace.Pop())
                layout.Replace(toRemove.View, TopPage.View, false, false, new Vector2D<float>(1f, 0f), animated ? ANIMATION_DURATION : 0);
            else
                layout.Remove(toRemove.View, true, new Vector2D<float>(1f, 0f), animated ? ANIMATION_DURATION : 0);

            DidNavigateBack.SafeInvoke(this, TopPage);
        }

        public void EnableAlternativeMode(bool enabled, bool animated)
        {
            pages.Peek().AlternativeMode = enabled;
            navBar.ExchangeNavigationBarItems(animated, pages.Peek().NavigationBarItems);
        }
    }


    public class NavigationPage
    {
        public string Title { get; set; }
        /// <summary>
        /// currently not used
        /// </summary>
        public string Subtitle { get; set; }
        public View View { get; set; }
        public Action WillRemoveAction { get; set; } // todo: improve design
        public bool AlternativeMode { get; set; }
        public IToolbarItem[] NavigationBarItems { get { return (AlternativeMode ? alternativeItems : defaultItems); } set { defaultItems = value; } }
        public IToolbarItem[] AlternativeNavigationBarItems { set { alternativeItems = value; } }

        private IToolbarItem[] defaultItems;
        private IToolbarItem[] alternativeItems;

    }
}