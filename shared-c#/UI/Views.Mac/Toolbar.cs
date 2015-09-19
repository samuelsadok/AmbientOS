using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Foundation;
using UIKit;
using AppInstall.Framework;

namespace AppInstall.UI
{

    public class Toolbar : View<UIToolbar>
    {
        private class ToolbarDelegate : UIToolbarDelegate
        {
            public override UIBarPosition GetPositionForBar(IUIBarPositioning barPositioning)
            {
                return UIBarPosition.Any;
            }
        }

        public Toolbar()
        {
            nativeView.Translucent = true;
            nativeView.Delegate = new ToolbarDelegate();
        }
        public Toolbar(params IToolbarItem[] items)
            : this()
        {
            SetItems(false, items);
        }
        public override bool IsOpaque()
        {
            return !nativeView.Translucent;
        }

        public void SetItems(bool animated, params IToolbarItem[] items)
        {
            nativeView.SetItems((from i in items select i.Item).ToArray(), animated);
        }
    }

}