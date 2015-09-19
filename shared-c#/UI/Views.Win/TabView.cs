using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.UI
{
    public class TabView : View<System.Windows.Controls.TabControl>
    {
        public TabView(params TabViewItem[] items)
        {
            //nativeView.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
            //nativeView.VerticalContentAlignment = System.Windows.VerticalAlignment.Stretch;
            foreach (var i in items)
                nativeView.Items.Add(new System.Windows.Controls.TabItem() { Header = i.Header, Content = i.Content.ToNativeView() });
        }
    }

    public class TabViewItem
    {
        public string Header { get; set; }
        public View Content { get; set; }
    }
}
