using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;
using AppInstall.Graphics;

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
            /// The feature can be added below a view and the ConstructView method is implemented.
            /// This feature has a fixed width and is displayed at the bottom right.
            /// </summary>
            BottomSimple = 1,

            /// <summary>
            /// The feature can be added below a view and the ConstructView method is implemented.
            /// This feature has a flexible width and is used to fill the remaining bottom.
            /// </summary>
            BottomAggregate = 2,
        };

        /// <summary>
        /// Indicates one or several modes in which the features can be displayed.
        /// </summary>
        public virtual DisplayMode SupportedModes { get { return DisplayMode.None; } }

        /// <summary>
        /// Constructs a view that represents this feature.
        /// </summary>
        public virtual View ConstructSimpleView(View containingView)
        {
            if ((SupportedModes & (DisplayMode.BottomSimple)) == DisplayMode.None)
                throw new NotSupportedException("this feature cannot be displayed as a simple view");
            throw new NotImplementedException("this feature cannot be displayed as a simple view");
        }

        /// <summary>
        /// Constructs a view that represents multiple features of the same type.
        /// This function is used for features that support the BottomFill mode.
        /// </summary>
        public virtual View ConstructAggregateView(View containingView, IEnumerable<FeatureController> features)
        {
            if ((SupportedModes & (DisplayMode.BottomAggregate)) == DisplayMode.None)
                throw new NotSupportedException("this feature cannot be displayed as an aggregate view");
            throw new NotImplementedException("this feature cannot be displayed as an aggregate view");
        }
    }


    public partial class InvokableFeature : FeatureController
    {
        public override FeatureController.DisplayMode SupportedModes { get { return base.SupportedModes | DisplayMode.BottomSimple; } }

        public override View ConstructSimpleView(View containingView)
        {
            var btn = new Button() { Text = this.Text };
            btn.Triggered += (o) => Invoke();
            return btn;
        }
    }



    public partial class ActivityFeature : InvokableFeature
    {
        public override FeatureController.DisplayMode SupportedModes { get { return base.SupportedModes | DisplayMode.BottomSimple | DisplayMode.BottomAggregate; } }

        public override View ConstructAggregateView(View containingView, IEnumerable<FeatureController> features)
        {
            LayerLayout supplementaryInfo = new LayerLayout();

            var dict = features.Cast<ActivityFeature>().ToDictionary(a => a.ActivityTracker);

            AggregateActivity activity = new AggregateActivity(dict.Keys.ToArray());
            ActivityIndicator activityIndicator = new ActivityIndicator();

            Label exceptionLabel = new Label() { TextColor = Color.Red };
            exceptionLabel.Selected += () => {
                ExceptionDisplayCommand.Data = activity.LastFailedChild.LastException;
                ExceptionDisplayCommand.Invoke();
            };

            Action updateStatus = () => {
                if (activity.Status == ActivityStatus.Active) {
                    containingView.Enabled = !(activityIndicator.Active = true);
                    supplementaryInfo.Replace(activityIndicator);
                } else if (activity.Status == ActivityStatus.Failed) {
                    var failed = activity.LastFailedChild;
                    exceptionLabel.Text = dict[failed].ErrorMessageFactory(failed.LastException);
                    containingView.Enabled = !(activityIndicator.Active = false);
                    supplementaryInfo.Replace(exceptionLabel);
                } else {
                    containingView.Enabled = !(activityIndicator.Active = false);
                    supplementaryInfo.Remove(activityIndicator, false, duration: 0);
                }
            };

            activity.StatusChanged += (o, e) => Platform.InvokeMainThread(() => updateStatus());
            updateStatus();

            return supplementaryInfo;
        }
    }


    public partial class StandardFeature : InvokableFeature
    {
        // everything already defined
    }
}
