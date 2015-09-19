using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{


    public abstract partial class FeatureController
    {
        /// <summary>
        /// The mode in which a feature can be displayed.
        /// </summary>
        public enum DisplayMode
        {
            /// <summary>
            /// No display mode is implemented by this feature.
            /// </summary>
            None = 0,

            /// <summary>
            /// The feature can be added below a listview section and the ConstructListViewItem method is implemented.
            /// </summary>
            ListViewItem = 1,

            /// <summary>
            /// The feature can be added to a list and the AddToList method is implemented.
            /// </summary>
            ListFeature = 2,

            /// <summary>
            /// The feature can be displayed in a navigation bar and the ConstructToolbarButton method is implemented.
            /// </summary>
            TopBarItem = 4,

            /// <summary>
            /// The feature can be displayed in a toolbar and the ConstructToolbarButton method is implemented.
            /// </summary>
            BottomBarItem = 8,

            /// <summary>
            /// The feature can be displayed both in a navigation bar (at the top) and in a toolbar (at the bottom)
            /// </summary>
            ToolbarItem = 12
        };

        /// <summary>
        /// Indicates one or several modes in which the features can be displayed.
        /// The modes shall be considered in the following order: ListViewItem - ListFeature - TopBarItem - BottomBarItem.
        /// </summary>
        public virtual DisplayMode SupportedModes { get { return DisplayMode.None; } }

        /// <summary>
        /// Constructs the feature in the form of a toolbar item.
        /// This may be unsupported for some features (see SupportedModes).
        /// </summary>
        public virtual IToolbarItem ConstructToolbarButton() {
            if ((SupportedModes & DisplayMode.ToolbarItem) == DisplayMode.None)
                throw new NotSupportedException("this feature cannot be displayed as a toolbar item");
            throw new NotImplementedException("this feature cannot be displayed as a toolbar item");
        }

        /// <summary>
        /// Constructs the alternative version of this feature in the form of a toolbar item.
        /// This is intended for features like "edit" that are associated with a "done" button.
        /// If no alternative mode exists for the feature, the function returns null.
        /// This is only supported if ConstructToolbarButton is supported.
        /// </summary>
        public virtual IToolbarItem ConstructAlternativeToolbarButton()
        {
            if ((SupportedModes & DisplayMode.ToolbarItem) == DisplayMode.None)
                throw new NotSupportedException("this feature cannot be displayed as a toolbar item");
            return null;
        }

        /// <summary>
        /// Incorporates the feature directly into a list view. This may be appropriate e.g. for an activity indicator.
        /// todo: mitigate the effect of adding multiple features of the same type to the same listview
        /// </summary>
        public virtual void AddToList(ListView listView)
        {
            if ((SupportedModes & DisplayMode.ListFeature) == DisplayMode.None)
                throw new NotSupportedException("this feature cannot be incorporated into a list view");
        }

        /// <summary>
        /// Constructs the feature in the form of a list view item.
        /// This may be unsupported for some features (see SupportedModes).
        /// </summary>
        public virtual ListViewItem ConstructListViewItem()
        {
            if ((SupportedModes & DisplayMode.ListViewItem) == DisplayMode.None)
                throw new NotSupportedException("this feature cannot be displayed as a list view item");

            return new ListViewItem(true, false, false) {
                Text = this.Text + "...",
                PersistentSelection = false,
                IsSelectable = true
            };
        }
    }

    public partial class InvokableFeature : FeatureController
    {
        public override FeatureController.DisplayMode SupportedModes { get { return base.SupportedModes | DisplayMode.ListViewItem | DisplayMode.ToolbarItem; } }

        public override IToolbarItem ConstructToolbarButton()
        {
            var btn = new ToolbarButton(this.Text);
            btn.Triggered += (o) => Invoke();
            return btn;
        }

        public override ListViewItem ConstructListViewItem()
        {
            var item = base.ConstructListViewItem();
            item.Selected += (o, e) => Invoke();
            return item;
        }
    }


    public partial class ActivityFeature : InvokableFeature
    {
        public override FeatureController.DisplayMode SupportedModes { get { return DisplayMode.ListFeature; } }

        public override void AddToList(ListView listView)
        {
            // todo: ensure that only one instance of this feature is in the same view controller
            listView.RefreshControl.InactiveText = Text;
            listView.RefreshControl.ActiveText = ActiveText;
            listView.RefreshControl.RefreshAction = Invoke;
            listView.RefreshControl.Visible = true;

            if (ExceptionDisplayCommand != null) {
                listView.StatusHeader.TouchUpInside += () => {
                    if (ActivityTracker.Status == ActivityStatus.Failed) {
                        ExceptionDisplayCommand.Data = ActivityTracker.LastException;
                        ExceptionDisplayCommand.Invoke();
                    }
                };
            } else {
                Application.UILog.Log("have no error view");
            }

            Action updateStatus = () => {
                Application.UILog.Log("stauts changed to " + ActivityTracker.Status);
                if (ActivityTracker.Status == ActivityStatus.Active) {
                    listView.RefreshControl.BeginRefreshing();
                    listView.StatusHeader.Visible = true;
                    listView.StatusHeader.SetText("???", null);
                } else {
                    listView.RefreshControl.EndRefreshing();
                    listView.StatusHeader.Visible = true;
                    if (ActivityTracker.Status == ActivityStatus.Failed) {
                        listView.StatusHeader.SetText(ErrorMessageFactory(ActivityTracker.LastException), false);
                    } else if (ActivityTracker.Status == ActivityStatus.Succeeded) {
                        listView.StatusHeader.SetText(SuccessMessageFactory(ActivityTracker.LastSuccess), true);
                    } else {
                        listView.StatusHeader.SetText("???", null);
                    }
                }
                // todo: show error message
            };

            ActivityTracker.StatusChanged += (obj, status) => Platform.InvokeMainThread(() => updateStatus());

            updateStatus();
            
            // todo: remove pull down to submit
        }
    }


    public partial class StandardFeature : InvokableFeature
    {
        public override FeatureController.DisplayMode SupportedModes { get { return base.SupportedModes | (AlternativeAction == null ? DisplayMode.ListViewItem : DisplayMode.None) | DisplayMode.ToolbarItem; } }

        public Action AlternativeAction { get; set; }
        public StandardCommandType AlternativeType { get; set; }


        private ToolbarButton.iOSNavigationBarItemType ConvertType(StandardCommandType type) {
            switch (type) {
                case StandardCommandType.Add: return ToolbarButton.iOSNavigationBarItemType.Add;
                case StandardCommandType.Edit: return ToolbarButton.iOSNavigationBarItemType.Edit;
                case StandardCommandType.Delete: return ToolbarButton.iOSNavigationBarItemType.Delete;
                case StandardCommandType.Done: return ToolbarButton.iOSNavigationBarItemType.Done;
            }
            throw new Exception("invalid value");
        }

        public override IToolbarItem ConstructToolbarButton()
        {
            var btn = new ToolbarButton(ConvertType(Type));
            btn.Triggered += (o) => Invoke();
            return btn;
        }

        public override IToolbarItem ConstructAlternativeToolbarButton()
        {
            if (AlternativeAction == null)
                return null;
            var btn = new ToolbarButton(ConvertType(AlternativeType));
            btn.Triggered += (o) => AlternativeAction();
            return btn;
        }

        public override ListViewItem ConstructListViewItem()
        {
            if (AlternativeAction != null)
                throw new NotSupportedException("this type of button cannot be constructed as a list view item");

            var item = base.ConstructListViewItem();
            item.Selected += (o, e) => Invoke();
            return item;
        }
    }
}
