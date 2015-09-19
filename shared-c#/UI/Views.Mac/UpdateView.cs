using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using AppInstall.Framework;
using AppInstall.OS;
using Foundation;
using UIKit;
using CoreAnimation;
using CoreGraphics;

namespace AppInstall.UI
{
    class ProgressView : UIAlertView
    {
        private static int MESSAGE_BOX_WIDTH = 260;
        private static int PROGRESS_BAR_MARGIN = 20;
        private static int PROGRESS_BAR_LABEL_MARGIN = 10;
        
        /// <summary>
        /// Represents a dialog box with a progress bar that displays the state of a progress monitor instance.
        /// </summary>
        public ProgressView(string title, string message, ProgressMonitor progressMonitor)
            : base(title, message, null, "cancel")
        {
            UIView outerView = new UIView(new RectangleF(0, 0, 1000, 1000));

            UILabel label = new UILabel();
            label.TextAlignment = UITextAlignment.Right;
            label.Text = "100%";
            label.Font = UIFont.FromName(label.Font.Name, 12);
            label.SizeToFit();
            outerView.AddSubview(label);
            label.Frame = new CGRect(label.Superview.Frame.Width - label.Frame.Width, 0, label.Frame.Width, label.Frame.Height);
            label.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleBottomMargin;

            UIProgressView progressView = new UIProgressView(UIProgressViewStyle.Bar);
            outerView.AddSubview(progressView);
            progressView.Progress = (float)progressMonitor.Progress;
            progressView.Frame = new CGRect(0, 7, progressView.Superview.Frame.Width - label.Frame.Width - PROGRESS_BAR_LABEL_MARGIN, progressView.Superview.Frame.Height);
            progressView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

            this.SetValueForKey(outerView, new NSString("accessoryView"));
            outerView.Frame = new CGRect(0, 0, MESSAGE_BOX_WIDTH - PROGRESS_BAR_MARGIN * 2, label.Frame.Height);


            progressMonitor.ProgressChanged += (o, e) => Platform.InvokeMainThread(() => {
                progressView.Progress = (float)e;
                label.Text = Math.Round(e * 100) + "%";
            });

            progressView.Progress = (float)progressMonitor.Progress;
            label.Text = Math.Round(progressMonitor.Progress * 100) + "%";
        }

        protected override void Dispose(bool disposing)
        {
            Platform.InvokeMainThread(() => this.DismissWithClickedButtonIndex(-1, true));
            base.Dispose(disposing);
        }
    }
}