using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Foundation;
using UIKit;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public class ListViewItem : UITableViewCell
    {
        public const float DEFAULT_HEIGHT = 44f;

        public event Action TextChanged;
        public event Action SubtitleChanged;
        public event Action AccessoryItemExpanded;
        public event Action AccessoryItemCollapsed;

        private View accessoryView;

        /// <summary>
        /// The main text of the item.
        /// </summary>
        public string Text { get { return TextLabel.Text; } set { TextLabel.Text = value; TextChanged.SafeInvoke(); } }
        /// <summary>
        /// A subtitle below the text. Can only be used when the item was created with the subtitle flag.
        /// </summary>
        public string Subtitle { get { return DetailTextLabel.Text; } set { DetailTextLabel.Text = value; SubtitleChanged.SafeInvoke(); } }
        /// <summary>
        /// Sets the accessory on the right side of the cell
        /// </summary>
        public new View AccessoryView { get { return accessoryView; } set { base.AccessoryView = new PlatformViewWrapper(accessoryView = value).NativeView; } }
        //public float AccessoryViewRightMargin { get { return Frame.Width - base.AccessoryView.Frame.Width - base.AccessoryView.Frame.X; } }
        //public float LeftMargin { get { return TextLabel.Frame.Left; } }
        /// <summary>
        /// The color of the main text
        /// </summary>
        public Color TextColor { get { return TextLabel.TextColor.ToColor(); } set { TextLabel.TextColor = value.ToUIColor(); } }
        /// <summary>
        /// The color of the main text
        /// </summary>
        public Color SubtitleColor { get { return DetailTextLabel.TextColor.ToColor(); } set { DetailTextLabel.TextColor = value.ToUIColor(); } }
        /// <summary>
        /// Specifies if the item can be selected
        /// </summary>
        public bool IsSelectable { get; set; }
        /// <summary>
        /// It the selection is not persistent, the item will only be highlighted while it is being tapped on.
        /// </summary>
        public bool PersistentSelection { get; set; }
        /// <summary>
        /// Specifies the height of this cell
        /// </summary>
        public virtual float Height { get; set; }
        public Converter<ListViewItem, ListViewItem> AccessoryItemConstructor { private get; set; }

        private bool isChecked = false;
        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                var wasChecked = isChecked;
                isChecked = value;
                Accessory = (isChecked ? UITableViewCellAccessory.Checkmark : UITableViewCellAccessory.None);
                if (isChecked != wasChecked) CheckedChanged.SafeInvoke(this, isChecked);
            }
        }
        public EventHandler<bool> CheckedChanged;

        /// <summary>
        /// Specifes the action that should be carried out to upate the content of this listview item.
        /// If not set, no action will be taken.
        /// </summary>
        public Action<ListViewItem> UpdateAction { private get; set; }

        /// <summary>
        /// Triggered when the user tapped on the item
        /// </summary>
        public new event EventHandler<BaseListView> Selected;
        /// <summary>
        /// Triggered for instance when the user tapped on another item
        /// </summary>
        public event EventHandler<BaseListView> Deselected;
        /// <summary>
        /// Triggered when the user tapped on the info button on the right side
        /// </summary>
        public event EventHandler<BaseListView> InfoButtonClicked;

        public ListViewItem(bool disclosureIndicator, bool detailButton, bool subtitle)
            : base((subtitle ? UITableViewCellStyle.Subtitle : UITableViewCellStyle.Default), new Guid().ToString())
        {
            base.Accessory = disclosureIndicator ? (detailButton ? UITableViewCellAccessory.DetailDisclosureButton : UITableViewCellAccessory.DisclosureIndicator) : (detailButton ? UITableViewCellAccessory.DetailButton : UITableViewCellAccessory.None);
            base.EditingAccessory = UITableViewCellAccessory.None;
            base.AutoresizingMask = UIViewAutoresizing.FlexibleHeight;
            base.ClipsToBounds = true;
            TextLabel.Frame = new RectangleF(0, 0, 50, 30);
            Height = DEFAULT_HEIGHT;
            AccessoryItemConstructor = null;
        }

        /// <summary>
        /// After calling this, the user will be able to check and uncheck this item.
        /// </summary>
        public void EnableCheckmark()
        {
            Selected += (o, e) => IsChecked = !IsChecked;
        }

        public void PerformSelection(BaseListView parent)
        {
            Selected.SafeInvoke(this, parent);
        }
        public void PerformDeselection(BaseListView parent)
        {
            Deselected.SafeInvoke(this, parent);
        }
        public void PerformInfoButtonClick(BaseListView parent)
        {
            InfoButtonClicked.SafeInvoke(this, parent);
        }

        /// <summary>
        /// Invokes the UpdateAction
        /// </summary>
        public void PerformUpdate()
        {
            UpdateAction.SafeInvoke(this);
        }

        public virtual ListViewItem ConstructAccessoryItem()
        {
            if (AccessoryItemConstructor == null)
                return null;

            AccessoryItemExpanded.SafeInvoke();
            return AccessoryItemConstructor(this);
        }
        public virtual void DestructAccessoryItem()
        {
            AccessoryItemCollapsed.SafeInvoke();
        }
    }

    public class CheckListViewItem : ListViewItem
    {
        

        public CheckListViewItem(bool subtitle)
            : base(false, false, subtitle)
        {
            Selected += (o, e) => IsChecked = !IsChecked;
            IsSelectable = true;
            PersistentSelection = false;
        }
    }

    public class PlainListViewItem : ListViewItem
    {
        public View View { get; private set; }

        private float height;

        /// <summary>
        /// Returns the current actual height of the item.
        /// Sets the height to an absolute value (>=0), content height (<0) or default height (NaN)
        /// </summary>
        public override float Height { get { return (float.IsNaN(height) ? DEFAULT_HEIGHT : (height < 0 ? View.GetMinSize(new Vector2D<float>(float.MaxValue, float.MaxValue)).Y : height)); } set { height = value; } }

        public PlainListViewItem(View view)
            : base(false, false, false)
        {
            this.View = view;
            if (view == null) throw new ArgumentNullException("content");
            AddSubview(new PlatformViewWrapper(view).NativeView);
            Height = -1;
        }

        public override void LayoutSubviews()
        {
            Application.UILog.Log("plain list view item layout");
            View.Size = this.Bounds.Size.ToVector2D();
            View.UpdateLayout();
            base.LayoutSubviews();
        }
    }
}