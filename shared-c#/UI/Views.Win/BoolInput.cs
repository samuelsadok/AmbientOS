using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class BoolInput : View<System.Windows.Controls.CheckBox>
    {
        public bool? Value { get { return nativeView.IsChecked; } set { nativeView.IsChecked = value; } }

        public event Action<bool?> ValueChanged;


        public BoolInput()
        {
            nativeView.Checked += (o, e) => ValueChanged.SafeInvoke(true);
            nativeView.Unchecked += (o, e) => ValueChanged.SafeInvoke(false);
        }
    }
}
