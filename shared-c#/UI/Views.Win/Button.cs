using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class Button : View<System.Windows.Controls.Button>
    {
        public event Action<Button> Triggered;
        public string Text { get { return (string)nativeView.Content; } set { nativeView.Content = value; } }

        public Button()
        {
            nativeView.Click += (o, e) => Triggered.SafeInvoke(this);
        }
    }
}
