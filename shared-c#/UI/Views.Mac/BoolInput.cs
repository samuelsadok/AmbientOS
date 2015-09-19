using System;
using UIKit;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class BoolInput : View<UISwitch>
    {
        public bool? Value { get { return nativeView.On; } set { if (!value.HasValue) throw new NotImplementedException(); nativeView.On = value.Value; } }

        public event Action<bool?> ValueChanged;


        public BoolInput()
        {
            nativeView.ValueChanged += (o, e) => ValueChanged.SafeInvoke(Value);
        }
    }
}