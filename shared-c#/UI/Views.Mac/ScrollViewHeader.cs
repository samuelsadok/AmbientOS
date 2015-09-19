using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing;
using UIKit;
using CoreGraphics;
using AppInstall.Framework;
using AppInstall.Graphics;
using AppInstall.OS;

namespace AppInstall.UI
{
    /// <summary>
    /// Describes a control that can inserted at the top of a scroll view.
    /// </summary>
    public class ScrollViewHeader : UIView
    {
        /// <summary>
        /// If true, the view may be visible (either collapsed or expanded).
        /// </summary>
        public bool Visible { get { return !this.Hidden; } set { this.Hidden = !value; } }

        public bool blatest = false;

        /// <summary>
        /// If true, the view pops up if the containing scroll view is pulled down.
        /// Returns false if not overridden.
        /// </summary>
        public virtual bool Collapsed { get { return blatest; } }

        /// <summary>
        /// Can be used to override the maximum opacity of the view.
        /// </summary>
        protected virtual float Opacity { get { return 1f; } }

        public virtual float Height { get { return (float)Frame.Height; } }

        /// <summary>
        /// A value between 0 and 1.
        /// 0 when the view is entirely collapsed or invisible, 1 if the view is fully expanded.
        /// </summary>
        public float BoundedPull { get; protected set; }


        public event Action LayoutChanged;

        /// <summary>
        /// Must be invoked if the Collapsed or Visible property changes.
        /// </summary>
        protected void PerformLayoutChange()
        {
            LayoutChanged.SafeInvoke();
        }


        /// <summary>
        /// Invoked whenever the amount of pull changes.
        /// An inheriting implementation can use this funciton to perform actions if the view is pulled far enough.
        /// </summary>
        /// <param name="amount">The relative amount of pull (0: no neutral, 1: as much pull as the height of the view, value can be outside of this interval)</param>
        public virtual void PullChanged(float amount)
        {

            if (!Visible) {
                BoundedPull = 0f;
            } else if (!Collapsed) {
                BoundedPull = 1f;
            } else {
                BoundedPull = Math.Min(Math.Max(amount, 0f), 1f);
            }

            var opacity = Opacity;
            this.Alpha = Math.Min(Math.Max(opacity * BoundedPull, 0f), opacity);

            LayoutSubviews();
        }


        /// <summary>
        /// Must be invoked when the user starts dragging the containing scroll view.
        /// </summary>
        public virtual void DraggingStarted(UIScrollView scrollView)
        {
        }
            
        /// <summary>
        /// Must be invoked when the user stops dragging the containing scroll view.
        /// </summary>
        public virtual void DraggingEnded(UIScrollView scrollView)
        {
        }

        public ScrollViewHeader(float height)
            : base(new RectangleF(0, 0, 100f, height))
        {
            Visible = true;
        }





        //public event Action TouchUpOutside;
        //
        //public override void TouchesEnded(NSSet touches, UIEvent evt)
        //{
        //    var points = touches.Select((touch) => ((UITouch)touch).LocationInView(this));
        //    var inside = points.Select((point) => point.X >= this.Frame.X && point.X < this.Frame.X + this.Frame.Width && point.Y >= this.Frame.Y && point.Y < this.Frame.Y + this.Frame.Height);
        //
        //    if (inside.Any(x => x))
        //        TouchUpInside.SafeInvoke();
        //    if (inside.Any(x => !x))
        //        TouchUpOutside.SafeInvoke();
        //}
    }


    /// <summary>
    /// A control that starts a refresh action when the containing scroll view is pulled down far enough.
    /// </summary>
    public class RefreshControl : ScrollViewHeader
    {
        /// <summary>
        /// Duration (in seconds) of the expand and collapse animations
        /// </summary>
        private const float ANIMATION_DURATION = 0.5f;

        /// <summary>
        /// Relative pull required to activate the refresh action
        /// </summary>
        private const float REQUIRED_PULL = 1f;


        public bool IsRefreshing { get; private set; }

        public override bool Collapsed { get { return !IsRefreshing; } }

