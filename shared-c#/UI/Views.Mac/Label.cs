using UIKit;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public class Label : View<UILabel>
    {
        public string SizeSampleText { get; set; }
        public string Text { get { return nativeView.Text; } set { nativeView.Text = value; } }
        public float FontSize { get { return (float)nativeView.Font.PointSize; } set { nativeView.Font = nativeView.Font.WithSize(value); } }
        public TextAlignment TextAlignment { get { return Abstraction.ToTextAlignment(nativeView.TextAlignment); } set { nativeView.TextAlignment = Abstraction.ToUITextAlignment(value); } }
        public Color TextColor { get { return nativeView.TextColor.ToColor(); } set { nativeView.TextColor = value.ToUIColor(); } }

        public Label()
        {
            nativeView.Lines = 0;
            nativeView.Text = ""; // text must not be null
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            return PlatformUtilities.MeasureStringSize(nativeView.Font, maxSize, Text, SizeSampleText);
        }
    }
}