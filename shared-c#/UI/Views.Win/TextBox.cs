using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public class TextBox : View<System.Windows.Controls.TextBox>, ITextBox
    {
        public event Action<ITextBox> TextChanged;

        public string Text { get { return nativeView.Text; } set { nativeView.Text = value; } }
        public Color TextColor { get { return textColor; } set { nativeView.Foreground = (textColor = value).ToBrush(); } }
        Color textColor = Color.Black;
        public TextAlignment TextAlignment { get { return nativeView.TextAlignment.ToTextAlignment(); } set { nativeView.TextAlignment = value.ToWPFTextAlignment(); } }
        public bool IsReadOnly { get { return nativeView.IsReadOnly; } set { nativeView.IsReadOnly = value; } }

        public TextBox()
        {
            nativeView.TextChanged += (o, e) => TextChanged.SafeInvoke(this);
        }
    }

    public class TextField : TextBox
    {

    }
}
