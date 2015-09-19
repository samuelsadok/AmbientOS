using System;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public class Label : View<System.Windows.Controls.TextBlock>
    {
        public event Action Selected;

        public string Text { get { return (string)nativeView.Text; } set { nativeView.Text = value; } }
        public Color TextColor { get { return textColor; } set { nativeView.Foreground = (textColor = value).ToBrush(); } }
        Color textColor;
        public TextAlignment TextAlignment { get { return nativeView.TextAlignment.ToTextAlignment(); } set { nativeView.TextAlignment = value.ToWPFTextAlignment(); } }

        public Label()
        {
            nativeView.Focusable = false;
            nativeView.GotFocus += (o, e) => Application.UILog.Log("got focus");
            nativeView.GotKeyboardFocus += (o, e) => Application.UILog.Log("got keyboard focus");
            nativeView.PreviewMouseUp += (o, e) => Application.UILog.Log("soon mouse up on label");
            nativeView.MouseUp += (o, e) => Application.UILog.Log("mouse up on label");
            nativeView.MouseUp += (o, e) => Selected.SafeInvoke();
            nativeView.TextAlignment = System.Windows.TextAlignment.Center;
            Text = "";
        }
    }
}
