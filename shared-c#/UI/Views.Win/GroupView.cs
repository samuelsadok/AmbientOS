using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.UI
{
    public class GroupView : View<System.Windows.Controls.GroupBox>
    {
        private View view;
        public string Title { get { return (string)nativeView.Header; } set { nativeView.Header = value; } }
        public View Content { get { return view; } set { nativeView.Content = (view = value).ToNativeView(); } }
    }
}
