using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.UI
{
    /// <summary>
    /// An animated symbol that indicates background activity.
    /// Inactive by default.
    /// </summary>
    public class ActivityIndicator : View<System.Windows.Controls.ProgressBar>
    {
        public bool Active {
            get { return nativeView.IsVisible; }
            set { nativeView.Visibility = (value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden); }
        }

        public ActivityIndicator()
        {
            nativeView.IsIndeterminate = true;
            nativeView.Orientation = System.Windows.Controls.Orientation.Horizontal;
            Active = false;
            
            // may be used for error display:
            //nativeView.Foreground = System.Windows.Media.Brushes.Red;
        }
    }
}