        private string activeText = null;
        private string inactiveText = null;

        public string ActiveText { get { return activeText; } set { activeText = value; UpdateText(); } }
        public string InactiveText { get { return inactiveText; } set { inactiveText = value; UpdateText(); } }

        private void UpdateText()
        {
            label.Text = (IsRefreshing ? activeText : inactiveText);
            LayoutSubviews();
        }

        /// <summary>
        /// Invoked when a refresh started either from UI or from code.
        /// </summary>
        public event Action StartedRefreshing;

        /// <summary>
        /// Invoked on a background thread when a refresh was issued by the user via the UI.
        /// The handler of this event should perform the underlying refresh actions and call EndRefreshing when appropriate.
        /// </summary>
        public Action RefreshAction { get; set; }


        private bool armed = false;
        private bool collapsing = false;


        UIActivityIndicatorView activityIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Gray) { HidesWhenStopped = false };
        UILabel label = new UILabel();

        public RefreshControl()
            : base(60f)
        {
            AddSubview(activityIndicator);
            AddSubview(label);
            label.Font = UIFont.PreferredFootnote;
        }

        public override void LayoutSubviews()
        {
            // todo: make animation nicer (e.g. rotation while pulling)

            if (collapsing) {
                var scale = (BoundedPull + 0.1f) / 1.1f; // animation doesn't work when scaling to 0
                activityIndicator.Transform = CoreGraphics.CGAffineTransform.MakeScale(scale, scale);
                label.Alpha = 0f;
            } else {
                //activityIndicator.Transform = CoreGraphics.CGAffineTransform.MakeRotation(2 * (float)Math.PI * pullProgress);
                //activityIndicator.Transform = CoreGraphics.CGAffineTransform.MakeScale(2f - pullProgress, pullProgress);
                activityIndicator.Transform = CoreGraphics.CGAffineTransform.MakeIdentity();
                label.Alpha = 1f;
            }

            // update frames
            var activityIndicatorSize = activityIndicator.SizeThatFits(new SizeF(float.MaxValue, float.MaxValue));
            var labelSize = label.SizeThatFits(new SizeF(float.MaxValue, float.MaxValue)) + new SizeF(0, 8f);
            activityIndicator.Frame = new CGRect((this.Frame.Width - activityIndicatorSize.Width) / 2, (this.Frame.Height - activityIndicatorSize.Height - label.Frame.Height) / 2, activityIndicatorSize.Width, activityIndicatorSize.Height);
            label.Frame = new CGRect((this.Frame.Width - labelSize.Width) / 2, (this.Frame.Height + activityIndicatorSize.Height - labelSize.Height) / 2, labelSize.Width, labelSize.Height);
            base.LayoutSubviews();
        }

        public override void PullChanged(float amount)
        {
            base.PullChanged(amount);

            if (amount >= REQUIRED_PULL && armed) {
                armed = false;
                BeginRefreshing(false);
                new Task(() => {
                    RefreshAction.SafeInvoke();
                }).Start();
            }
        }


        public override void DraggingStarted(UIScrollView scrollView)
        {
            if (!IsRefreshing) {
                armed = true;
                collapsing = false;
            }
        }

        public override void DraggingEnded(UIScrollView scrollView)
        {
            armed = false;
        }

        /// <summary>
        /// Puts the contol into refreshing mode.
        /// This function can be called from non-UI threads.
        /// </summary>
        public void BeginRefreshing()
        {
            BeginRefreshing(true);
        }

        /// <summary>
        /// Puts the contol into inactive mode.
        /// This function can be called from non-UI threads.
        /// </summary>
        public void EndRefreshing()
        {
            EndRefreshing(true);
        }

        private void BeginRefreshing(bool animate)
        {
            Platform.InvokeMainThread(() => {
                if (IsRefreshing)
                    return;
                IsRefreshing = true;
                UpdateText();
                activityIndicator.StartAnimating();

                Action animatedAction = () => {
                    PerformLayoutChange();
                };

                if (animate)
                    Animate(ANIMATION_DURATION, animatedAction);
                else
                    animatedAction();


                StartedRefreshing.SafeInvoke();
            });
        }


