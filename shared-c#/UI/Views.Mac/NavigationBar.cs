using System;
using System.Collections.Generic;
using System.Linq;
using UIKit;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class NavigationBar : View<UINavigationBar>
    {
        private NavBarDelegate navBarDel = new NavBarDelegate();
        private Stack<NavigationPage> navItems = new Stack<NavigationPage>();

        public event Action NavigatedForwared;
        public event Action UserNavigationBack;
        public event Action NavigatedBack;

        private class NavBarDelegate : UINavigationBarDelegate
        {
            public bool CodeBackNavigation { get; set; }

            public event Action NavigatedForward;
            public event Action UserNavigationBack;
            public event Action NavigatedBack;

            public override UIBarPosition GetPositionForBar(IUIBarPositioning barPositioning)
            {
                return UIBarPosition.TopAttached;
            }

            public override void DidPushItem(UINavigationBar navigationBar, UINavigationItem item)
            {
                NavigatedForward.SafeInvoke();
            }

            public override void DidPopItem(UINavigationBar navigationBar, UINavigationItem item)
            {
                NavigatedBack.SafeInvoke();
            }

            public override bool ShouldPopItem(UINavigationBar navigationBar, UINavigationItem item)
            {
                Application.UILog.Log("will nav back");

                if (!CodeBackNavigation)
                    UserNavigationBack.SafeInvoke();

                return true;
            }
        }

        public NavigationBar()
        {
            nativeView.Translucent = true;
            nativeView.Delegate = navBarDel;
            navBarDel.NavigatedForward += () => NavigatedForwared.SafeInvoke();
            navBarDel.UserNavigationBack += () => {
                Application.UILog.Log("user did nav back");
                UserNavigationBack.SafeInvoke();
                Application.UILog.Log("did invoke");
            };
            navBarDel.NavigatedBack += () => NavigatedBack.SafeInvoke();
        }
        public override bool IsOpaque()
        {
            return !nativeView.Translucent;
        }

        public void NavigateForward(string title, bool animated, params IToolbarItem[] items)
        {
            if (items == null)
                items = new IToolbarItem[0];
            nativeView.PushNavigationItem(new UINavigationItem(title) { RightBarButtonItems = (from i in items select i.Item).Reverse().ToArray() }, animated);
            nativeView.LayoutSubviews();
        }

        public void NavigateBack(bool animated)
        {
            navBarDel.CodeBackNavigation = true;
            nativeView.PopNavigationItem(animated);
            navBarDel.CodeBackNavigation = false;
        }

        public void ExchangeNavigationBarItems(bool animated, params IToolbarItem[] items)
        {
            nativeView.TopItem.SetRightBarButtonItems((from i in items select i.Item).Reverse().ToArray(), animated);
        }
    }
}