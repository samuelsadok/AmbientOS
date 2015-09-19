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
    public class Button : View<UIButton>
    {
        public event Action<Button> Triggered;

        public string Text { get { return (nativeView.TitleLabel == null ? "" : nativeView.TitleLabel.Text); } set { if (nativeView.TitleLabel != null) nativeView.SetTitle(value, UIControlState.Normal); } }
        public float FontSize { get { return (float)nativeView.Font.PointSize; } set { nativeView.Font = nativeView.Font.WithSize(value); } }
        public Color TextColor { get { return nativeView.CurrentTitleColor.ToColor(); } set { nativeView.SetTitleColor(value.ToUIColor(), UIControlState.Normal); } }

        public Button()
            : base(new UIButton(UIButtonType.RoundedRect), true) // todo: implement padding
        {
            nativeView.Layer.CornerRadius = 5;
            nativeView.Layer.BorderWidth = 1;
            //button.Layer.BorderColor = button.TintColor.CGColor;
            nativeView.TouchUpInside += (o, e) => Triggered.SafeInvoke(this);
            BackgroundColor = Color.Clear;
            BorderColor = Color.Clear;
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            return PlatformUtilities.MeasureStringSize(Text, nativeView.Font, maxSize);
        }
    }
}