        private void EndRefreshing(bool animate)
        {
            Platform.InvokeMainThread(() => {
                if (!IsRefreshing)
                    return;
                IsRefreshing = false;
                collapsing = true;
                BoundedPull = 1f;
                UpdateText();
                activityIndicator.StopAnimating();

                Action animatedAction = () => {
                    BoundedPull = 0f;
                    LayoutSubviews();
                    PerformLayoutChange();
                };

                if (animate)
                    Animate(ANIMATION_DURATION, animatedAction);
                else
                    animatedAction();
            });
        }

    }


    /// <summary>
    /// A header showing a status message.
    /// todo: add icon to view, depending on status
    /// </summary>
    public class StatusHeader : ScrollViewHeader
    {
        UIButton label = new UIButton();

        private bool? status;

        public override bool Collapsed { get { return status.HasValue ? status.Value : true; } }

        public override float Height { get { return 32f; } }

        public event Action TouchUpInside;

        private IEnumerable<UIGestureRecognizer> GetGestureRecognizers(UIView view)
        {
            while (view != null) {
                if (view.GestureRecognizers != null)
                    foreach (var x in view.GestureRecognizers)
                        yield return x;
                view = view.Superview;
            }
        }


        public StatusHeader()
            : base(0f)
        {
            AddSubview(label);
            label.TitleLabel.Font = UIFont.PreferredFootnote;
            label.SetTitleColor(Color.Black.ToUIColor(), UIControlState.Normal);

            // todo: determine why taps are not recognized:
            // if an error is shown first, the tap works until the first time a success message is shown

            UIGestureRecognizer[] list = null;

            label.TouchDown += (o, e) => {
                Application.UILog.Log("touch down");
                list = GetGestureRecognizers(label.Superview.Superview).Where(x => x.Enabled).ToArray();
                foreach (var gr in list)
                    gr.Enabled = false;
            };

            label.TouchUpInside += (o, e) => {
                Application.UILog.Log("touch up inside");
                TouchUpInside.SafeInvoke();

                foreach (var gr in list)
                    gr.Enabled = true;
            };
            label.TouchUpOutside += (o, e) => {
                Application.UILog.Log("touch up outside");
                foreach (var gr in list)
                    gr.Enabled = true;
            };
        }

        public override void LayoutSubviews()
        {
            label.Transform = CoreGraphics.CGAffineTransform.MakeScale(1f, (BoundedPull + 0.1f) / 1.1f);

            this.Frame = new CGRect(Frame.Left, Frame.Top, Frame.Width, BoundedPull * Height);
            var labelSize = label.SizeThatFits(new SizeF(float.MaxValue, float.MaxValue)) + new SizeF(0, 8f);
            //label.Frame = new RectangleF((this.Frame.Width - labelSize.Width) / 2, (this.Frame.Height - labelSize.Height) / 2, labelSize.Width, labelSize.Height);
            label.Frame = new CGRect(0, 0, Frame.Width, Frame.Height);

            base.LayoutSubviews();
        }

        /// <summary>
        /// Sets the text of this status display.
        /// </summary>
        /// <param name="success">Indicates the success status (null: unknown or paused). For any status other than success, the view remains expanded.</param>
        public void SetText(string text, bool? success)
        {
            this.status = success;
            if (success.HasValue) {
                if (success.Value) {
                    BackgroundColor = new Color(Color.Green, 0.5f).ToUIColor();
                } else {
                    BackgroundColor = new Color(Color.Red, 0.5f).ToUIColor();
                }
            } else {
                BackgroundColor = new Color(Color.Orange, 0.5f).ToUIColor();
            }
            //label.Text = text;
            label.SetTitle(text, UIControlState.Normal);
            LayoutSubviews();

            Application.UILog.Log("label: " + string.Join(", ", GetGestureRecognizers(this).Select(x => x.ToString())));

            Animate(0.5f, () => {
                PerformLayoutChange();
            });
        }
    }
}