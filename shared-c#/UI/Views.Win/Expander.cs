using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public class Expander : View<System.Windows.Controls.Expander>
    {
        public bool IsExpanded { get { return nativeView.IsExpanded; } set { nativeView.IsExpanded = value; } }
        View header;
        public View Header { get { return header; } set { nativeView.Header = (header = value).NativeView; } }
        public string Text
        {
            get { return (Header as Label).Text; }
            set
            {
                var headerLabel = Header as Label;
                if (headerLabel == null)
                    headerLabel = new Label() { TextColor = Color.Grey };
                headerLabel.Text = value;
                Header = headerLabel;
            }
        }

        public Expander()
        {
            nativeView.Content = new System.Windows.Controls.ItemsPresenter();
        }
    }
}
