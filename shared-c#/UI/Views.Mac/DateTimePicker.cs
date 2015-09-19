using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Foundation;
using UIKit;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class DateTimePicker : View<UIDatePicker>
    {
        public event Action<DateTime?> ValueChanged;

        public enum SelectionMode
        {
            Date = 1,
            Time = 2,
            DateTime = 3
        }

        public SelectionMode Mode
        {
            get
            {
                switch (nativeView.Mode) {
                    case UIDatePickerMode.Date: return SelectionMode.Date;
                    case UIDatePickerMode.Time | UIDatePickerMode.CountDownTimer: return SelectionMode.Time;
                    case UIDatePickerMode.DateAndTime: return SelectionMode.DateTime;
                    default: throw new InvalidOperationException("unknown mode: " + nativeView.Mode);
                }
            }
            set
            {
                switch (value) {
                    case SelectionMode.Date: nativeView.Mode = UIDatePickerMode.Date; break;
                    case SelectionMode.Time: nativeView.Mode = UIDatePickerMode.CountDownTimer; break;
                    case SelectionMode.DateTime: nativeView.Mode = UIDatePickerMode.DateAndTime; break;
                    default: throw new InvalidOperationException("unknown mode: " + nativeView.Mode);
                }
            }
        }

        public DateTime? Value { get { return nativeView.Date.ToDateTime(); } set { if (!value.HasValue) throw new NotImplementedException(); nativeView.Date = value.Value.ToNSDate(); } }

        public DateTimePicker()
        {
            // todo: make duration of 0 min selectable
            nativeView.ValueChanged += (o, e) => ValueChanged.SafeInvoke(Value);
        }
    }
}
