using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class DateTimePicker : View<System.Windows.Controls.DatePicker>
    {
        public event Action<DateTime?> ValueChanged;

        public bool CanSelectTime { get { return false; } set { if (value) throw new NotImplementedException(); } }

        public DateTime? Value { get { return nativeView.SelectedDate; } set { nativeView.SelectedDate = value; } }

        public DateTimePicker()
        {
            nativeView.SelectedDateChanged += (o, e) => ValueChanged.SafeInvoke(Value);
        }
    }
}
