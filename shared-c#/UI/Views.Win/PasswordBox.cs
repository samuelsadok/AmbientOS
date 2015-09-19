using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public class PasswordBox : View<System.Windows.Controls.PasswordBox>, ITextBox
    {
        public event Action<ITextBox> TextChanged;

        public string Text { get { return nativeView.Password; } set { nativeView.Password = value; } }
        public bool IsReadOnly { get { return false; } set { if (value) throw new NotSupportedException("a password box cannot be read-only"); } }

        public Color TextColor { get { return textColor; } set { nativeView.Foreground = (textColor = value).ToBrush(); } }
        Color textColor = Color.Black;

        public PasswordBox()
        {
            nativeView.PasswordChanged += (o, e) => TextChanged.SafeInvoke(this);
        }

        /*
        public static PasswordBox ForField(FieldSource<string> fieldSource)
        {
            // todo: create link new field source

            if (fieldSource.IsReadOnly)
                throw new ArgumentException("the field source cannot be read-only");

            var textBox = new PasswordBox();

            Action updateAction = () => {
                var newText = fieldSource.Get();
                if (textBox.Password != newText)
                    textBox.Password = fieldSource.Get();
            };
            updateAction();
            textBox.PasswordChanged += () => 
                fieldSource.Set(textBox.Password);
            fieldSource.ValueChanged += (v) => 
                updateAction();

            return textBox;
        }
         * */
    }
}
