using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Foundation;
using UIKit;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    class TextBox : View<UITextField>, ITextBox
    {
        public event Action<ITextBox> TextChanged;
        public event Action ReturnPressed;

        public string Text { get { return nativeView.Text; } set { nativeView.Text = value; } }
        public string Placeholder { get { return nativeView.Placeholder; } set { nativeView.Placeholder = value; } }
        public float FontSize { get { return (float)nativeView.Font.PointSize; } set { nativeView.Font = nativeView.Font.WithSize(value); } }
        public Color TextColor { get { return nativeView.TextColor.ToColor(); } set { nativeView.TextColor = value.ToUIColor(); } }
        public TextAlignment TextAlignment { get { return Abstraction.ToTextAlignment(nativeView.TextAlignment); } set { nativeView.TextAlignment = Abstraction.ToUITextAlignment(value); } }
        public bool Secure { get { return nativeView.SecureTextEntry; } set { nativeView.SecureTextEntry = value; } }
        public bool IsReadOnly { get; set; }

        private class TextFieldDelegate : UITextFieldDelegate
        {
            public event Action ReturnPressed;
            public TextBox parent { get; set; }

            public override bool ShouldReturn(UITextField textField)
            {
                textField.ResignFirstResponder();
                ReturnPressed.SafeInvoke();
                return false;
            }

            public override bool ShouldBeginEditing(UITextField textField)
            {
                return !parent.IsReadOnly;
            }
        }

        public TextBox()
        {
            Text = "";

            var del = new TextFieldDelegate() { parent = this };
            del.ReturnPressed += () => ReturnPressed.SafeInvoke();
            
            nativeView.Delegate = del;
            nativeView.ReturnKeyType = UIReturnKeyType.Done;
            nativeView.EditingChanged += (o, e) => TextChanged.SafeInvoke(this);
        }

        
    }
}