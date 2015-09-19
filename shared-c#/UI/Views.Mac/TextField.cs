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
    /// <summary>
    /// Enhances the normal text view by hiding the carent when it's read-only
    /// </summary>
    public class EnhancedTextView : UITextView
    {
        public override CoreGraphics.CGRect GetCaretRectForPosition(UITextPosition position)
        {
            // this is kind of a hack, the ShouldChangeText method is implemented by TextField to make it read only
            if (this.Delegate.ShouldChangeText(this, new NSRange(this.GetOffsetFromPosition(this.BeginningOfDocument, position), 1), ""))
                return base.GetCaretRectForPosition(position);
            else
                return RectangleF.Empty;
        }
    }


    public class TextField : View<EnhancedTextView>, ITextBox
    {
        public const float TEXT_PADDING = 8f;
        public const float PLACEHOLDER_OPACITY = 0.7f;

        private UILabel placeholderLabel = new UILabel() { Opaque = true, Hidden = false, TextColor = UIColor.LightGray };
        private Toolbar keyboardToolbar = null;

        public event Action<TextField> EditingStarted;
        public event Action<TextField> EditingEnded;
        public event Action<ITextBox> TextChanged;

        public string Text { get { return nativeView.Text; } set { nativeView.Text = value; } }
        public string PlaceholderText { get { return placeholderLabel.Text; } set { placeholderLabel.Text = value; } }
        public float FontSize { get { return (float)nativeView.Font.PointSize; } set { nativeView.Font = placeholderLabel.Font = nativeView.Font.WithSize(value); } }
        public Color TextColor { get { return nativeView.TextColor.ToColor(); } set { nativeView.TextColor = value.ToUIColor(); } }
        public TextAlignment TextAlignment { get { return Abstraction.ToTextAlignment(nativeView.TextAlignment); } set { nativeView.TextAlignment = Abstraction.ToUITextAlignment(value); } }
        public Toolbar KeyboardToolbar { get { return keyboardToolbar; } set { nativeView.InputAccessoryView = new PlatformViewWrapper(keyboardToolbar = value).NativeView; } }
        public bool IsReadOnly { get; set; }

        private class TextViewDelegate : UITextViewDelegate
        {
            public TextField Parent { get; set; }

            public event Action EditingStartedEvent;
            public event Action EditingEndedEvent;
            public event Action ChangedEvent;

            public override void EditingStarted(UITextView textView)
            {
                EditingStartedEvent.SafeInvoke();
            }

            public override void EditingEnded(UITextView textView)
            {
                EditingEndedEvent.SafeInvoke();
            }

            public override void Changed(UITextView textView)
            {
                ChangedEvent.SafeInvoke();
            }

            public override bool ShouldBeginEditing(UITextView textView)
            {
                return true;
            }

            public override bool ShouldChangeText(UITextView textView, NSRange range, string text)
            {
                return !Parent.IsReadOnly;
            }

           
        }

        public TextField(bool useDefaultToolbar)
            : base(true)
        {
            nativeView.AddSubview(placeholderLabel);
            
            Text = "";

            TextChanged += (o) => placeholderLabel.Alpha = (Text == "" ? PLACEHOLDER_OPACITY : 0f);
            nativeView.Font = placeholderLabel.Font = UIFont.SystemFontOfSize(UIFont.SmallSystemFontSize);
            nativeView.Layer.CornerRadius = 5;
            nativeView.Layer.BorderWidth = 1;
            nativeView.Frame = new RectangleF(0, 0, 50, 400f);
            
            BorderColor = Color.Clear;


            // configure keyboard
            nativeView.KeyboardType = UIKeyboardType.Default;
            nativeView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.None;
            nativeView.KeyboardAppearance = UIKeyboardAppearance.Light;

            if (useDefaultToolbar) {
                ToolbarButton tBarDone = new ToolbarButton(ToolbarButton.iOSNavigationBarItemType.Done);
                tBarDone.Triggered += (o) => { SetFocus(false); };
                KeyboardToolbar = new Toolbar(new ToolbarSpacer(), tBarDone);
            }

            var del = new TextViewDelegate() { Parent = this };
            del.EditingStartedEvent += () => EditingStarted.SafeInvoke(this);
            del.EditingEndedEvent += () => EditingEnded.SafeInvoke(this);
            del.ChangedEvent += () => TextChanged.SafeInvoke(this);
            nativeView.Delegate = del;
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            return PlatformUtilities.MeasureStringSize(nativeView.Font, maxSize, Text, PlaceholderText, string.Concat(Enumerable.Repeat("a\n", 20)));
        }

        protected override void UpdateContentLayout()
        {
            placeholderLabel.Frame = new RectangleF(TEXT_PADDING, TEXT_PADDING, Size.X - TEXT_PADDING * 2, 0);
            placeholderLabel.SizeToFit();

            nativeView.ContentInset = new UIEdgeInsets(Padding.Top, Padding.Left, Padding.Bottom, Padding.Right);
            nativeView.ScrollIndicatorInsets = new UIEdgeInsets(Padding.Top, Padding.Left, Padding.Bottom, Padding.Right);

            base.UpdateContentLayout();
        }

        public void SetFocus(bool focus)
        {
            if (focus)
                nativeView.BecomeFirstResponder();
            else
                nativeView.ResignFirstResponder();
        }
    }
}