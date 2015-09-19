using System;
using UIKit;
using AppInstall.Framework;

namespace AppInstall.UI
{

    public interface IToolbarItem
    {
        UIBarButtonItem Item { get; }
    }

    public class ToolbarSpacer : IToolbarItem
    {
        public UIBarButtonItem Item { get; private set; }

        public ToolbarSpacer()
        {
            Item = new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace);
        }
    }

    public class ToolbarButton : IToolbarItem
    {
        public UIBarButtonItem Item { get; private set; }
        public bool Enabled { get { return Item.Enabled; } set { Item.Enabled = value; } }

        public event Action<IToolbarItem> Triggered;

        /// <summary>
        /// note: this may possibly be removed, and the iOS type used directly
        /// </summary>
        public enum iOSNavigationBarItemType
        {
            Action,
            Add,
            Edit,
            Delete,
            Done
        }

        private UIBarButtonSystemItem Convert(iOSNavigationBarItemType type)
        {
            switch (type) {
                case iOSNavigationBarItemType.Action: return UIBarButtonSystemItem.Action;
                case iOSNavigationBarItemType.Add: return UIBarButtonSystemItem.Add;
                case iOSNavigationBarItemType.Edit: return UIBarButtonSystemItem.Edit;
                case iOSNavigationBarItemType.Delete: return UIBarButtonSystemItem.Trash;
                case iOSNavigationBarItemType.Done: return UIBarButtonSystemItem.Done;
                default: throw new NotImplementedException(type.ToString() + " not implemented");
            }
        }

        public ToolbarButton(iOSNavigationBarItemType type)
        {
            Item = new UIBarButtonItem(Convert(type), (o, e) => Triggered.SafeInvoke(this));
        }

        public ToolbarButton(string title)
        {
            Item = new UIBarButtonItem(title, UIBarButtonItemStyle.Plain, (o, e) => Triggered.SafeInvoke(this));
        }
    }

}